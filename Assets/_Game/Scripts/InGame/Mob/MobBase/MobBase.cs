using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.Manager;
using InGame.ObjectPool;
using InGame.Player.Player_Base;

namespace InGame.Mob.MobBase
{
    /// <summary>
    /// 모든 몬스터의 최상위 추상 클래스입니다.
    /// <br/> 기본 스탯 관리, 상태 머신 기반 행동 제어, 전투(피격/DoT), 오브젝트 풀링 인터페이스를 제공합니다.
    /// </summary>
    public abstract class MobBase : MonoBehaviour, IObjectPoolUser
    {
        #region 1. 상수 및 정적 변수

        // 피격 사운드 중복 재생 방지를 위한 정적 쿨타임
        private const float k_HitSoundCooldown = 0.1f;
        private static float s_lastHitSoundTime;

        #endregion

        #region 2. 에디터 설정 (Inspector)

        [Header("몬스터 설정")] 
        [SerializeField, Tooltip("몬스터의 기본 스탯 데이터")] 
        protected MobStats m_stats;

        [SerializeField, Tooltip("현재 몬스터의 상태 (디버깅용)")] 
        protected MobState m_currentState;

        #endregion

        #region 3. 내부 상태 및 데이터

        /// <summary>
        /// 몬스터의 동작 상태 정의
        /// </summary>
        public enum MobState
        {
            Idle,
            Move,
            Stun,
            Attack,
            Die
        }

        // 상태 제어 플래그
        protected bool m_canMoveByState = true;
        
        // 타겟 참조
        protected PlayerBase m_player;
        protected Transform m_playerTransform;

        // 비동기 작업 관리 (DoT)
        private CancellationTokenSource m_dotCts;

        #endregion

        #region 4. 공개 프로퍼티 (Accessors)

        /// <summary>오브젝트 풀 관리를 위한 스포너 참조입니다.</summary>
        public ObjectPoolSpawner ObjectPoolSpawner { get; set; }

        // --- 스탯 래퍼 프로퍼티 ---
        public float MoveSpeed { get => m_stats.MoveSpeed; set => m_stats.MoveSpeed = value; }
        public float CurrentHp { get => m_stats.Hp; protected set => m_stats.Hp = value; }
        public float AttackDamage { get => m_stats.AttackDamage; set => m_stats.AttackDamage = value; }
        public float AttackSpeed { get => m_stats.AttackSpeed; set => m_stats.AttackSpeed = value; }
        public float AttackRange { get => m_stats.AttackRange; set => m_stats.AttackRange = value; }
        public float StunTime { get => m_stats.StunTime; set => m_stats.StunTime = value; }

        // --- 상태 프로퍼티 ---
        public bool IsDead { get; protected set; }
        public bool IsHit { get; protected set; }
        public MobState CurrentState => m_currentState;

        /// <summary>
        /// 현재 이동 가능 여부를 반환합니다. (몬스터 상태 + 게임 전체 일시정지 여부 고려)
        /// </summary>
        public bool IsMoveEnabled
        {
            get
            {
                if (GameManager.Instance == null || GameManager.Instance.State == null) return false;
                return m_canMoveByState && GameManager.Instance.State.IsPlaying;
            }
        }

        #endregion

        #region 5. 유니티 생명주기

        public virtual void OnEnable()
        {
            // 상태 초기화
            IsDead = false;
            IsHit = false;
            m_canMoveByState = true;

            // 이전 비동기 작업 정리
            ResetDotToken();

            // 이벤트 구독
            if (GameManager.Instance != null && GameManager.Instance.State != null)
            {
                GameManager.Instance.State.OnGameOver += OnGameOver;
            }
        }

        protected virtual void OnDisable()
        {
            // 비동기 작업 취소
            ResetDotToken();

            // 이벤트 해제
            if (GameManager.Instance != null && GameManager.Instance.State != null)
            {
                GameManager.Instance.State.OnGameOver -= OnGameOver;
            }
        }

        #endregion

        #region 6. 초기화 및 설정

        /// <summary>
        /// 몬스터의 추적 대상을 설정합니다.
        /// </summary>
        public virtual void SetTarget(PlayerBase target)
        {
            m_player = target;
            if (m_player != null)
            {
                // 일반적으로 모델링 구조상 최상위 부모를 타겟으로 잡음
                m_playerTransform = m_player.transform.parent != null ? m_player.transform.parent : m_player.transform;
            }
        }

        private void ResetDotToken()
        {
            if (m_dotCts != null)
            {
                m_dotCts.Cancel();
                m_dotCts.Dispose();
                m_dotCts = null;
            }
        }

        #endregion

        #region 7. 상태 제어 (State Machine)

        /// <summary>
        /// 몬스터의 상태를 변경하고 관련 플래그를 갱신합니다.
        /// </summary>
        public void SetState(MobState state)
        {
            m_currentState = state;

            // 상태별 이동 가능 여부 설정
            switch (state)
            {
                case MobState.Stun:
                case MobState.Die:
                    m_canMoveByState = false;
                    break;
                
                case MobState.Move:
                case MobState.Idle:
                case MobState.Attack:
                    m_canMoveByState = true;
                    break;
            }

            if (state == MobState.Die)
            {
                OnDie();
            }
        }

        /// <summary>
        /// 사망 처리 로직입니다.
        /// </summary>
        protected virtual void OnDie()
        {
            if (IsDead) return;

            IsDead = true;
            m_canMoveByState = false;
            ResetDotToken(); // DoT 중지

            // 킬 카운트 증가
            if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.PlayerData != null)
            {
                PlayerDataManager.Instance.PlayerData.nowPlayMObkillCOunt++;
            }

            // 오브젝트 반환 또는 파괴
            if (ObjectPoolSpawner != null)
            {
                ObjectPoolSpawner.ReturnMob(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnGameOver()
        {
            m_canMoveByState = false;
        }

        #endregion

        #region 8. 전투 및 데미지 로직 (Combat)

        /// <summary>
        /// 일반 데미지를 입힙니다. (자식 클래스에서 구체적 구현)
        /// </summary>
        /// <param name="damage">데미지 수치</param>
        /// <param name="stunTime">경직 시간 (0이면 경직 없음)</param>
        public virtual void TakeDamage(float damage, float stunTime = 0f)
        {
            // Base implementation can be empty or handle basic HP reduction
        }

        /// <summary>
        /// 지속 피해(DoT)를 적용합니다. 기존 DoT는 취소되고 새로운 DoT로 덮어씌워집니다.
        /// </summary>
        public void ApplyDamageOverTime(float totalDamage, float duration, int tickCount, Action onTickAction = null)
        {
            if (IsDead || tickCount <= 0 || duration <= 0) return;

            // 기존 DoT 취소 및 새 토큰 생성
            ResetDotToken();
            m_dotCts = new CancellationTokenSource();

            float damagePerTick = totalDamage / tickCount;
            float interval = duration / tickCount;

            // Fire-and-Forget 방식으로 비동기 루프 실행
            DamageOverTimeLoopAsync(damagePerTick, interval, tickCount, m_dotCts.Token, onTickAction).Forget();
        }

        private async UniTaskVoid DamageOverTimeLoopAsync(float damage, float interval, int ticks, CancellationToken token, Action onTickAction)
        {
            try
            {
                for (int i = 0; i < ticks; i++)
                {
                    // 인터벌 대기 (UniTask)
                    await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: token);
                    
                    if (IsDead) break;

                    TakeDotDamage(damage);
                    onTickAction?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                // DoT 취소됨 (새로운 DoT 적용, 사망, 혹은 비활성화)
            }
        }

        /// <summary>
        /// DoT 전용 데미지 처리 (피격 모션/사운드 없이 체력만 감소)
        /// </summary>
        protected virtual void TakeDotDamage(float damage)
        {
            if (IsDead) return;

            CurrentHp -= damage;
            if (CurrentHp <= 0)
            {
                OnDie();
            }
        }

        /// <summary>
        /// 이동 속도 감소(CC)를 적용합니다.
        /// </summary>
        public virtual void ApplySlow(float slowMultiplier, float duration)
        {
            // Override in child classes
        }

        /// <summary>
        /// 피격 이펙트 및 셰이더 효과를 재생합니다.
        /// </summary>
        public virtual void PlayDamageEffect(Color? color = null)
        {
            // Override in child classes
        }

        /// <summary>
        /// 전역 쿨타임을 고려하여 피격 사운드 재생 가능 여부를 확인합니다.
        /// </summary>
        protected bool CanPlayHitSound()
        {
            if (Time.time >= s_lastHitSoundTime + k_HitSoundCooldown)
            {
                s_lastHitSoundTime = Time.time;
                return true;
            }
            return false;
        }

        #endregion
    }
}