using UnityEngine;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 모든 몬스터의 최상위 기본 클래스입니다.
    /// 공통 스탯, 상태 머신(FSM), 이벤트 핸들링을 정의합니다.
    /// </summary>
    public abstract class VamserMobBase : MonoBehaviour, IObjectPoolUser
    {
        #region 프로퍼티 (공통 스탯)

        // 인터페이스 구현
        public ObjectPoolSpawner ObjectPoolSpawner { get; set; }

        // [최적화] 명명 규칙 통일 (PascalCase, Mob_ 접두사 제거)
        public float MoveSpeed { get; set; }
        public float CurrentHp { get; protected set; }
        public float AttackDamage { get; set; }
        public float AttackSpeed { get; set; }
        public float AttackRange { get; set; }
        public float StunTime { get; set; }

        public bool IsDead { get; protected set; }
        public bool IsHit { get; protected set; }
        
        /// <summary>
        /// 이동 가능 여부 (일시정지, 스턴 등에서 제어)
        /// </summary>
        public bool IsMoveEnabled { get; protected set; }

        #endregion

        #region 내부 참조 및 상태

        protected PlayerBase m_player; 
        protected Transform m_playerTransform;

        public enum MobState
        {
            Idle,
            Move,
            Stun,
            Attack,
            Die
        }

        [Tooltip("현재 몬스터의 상태 (디버깅용)")]
        [SerializeField] protected MobState m_currentState;
        public MobState CurrentState => m_currentState;

        #endregion

        #region Unity 라이프사이클

        public virtual void OnEnable()
        {
            IsDead = false;
            IsHit = false;
            IsMoveEnabled = true;
            
            // 이벤트 구독
            PlayStateManager.OnGamePause += OnPause;
            PlayStateManager.OnGameResume += OnResume;
            PlayStateManager.OnGameOver += OnGameOver;
        }

        protected virtual void OnDisable()
        {
            // 이벤트 구독 해제
            PlayStateManager.OnGamePause -= OnPause;
            PlayStateManager.OnGameResume -= OnResume;
            PlayStateManager.OnGameOver -= OnGameOver;
        }

        private void OnValidate()
        {
            // 에디터에서 상태 강제 변경 테스트용
            if (Application.isPlaying)
            {
                SetState(m_currentState);
            }
        }

        #endregion

        #region 초기화 및 타겟 설정

        /// <summary>
        /// 외부에서 플레이어(타겟)를 설정합니다.
        /// </summary>
        public virtual void SetTarget(PlayerBase target)
        {
            m_player = target;
            if (m_player != null)
            {
                // 플레이어의 실제 이동 객체(부모) 추적
                m_playerTransform = m_player.transform.parent;
            }
            else
            {
                m_playerTransform = null;
            }
        }

        #endregion

        #region 상태 관리 (FSM)

        /// <summary>
        /// 몬스터의 상태를 변경하고 해당 동작을 실행합니다.
        /// </summary>
        public void SetState(MobState state)
        {
            m_currentState = state;
            switch (state)
            {
                case MobState.Idle: OnIdle(); break;
                case MobState.Move: OnMove(); break;
                case MobState.Stun: OnStun(); break;
                case MobState.Attack: OnAttack(); break;
                case MobState.Die: OnDie(); break;
            }
        }

        // 자식 클래스에서 오버라이드할 가상 메서드들 (이름 표준화: Mob_ -> On)
        protected virtual void OnIdle() { }

        protected virtual void OnMove() 
        {
            IsMoveEnabled = true;
        }

        protected virtual void OnStun() 
        {
            IsMoveEnabled = false;
        }

        protected virtual void OnAttack() { }

        /// <summary>
        /// 사망 처리 기본 로직 (오브젝트 풀 반환 포함)
        /// </summary>
        protected virtual void OnDie()
        {
            if (!IsDead)
            {
                IsDead = true;
                IsMoveEnabled = false;

                // 킬 카운트 증가
                if (PlayerDataManagerDontdesytoy.Instance != null)
                {
                    PlayerDataManagerDontdesytoy.Instance.PlayerData.nowPlayMObkillCOunt++;
                }

                LogManager.Log($"Die : {name}", LogManager.LogCategory.mobBase);

                // 오브젝트 풀 반환
                if (ObjectPoolSpawner != null)
                {
                    ObjectPoolSpawner.MobObjectPool.Release(this);
                }
                else
                {
                    // 풀이 없으면 파괴 (안전장치)
                    Destroy(gameObject);
                }
            }
        }

        #endregion

        #region 게임 상태 이벤트 핸들러

        protected virtual void OnPause()
        {
            IsMoveEnabled = false;
        }
        
        protected virtual void OnResume()
        {
            // 죽거나 스턴 상태가 아닐 때만 이동 재개
            if (!IsDead && m_currentState != MobState.Stun)
            {
                IsMoveEnabled = true;
            }
        }

        private void OnGameOver()
        {
            IsMoveEnabled = false;
        }

        #endregion

        #region 전투 인터페이스 (공용)

        /// <summary>
        /// 데미지 적용 (자식 클래스 구현 필요)
        /// </summary>
        public virtual void TakeDamage(float damage, float stunTime = 0f) { }

        /// <summary>
        /// 슬로우 효과 적용 (자식 클래스 구현 필요)
        /// </summary>
        public virtual void ApplySlow(float slowMultiplier, float duration) { }

        #endregion
    }
}