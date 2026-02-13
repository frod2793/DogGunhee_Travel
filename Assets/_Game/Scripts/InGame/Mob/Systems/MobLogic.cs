using System;
using UnityEngine;
using InGame.Mob.MobBase;

namespace InGame.Mob.Systems
{
    /// <summary>
    /// 몬스터의 인게임 비즈니스 로직을 담당하는 순수 C# 클래스입니다.
    /// <br/> MonoBehaviour 의존성 없이 스탯, 상태, 위치 계산을 처리합니다.
    /// </summary>
    public class MobLogic
    {
        #region 1. 필드 및 프라이빗 상태

        private MobStats m_stats;
        private float m_maxHp;
        private MobBase.MobBase.MobState m_currentState;
        private Vector3 m_position;
        private IMovementStrategy m_movementStrategy;

        // 타겟 정보
        private Vector3 m_targetPosition;

        #endregion

        #region 2. 이벤트 (Events - View 알림용)

        public event Action<MobBase.MobBase.MobState> OnStateChanged;
        public event Action<Vector3> OnPositionUpdated;
        public event Action<float, float> OnHpChanged; // Current, Max
        public event Action OnDie;

        #endregion

        #region 3. 생성자 및 초기화

        public MobLogic(MobStats stats, Vector3 startPos, IMovementStrategy strategy)
        {
            InitializeStats(stats);
            m_position = startPos;
            m_movementStrategy = strategy;
            m_currentState = MobBase.MobBase.MobState.Idle;
        }

        public void InitializeStats(MobStats stats)
        {
            m_stats = stats;
            m_maxHp = stats.Hp; // 시작 시 HP를 MaxHp로 간주
            OnHpChanged?.Invoke(m_stats.Hp, m_maxHp);
        }

        /// <summary>
        /// 이동 전략을 교체합니다. (추적 <=> 배회 전환 시 사용)
        /// </summary>
        public void SetMovementStrategy(IMovementStrategy strategy)
        {
            m_movementStrategy = strategy;
        }

        /// <summary>
        /// 외부(View/Spawner)에서 강제로 위치를 동기화합니다.
        /// </summary>
        public void SyncPosition(Vector3 newPos)
        {
            m_position = newPos;
            m_targetPosition = newPos; // 목표 지점도 현재 위치로 초기화하여 급발진 방지
            OnPositionUpdated?.Invoke(m_position);
        }

        #endregion

        #region 4. 공개 프로퍼티 (Accessors)

        public float CurrentHp => m_stats.Hp;
        public float MaxHp => m_maxHp;
        public float MoveSpeed => m_stats.MoveSpeed;
        public float AttackDamage => m_stats.AttackDamage;
        public float AttackSpeed => m_stats.AttackSpeed;
        public float AttackRange => m_stats.AttackRange;
        public float StunResistance => m_stats.StunResistance;

        public MobBase.MobBase.MobState CurrentState => m_currentState;
        public Vector3 Position => m_position;
        public Vector3 TargetPosition => m_targetPosition;

        public bool HasReachedTarget(float stopDistance = 0.1f)
        {
            return Vector3.Distance(m_position, m_targetPosition) <= stopDistance;
        }

        #endregion

        #region 5. 핵심 로직 (Logic Processing)

        /// <summary>
        /// 매 프레임 로직 업데이트를 수행합니다. (Controller에서 호출)
        /// </summary>
        public void Update(float deltaTime)
        {
            if (m_currentState == MobBase.MobBase.MobState.Die) return;
            if (m_currentState == MobBase.MobBase.MobState.Stun) return;

            // 이동 상태일 때만 위치 계산
            if (m_currentState == MobBase.MobBase.MobState.Move)
            {
                UpdateMovement(deltaTime);
            }
        }

        /// <summary>
        /// 목표 지점을 설정합니다.
        /// </summary>
        public void SetTargetPosition(Vector3 targetPos)
        {
            m_targetPosition = targetPos;
        }

        /// <summary>
        /// 상태를 변경하고 이벤트를 발생시킵니다.
        /// </summary>
        public void SetState(MobBase.MobBase.MobState newState)
        {
            if (m_currentState == newState) return;

            m_currentState = newState;
            OnStateChanged?.Invoke(newState);

            if (newState == MobBase.MobBase.MobState.Die)
            {
                OnDie?.Invoke();
            }
        }

        /// <summary>
        /// 데미지를 입고 상태를 생존 여부에 따라 갱신합니다.
        /// </summary>
        public void TakeDamage(float damage, float stunTime = 0f)
        {
            if (m_currentState == MobBase.MobBase.MobState.Die) return;

            m_stats.Hp -= damage;
            OnHpChanged?.Invoke(m_stats.Hp, m_maxHp);

            if (m_stats.Hp <= 0)
            {
                SetState(MobBase.MobBase.MobState.Die);
            }
            else if (stunTime > 0)
            {
                SetState(MobBase.MobBase.MobState.Stun);
            }
        }

        #endregion

        #region 6. 내부 도우미 로직

        private void UpdateMovement(float deltaTime)
        {
            if (m_movementStrategy == null) return;

            Vector3 nextPos = m_movementStrategy.CalculateNextPosition(m_position, m_targetPosition, m_stats.MoveSpeed, deltaTime);
            
            if (m_position != nextPos)
            {
                m_position = nextPos;
                OnPositionUpdated?.Invoke(m_position);
            }

            // 목표 도달 시 Idle 전환 (필요할 경우)
            if (Vector3.Distance(m_position, m_targetPosition) < 0.05f)
            {
                // 여기서는 리더(Controller)가 상태를 제어할 수 있도록 유연하게 두거나, 
                // 특정 거리 이하에서 정지 로직 추가
            }
        }

        #endregion
    }
}
