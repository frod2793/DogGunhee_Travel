using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using InGame.Manager;
using InGame.Player.Player_Base;
using InGame;

namespace InGame.Mob.MobBase
{
    /// <summary>
    /// 모든 몬스터의 최상위 부모 클래스입니다.
    /// 기본 스탯(MobStats), 전투 처리(피격, DoT), 오브젝트 풀링 인터페이스를 제공합니다.
    /// </summary>
    public abstract class MobBase : MonoBehaviour, IObjectPoolUser
    {
        #region 상수 및 정적 변수

        private const float k_HitSoundCooldown = 0.1f;
        private static float s_lastHitSoundTime;

        #endregion

        #region 공통 스탯 프로퍼티

        /// <summary>오브젝트 풀 관리를 위한 스포너 참조입니다.</summary>
        public ObjectPoolSpawner ObjectPoolSpawner { get; set; }

        /// <summary>몬스터의 주요 스탯을 관리하는 구조체입니다.</summary>
        [Header("몬스터 스탯")]
        [SerializeField] protected MobStats m_stats;
        
        // 편의를 위한 프로퍼티 래퍼 (외부 의존성 최소화를 위해 유지)
        public float MoveSpeed { get => m_stats.MoveSpeed; set => m_stats.MoveSpeed = value; }
        public float CurrentHp { get => m_stats.Hp; protected set => m_stats.Hp = value; }
        public float AttackDamage { get => m_stats.AttackDamage; set => m_stats.AttackDamage = value; }
        public float AttackSpeed { get => m_stats.AttackSpeed; set => m_stats.AttackSpeed = value; }
        public float AttackRange { get => m_stats.AttackRange; set => m_stats.AttackRange = value; }
        public float StunTime { get => m_stats.StunTime; set => m_stats.StunTime = value; }

        /// <summary>사망 여부입니다.</summary>
        public bool IsDead { get; protected set; }

        /// <summary>현재 피격 중인지 여부입니다.</summary>
        public bool IsHit { get; protected set; }

        /// <summary>이동 가능 여부입니다. (CC기, 게임 정지 등에 의해 제어됨)</summary>
        public bool IsMoveEnabled { get; protected set; }

        #endregion

        #region 내부 상태 및 참조 필드

        /// <summary>현재 타겟팅 중인 플레이어입니다.</summary>
        protected PlayerBase m_player; 
        
        /// <summary>플레이어의 메인 트랜스폼(부모 포함)입니다.</summary>
        protected Transform m_playerTransform;

        /// <summary>몬스터의 현재 상태(애니메이션 및 View 동기화용)입니다.</summary>
        public enum MobState { Idle, Move, Stun, Attack, Die }
        
        [SerializeField] 
        protected MobState m_currentState;
        
        /// <summary>현재 몬스터 상태를 반환합니다.</summary>
        public MobState CurrentState => m_currentState;

        // 지속 데미지(DoT) 관리용 토큰
        private CancellationTokenSource m_dotCts;

        #endregion

        #region Unity 라이프사이클 및 풀링

        public virtual void OnEnable()
        {
            IsDead = false;
            IsHit = false;
            IsMoveEnabled = true;
            
            // 이전 DoT 작업 취소
            m_dotCts?.Cancel();
            m_dotCts?.Dispose();
            m_dotCts = null;

            if (GameManager.Instance.State == null) return;
            
            // 게임 상태 이벤트 구독
            GameManager.Instance.State.OnGamePause += OnGamePause;
            GameManager.Instance.State.OnGameResume += OnGameResume;
            GameManager.Instance.State.OnGameOver += OnGameOver;
        }

        protected virtual void OnDisable()
        {
            // DoT 및 비동기 로직 정리
            m_dotCts?.Cancel();
            m_dotCts?.Dispose();
            m_dotCts = null;

            if (GameManager.Instance.State == null) return;
            
            // 게임 상태 이벤트 구독 해지
            GameManager.Instance.State.OnGamePause -= OnGamePause;
            GameManager.Instance.State.OnGameResume -= OnGameResume;
            GameManager.Instance.State.OnGameOver -= OnGameOver;
        }

        #endregion

        #region 초기화 및 설정

        /// <summary>
        /// 몬스터의 추적 대상을 설정합니다.
        /// </summary>
        public virtual void SetTarget(PlayerBase target)
        {
            m_player = target;
            if (m_player != null)
            {
                m_playerTransform = m_player.transform.parent;
            }
        }

        #endregion

        #region 상태 동기화 (애니메이션/뷰 전용)

        /// <summary>
        /// 몬스터의 상태를 설정합니다. (단순 상태 값 변경 및 이동 가능 여부 갱신)
        /// 로직은 Behavior Tree 등 외부 시스템에서 처리해야 합니다.
        /// </summary>
        public void SetState(MobState state)
        {
            m_currentState = state;
            
            // 상태에 따른 기본 플래그 설정
            switch (state)
            {
                case MobState.Stun:
                case MobState.Die:
                    IsMoveEnabled = false;
                    break;
                case MobState.Move:
                case MobState.Idle:
                case MobState.Attack:
                    // 게임이 일시정지 상태가 아니라면 이동 가능
                    if (GameManager.Instance.State != null && 
                        GameManager.Instance.State.PlayState != PlayStateManager.GameState.Pause)
                    {
                        IsMoveEnabled = true;
                    }
                    break;
            }

            if (state == MobState.Die)
            {
                OnDie();
            }
        }
        
        // Legacy Virtual Methods Removed (OnIdle, OnMove, etc.)

        protected virtual void OnDie()
        {
            if (!IsDead)
            {
                IsDead = true;
                IsMoveEnabled = false;
                
                m_dotCts?.Cancel(); // 사망 시 DoT 중지
                
                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.PlayerData.nowPlayMObkillCOunt++;
                }
                
                if (ObjectPoolSpawner != null)
                {
                    ObjectPoolSpawner.MobObjectPool.Release(this);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }

        #endregion

        #region 게임 상태 이벤트 핸들러

        /// <summary>
        /// 게임 일시 정지 시 호출됩니다.
        /// </summary>
        protected virtual void OnGamePause() => IsMoveEnabled = false;
        
        /// <summary>
        /// 게임 재개 시 호출됩니다.
        /// </summary>
        protected virtual void OnGameResume()
        {
            if (!IsDead && m_currentState != MobState.Stun)
            {
                IsMoveEnabled = true;
            }
        }

        /// <summary>
        /// 게임 오버 시 호출됩니다.
        /// </summary>
        private void OnGameOver() => IsMoveEnabled = false;

        #endregion

        #region 전투 인터페이스

        /// <summary>
        /// 데미지를 입고 체력을 감소시킵니다.
        /// </summary>
        /// <param name="damage">양의 데미지 양</param>
        /// <param name="stunTime">경직 시간 (초)</param>
        public virtual void TakeDamage(float damage, float stunTime = 0f) { }

        /// <summary>
        /// 지속 피해(DoT)를 적용합니다. 새로운 DoT가 적용되면 이전 DoT는 취소됩니다.
        /// </summary>
        public void ApplyDamageOverTime(float totalDamage, float duration, int tickCount, System.Action onTickAction = null)
        {
            if (IsDead || tickCount <= 0 || duration <= 0) return;

            // 이전 DoT 취소
            m_dotCts?.Cancel();
            m_dotCts?.Dispose();
            m_dotCts = new CancellationTokenSource();
            
            float damagePerTick = totalDamage / tickCount;
            float interval = duration / tickCount;

            DamageOverTimeLoopAsync(damagePerTick, interval, tickCount, m_dotCts.Token, onTickAction).Forget();
        }

        private async UniTaskVoid DamageOverTimeLoopAsync(float damage, float interval, int ticks, CancellationToken token, System.Action onTickAction)
        {
            try
            {
                for (int i = 0; i < ticks; i++)
                {
                    await UniTask.Delay(System.TimeSpan.FromSeconds(interval), cancellationToken: token);
                    if (IsDead) break;
                    
                    TakeDotDamage(damage);
                    onTickAction?.Invoke();
                }
            }
            catch (System.OperationCanceledException) { }
        }

        /// <summary>
        /// 지속 피해 전용 데미지 처리 (피격음, 스턴, 무적 시간 없음)
        /// </summary>
        protected virtual void TakeDotDamage(float damage)
        {
            if (IsDead) return;
            
            CurrentHp -= damage;
            
            // TODO: 필요한 경우 데미지 플로팅 텍스트 연동
            
            if (CurrentHp <= 0)
            {
                OnDie();
            }
        }

        /// <summary>
        /// 이동 속도 감소 효과(Slow)를 적용합니다.
        /// </summary>
        public virtual void ApplySlow(float slowMultiplier, float duration) { }

        /// <summary>
        /// 피격 이펙트를 재생합니다.
        /// </summary>
        public virtual void PlayDamageEffect(Color? color = null) { }

        /// <summary>
        /// 피격 사운드 재생 가능 여부를 확인하고 쿨타임을 갱신합니다.
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