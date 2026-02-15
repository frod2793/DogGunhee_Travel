using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.Managers;
using InGame.ObjectPool;
using InGame.Player.Player_Base;
using InGame.Mob.Systems;

namespace InGame.Mob.MobBase
{
    /// <summary>
    /// [설명]: 모든 몬스터의 최상위 추상 클래스입니다.
    /// 기본 스탯 관리, 상태 머신 기반 행동 제어, 전투(피격/DoT), 오브젝트 풀링 인터페이스를 제공합니다.
    /// </summary>
    public abstract class MobBase : MonoBehaviour, IObjectPoolUser, ITargetable
    {
        #region 상수 및 정적 변수

        /// <summary>
        /// [설명]: 피격 사운드 중복 재생 방지를 위한 정적 쿨타임
        /// </summary>
        private const float k_HitSoundCooldown = 0.1f;

        /// <summary>
        /// [설명]: 마지막으로 피격 사운드가 재생된 시간
        /// </summary>
        private static float s_lastHitSoundTime;

        #endregion

        #region 에디터 설정

        [Header("몬스터 설정")]
        [SerializeField, Tooltip("현재 몬스터의 상태 (디버깅용)")]
        protected MobState m_currentState;

        /// <summary>
        /// [설명]: 몬스터 인게임 비즈니스 로직
        /// </summary>
        protected MobLogic m_logic;

        /// <summary>
        /// [설명]: 몬스터 AI 의사결정 브레인
        /// </summary>
        protected MobBrain m_brain;

        #endregion

        #region 내부 상태 및 데이터

        /// <summary>
        /// [설명]: 몬스터의 동작 상태를 정의하는 열거형입니다.
        /// </summary>
        public enum MobState
        {
            /// <summary> [설명]: 대기 </summary>
            Idle,
            /// <summary> [설명]: 이동 </summary>
            Move,
            /// <summary> [설명]: 경직/기절 </summary>
            Stun,
            /// <summary> [설명]: 공격 </summary>
            Attack,
            /// <summary> [설명]: 사망 </summary>
            Die
        }

        /// <summary>
        /// [설명]: 상태별 이동 가능 여부 플래그
        /// </summary>
        protected bool m_canMoveByState = true;

        /// <summary>
        /// [설명]: 추적 타겟(플레이어) 참조
        /// </summary>
        protected PlayerBase m_player;

        /// <summary>
        /// [설명]: 타겟의 트랜스폼 참조
        /// </summary>
        protected Transform m_playerTransform;

        /// <summary>
        /// [설명]: 지속 피해(DoT) 비동기 작업 취소 토큰
        /// </summary>
        private CancellationTokenSource m_dotCts;

        /// <summary>
        /// [설명]: 몬스터 관리 시스템 참조
        /// </summary>
        protected MobManager m_mobManager;

        /// <summary>
        /// [설명]: 몬스터 경직/기절 타이머 트윈
        /// </summary>
        private DG.Tweening.Tween m_stunTween;

        /// <summary>
        /// [설명]: 플레이어 데이터 DTO 참조 (킬 카운트 등 기록용)
        /// </summary>
        protected InGame.Data.PlayerDataDTO m_playerData;

        /// <summary>
        /// [설명]: 사운드 서비스 참조 (DI)
        /// </summary>
        protected InGame.Services.ISoundManager m_soundManager;

        #endregion

        #region 공개 프로퍼티

        /// <summary>
        /// [설명]: 오브젝트 풀 관리를 위한 스포너 참조입니다.
        /// </summary>
        public ObjectPoolSpawner ObjectPoolSpawner { get; set; }

        /// <summary>
        /// [설명]: 몬스터의 이동 속도 (Logic 위임)
        /// </summary>
        public float MoveSpeed
        {
            get => m_logic?.MoveSpeed ?? 0f;
            set => m_logic?.InitializeStats(new MobStats(m_logic.CurrentHp, value, m_logic.AttackDamage, m_logic.AttackSpeed, m_logic.AttackRange, m_logic.StunResistance));
        }

        /// <summary>
        /// [설명]: 현재 체력
        /// </summary>
        public float CurrentHp => m_logic?.CurrentHp ?? 0f;

        /// <summary>
        /// [설명]: 최대 체력
        /// </summary>
        public float MaxHp => m_logic?.MaxHp ?? 0f;

        /// <summary>
        /// [설명]: 공격력
        /// </summary>
        public float AttackDamage => m_logic?.AttackDamage ?? 0f;

        /// <summary>
        /// [설명]: 공격 속도
        /// </summary>
        public float AttackSpeed => m_logic?.AttackSpeed ?? 0f;

        /// <summary>
        /// [설명]: 공격 사거리
        /// </summary>
        public float AttackRange => m_logic?.AttackRange ?? 0f;

        /// <summary>
        /// [설명]: 경직 저항력
        /// </summary>
        public float StunResistance => m_logic?.StunResistance ?? 0f;

        /// <summary>
        /// [설명]: 사망 여부
        /// </summary>
        public bool IsDead { get; protected set; }

        /// <summary>
        /// [설명]: 현재 피격 중 여부
        /// </summary>
        public bool IsHit { get; protected set; }

        /// <summary>
        /// [설명]: 현재 동작 상태
        /// </summary>
        public MobState CurrentState => m_logic?.CurrentState ?? m_currentState;

        /// <summary>
        /// [설명]: 현재 이동 가능 여부를 반환합니다. (몬스터 상태 및 게임 플레이 상태 고려)
        /// </summary>
        public bool IsMoveEnabled
        {
            get
            {
                if (GameManager.Instance == null || GameManager.Instance.State == null)
                {
                    return false;
                }
                return m_canMoveByState && GameManager.Instance.State.IsPlaying;
            }
        }

        /// <summary>
        /// [설명]: 몬스터의 현재 위치 (ITargetable 구현)
        /// </summary>
        public Vector3 Position => transform.position;

        /// <summary>
        /// [설명]: 몬스터의 트랜스폼 (ITargetable 구현)
        /// </summary>
        public Transform Transform => transform;

        /// <summary>
        /// [설명]: 오브젝트 활성화 여부 (ITargetable 구현)
        /// </summary>
        public bool IsActive => gameObject.activeInHierarchy;

        #endregion

        #region 유니티 생명주기

        /// <summary>
        /// [설명]: 오브젝트가 활성화될 때 호출되어 상태를 초기화하고 이벤트를 구독합니다.
        /// </summary>
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

            // 타겟 관리자 등록
            if (m_mobManager != null)
            {
                m_mobManager.Register(this);
            }

            m_brain?.OnEnable();
        }

        /// <summary>
        /// [설명]: 오브젝트가 비활성화될 때 호출되어 리소스를 정리하고 이벤트를 해제합니다.
        /// </summary>
        protected virtual void OnDisable()
        {
            m_brain?.OnDisable();

            // 타이머 정리
            m_stunTween?.Kill();
            m_stunTween = null;

            // 타겟 관리자 해제
            if (m_mobManager != null)
            {
                m_mobManager.Unregister(this);
            }

            // 비동기 작업 취소
            ResetDotToken();

            // 이벤트 해제
            if (GameManager.Instance != null && GameManager.Instance.State != null)
            {
                GameManager.Instance.State.OnGameOver -= OnGameOver;
            }
        }

        #endregion

        #region 초기화 및 설정

        /// <summary>
        /// [설명]: 몬스터 스폰 시 외부 시스템 참조를 주입받아 상태를 초기화합니다.
        /// </summary>
        /// <param name="mobManager">전역 몬스터 관리자</param>
        /// <param name="playerData">플레이어 데이터 (킬 카운트 등)</param>
        /// <param name="soundManager">사운드 매니저 (DI)</param>
        public virtual void Init(MobManager mobManager, InGame.Data.PlayerDataDTO playerData = null, InGame.Services.ISoundManager soundManager = null)
        {
            m_mobManager = mobManager;
            m_playerData = playerData;
            m_soundManager = soundManager;

            if (m_mobManager != null)
            {
                m_mobManager.Register(this);
            }

            // 상태 초기화
            m_currentState = MobState.Idle; // 기본 상태
            IsDead = false;
            IsHit = false;
        }

        /// <summary>
        /// [설명]: 몬스터의 추적 대상(플레이어)을 설정합니다.
        /// </summary>
        /// <param name="target">추적할 플레이어 객체</param>
        public virtual void SetTarget(PlayerBase target)
        {
            m_player = target;
            if (m_player != null)
            {
                // 일반적으로 모델링 구조상 최상위 부모를 타겟으로 잡음
                m_playerTransform = m_player.transform.parent != null ? m_player.transform.parent : m_player.transform;
            }
        }

        /// <summary>
        /// [설명]: 지속 피해(DoT) 작업을 위한 토큰을 초기화화합니다.
        /// </summary>
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

        #region 상태 제어

        /// <summary>
        /// [설명]: 몬스터의 현재 상태를 변경하고 관련 물리/행동 플래그를 갱신합니다.
        /// </summary>
        /// <param name="state">변경할 상태</param>
        public virtual void SetState(MobState state)
        {
            if (m_logic == null)
            {
                return;
            }

            m_logic.SetState(state);
            m_currentState = state;

            // 상태별 이동 가능 여부 플래그 동기화
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
        /// [설명]: 몬스터 사망 시 처리 로직입니다. (데이터 갱신 및 풀 반환)
        /// </summary>
        protected virtual void OnDie()
        {
            if (IsDead)
            {
                return;
            }

            IsDead = true;
            m_canMoveByState = false;
            ResetDotToken(); // DoT 중지

            // 킬 카운트 증가
            if (m_playerData != null)
            {
                m_playerData.NowPlayMobKillCount++;
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

        /// <summary>
        /// [설명]: 게임 오버 시 몬스터의 행동을 중지합니다.
        /// </summary>
        private void OnGameOver()
        {
            m_canMoveByState = false;
        }

        #endregion

        #region 전투 및 데미지

        /// <summary>
        /// [설명]: 외부로부터 데미지를 입었을 때 처리하는 호출 시점입니다.
        /// </summary>
        /// <param name="damage">입힐 데미지 수치</param>
        /// <param name="stunTime">적용할 경직 시간 (기본값 0)</param>
        public virtual void TakeDamage(float damage, float stunTime = 0f)
        {
            if (IsDead || m_logic == null)
            {
                return;
            }
            m_logic.TakeDamage(damage, stunTime);

            if (stunTime > 0)
            {
                ApplyStun(stunTime);
            }
        }

        /// <summary>
        /// [설명]: 지속 피해(DoT) 효과를 적용합니다. 기존에 실행 중인 DoT가 있다면 덮어씌웁니다.
        /// </summary>
        /// <param name="totalDamage">총 피해량</param>
        /// <param name="duration">지속 시간</param>
        /// <param name="tickCount">총 틱 횟수</param>
        /// <param name="onTickAction">매 틱마다 실행할 콜백</param>
        public void ApplyDamageOverTime(float totalDamage, float duration, int tickCount, Action onTickAction = null)
        {
            if (IsDead || tickCount <= 0 || duration <= 0)
            {
                return;
            }

            // 기존 DoT 취소 및 새 토큰 생성
            ResetDotToken();
            m_dotCts = new CancellationTokenSource();

            float damagePerTick = totalDamage / tickCount;
            float interval = duration / tickCount;

            // Fire-and-Forget 방식으로 비동기 루프 실행
            DamageOverTimeLoopAsync(damagePerTick, interval, tickCount, m_dotCts.Token, onTickAction).Forget();
        }

        /// <summary>
        /// [설명]: 지속 피해를 루프 단위로 처리하는 비동기 메서드입니다.
        /// </summary>
        private async UniTaskVoid DamageOverTimeLoopAsync(float damage, float interval, int ticks, CancellationToken token, Action onTickAction)
        {
            try
            {
                for (int i = 0; i < ticks; i++)
                {
                    // 인터벌 대기 (UniTask)
                    await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: token);

                    if (IsDead)
                    {
                        break;
                    }

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
        /// [설명]: 지속 피해(DoT) 전용 데미지 처리 로직입니다. (피격 연출 없이 체력만 감소)
        /// </summary>
        /// <param name="damage">입힐 데미지 수치</param>
        protected virtual void TakeDotDamage(float damage)
        {
            if (IsDead || m_logic == null)
            {
                return;
            }
            m_logic.TakeDamage(damage);
        }

        /// <summary>
        /// [설명]: 몬스터에게 경직(Stun) 상태를 적용합니다. 저항력 수치에 따라 시간이 보정됩니다.
        /// </summary>
        /// <param name="duration">경직 지속 시간</param>
        public virtual void ApplyStun(float duration)
        {
            if (IsDead || m_logic == null)
            {
                return;
            }

            // 경직 저항 공식 적용: 최종 시간 = 입력 시간 * (1 - 저항력)
            float resistance = m_logic.StunResistance;
            float finalDuration = duration * (1.0f - Mathf.Clamp01(resistance));

            if (finalDuration <= 0)
            {
                return;
            }

            // 기존 경직 타이머가 있다면 확실히 취소
            m_stunTween?.Kill();

            m_logic.SetState(MobState.Stun);

            m_stunTween = DG.Tweening.DOVirtual.DelayedCall(finalDuration, () =>
            {
                if (!IsDead && m_logic.CurrentState == MobState.Stun)
                {
                    SetState(MobState.Idle);
                }
                m_stunTween = null;
            }).SetLink(gameObject);
        }

        /// <summary>
        /// [설명]: 이동 속도 감소(Slow) 효과를 적용합니다. (자식 클래스 구현 권장)
        /// </summary>
        public virtual void ApplySlow(float slowMultiplier, float duration)
        {
            // Override in child classes
        }

        /// <summary>
        /// [설명]: 피격 시 시각적 이펙트(셰이더, 파티클 등)를 재생합니다.
        /// </summary>
        public virtual void PlayDamageEffect(Color? color = null)
        {
            // Override in child classes
        }

        /// <summary>
        /// [설명]: 전역 쿨타임을 고려하여 피격 사운드를 재생해도 되는 상태인지 확인합니다.
        /// </summary>
        /// <returns>재생 가능 여부</returns>
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