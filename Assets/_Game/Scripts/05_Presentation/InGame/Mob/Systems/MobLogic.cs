using System;
using UnityEngine;
using InGame.Mob.MobBase;

namespace InGame.Mob.Systems
{
    /// <summary>
    /// [설명]: 몬스터의 인게임 비즈니스 로직을 담당하는 순수 C# 클래스입니다.
    /// MonoBehaviour 의존성 없이 스탯, 상태, 위치 계산을 처리합니다.
    /// </summary>
    public class MobLogic
    {
        #region 내부 필드

        /// <summary> 공격/방어 등 핵심 스탯 데이터 </summary>
        private MobStats m_stats;

        /// <summary> 최대 체력 </summary>
        private float m_maxHp;

        /// <summary> 현재 몬스터의 동작 상태 </summary>
        private MobBase.MobBase.MobState m_currentState;

        /// <summary> 현재 월드 위치 </summary>
        private Vector3 m_position;

        /// <summary> 이동 계산 알고리즘 전략 </summary>
        private IMovementStrategy m_movementStrategy;

        /// <summary> 이동 목표 지점 </summary>
        private Vector3 m_targetPosition;

        /// <summary> 이동 가능 범위를 제한하는 맵의 경계 데이터 </summary>
        private Bounds m_mapBounds;

        #endregion

        #region 이벤트

        /// <summary>
        /// [설명]: 상태가 변경되었을 때 발생하는 이벤트입니다.
        /// </summary>
        public event Action<MobBase.MobBase.MobState> OnStateChanged;

        /// <summary>
        /// <summary> 위치가 변경되었을 때(이동 종료 시) 발생하는 이벤트 </summary>
        public event Action<Vector3> OnPositionUpdated;

        /// <summary> 이동이 맵 경계 등에 의해 차단되었을 때 발생하는 이벤트 </summary>
        public event Action OnMovementBlocked;

        /// <summary>
        /// [설명]: 체력이 변경되었을 때 발생하는 이벤트입니다. (현재값, 최대값)
        /// </summary>
        public event Action<float, float> OnHpChanged;

        /// <summary>
        /// [설명]: 사망 시 발생하는 이벤트입니다.
        /// </summary>
        public event Action OnDie;

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 몬스터 로직 객체를 초기화합니다.
        /// </summary>
        public MobLogic(MobStats stats, Vector3 startPos, IMovementStrategy strategy, Bounds mapBounds = default)
        {
            m_mapBounds = mapBounds;
            InitializeStats(stats);
            m_position = ClampPosition(startPos);
            m_movementStrategy = strategy;
            m_currentState = MobBase.MobBase.MobState.Idle;
        }

        /// <summary>
        /// [설명]: 몬스터의 기본 스탯을 설정하거나 갱신합니다.
        /// </summary>
        public void InitializeStats(MobStats stats)
        {
            m_stats = stats;
            m_maxHp = stats.Hp; // 시작 시 HP를 MaxHp로 간주
            OnHpChanged?.Invoke(m_stats.Hp, m_maxHp);
        }

        /// <summary>
        /// [설명]: 이동 전략을 교체합니다. (추적 <=> 배회 전환 시 사용)
        /// </summary>
        public void SetMovementStrategy(IMovementStrategy strategy)
        {
            m_movementStrategy = strategy;
        }

        /// <summary>
        /// [설명]: 외부(View/Spawner)에서 강제로 위치를 동기화합니다.
        /// </summary>
        public void SyncPosition(Vector3 newPos)
        {
            m_position = ClampPosition(newPos);
            m_targetPosition = m_position; // 목표 지점도 현재 위치로 초기화하여 급발진 방지
            OnPositionUpdated?.Invoke(m_position);
        }

        /// <summary>
        /// [설명]: 맵 경계 데이터를 최신으로 갱신하고 필요 시 위치를 보정합니다.
        /// </summary>
        public void UpdateMapBounds(Bounds bounds)
        {
            m_mapBounds = bounds;
            
            // 경계가 바뀌었을 때 현재 위치가 밖이라면 즉시 안으로 보정
            if (IsOutOfBounds(m_position))
            {
                SyncPosition(m_position);
            }
        }

        #endregion

        #region 공개 프로퍼티

        /// <summary>
        /// [설명]: 현재 체력
        /// </summary>
        public float CurrentHp => m_stats.Hp;

        /// <summary>
        /// [설명]: 최대 체력
        /// </summary>
        public float MaxHp => m_maxHp;

        /// <summary>
        /// [설명]: 이동 속도
        /// </summary>
        public float MoveSpeed => m_stats.MoveSpeed;

        /// <summary>
        /// [설명]: 공격력
        /// </summary>
        public float AttackDamage => m_stats.AttackDamage;

        /// <summary>
        /// [설명]: 공격 속도
        /// </summary>
        public float AttackSpeed => m_stats.AttackSpeed;

        /// <summary>
        /// [설명]: 공격 사거리
        /// </summary>
        public float AttackRange => m_stats.AttackRange;

        /// <summary>
        /// [설명]: 경직 저항력
        /// </summary>
        public float StunResistance => m_stats.StunResistance;

        /// <summary>
        /// [설명]: 현재 동작 상태
        /// </summary>
        public MobBase.MobBase.MobState CurrentState => m_currentState;

        /// <summary>
        /// [설명]: 현재 위치
        /// </summary>
        public Vector3 Position => m_position;

        /// <summary>
        /// [설명]: 목표 위치
        /// </summary>
        public Vector3 TargetPosition => m_targetPosition;

        /// <summary>
        /// [설명]: 목표 지점 도달 여부를 확인합니다.
        /// </summary>
        public bool HasReachedTarget(float stopDistance = 0.1f)
        {
            return Vector3.Distance(m_position, m_targetPosition) <= stopDistance;
        }

        /// <summary>
        /// [설명]: 해당 위치가 맵 경계 내부인지 확인합니다. (브레인에서 목적지 선 검증용)
        /// </summary>
        public bool IsInside(Vector3 pos)
        {
            if (m_mapBounds == default || m_mapBounds.extents == Vector3.zero)
            {
                return true;
            }

            return pos.x >= m_mapBounds.min.x && pos.x <= m_mapBounds.max.x &&
                   pos.y >= m_mapBounds.min.y && pos.y <= m_mapBounds.max.y;
        }

        #endregion

        #region 핵심 비즈니스 로직

        /// <summary>
        /// [설명]: 매 프레임 로직 업데이트를 수행합니다.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (m_currentState == MobBase.MobBase.MobState.Die)
            {
                return;
            }
            if (m_currentState == MobBase.MobBase.MobState.Stun)
            {
                return;
            }

            // 이동 상태일 때만 위치 계산
            if (m_currentState == MobBase.MobBase.MobState.Move)
            {
                UpdateMovement(deltaTime);
            }
        }

        /// <summary>
        /// [설명]: 새로운 목표 지점을 설정합니다.
        /// </summary>
        public void SetTargetPosition(Vector3 targetPos)
        {
            m_targetPosition = targetPos;
        }

        /// <summary>
        /// [설명]: 상태를 변경하고 구독자들에게 알립니다.
        /// </summary>
        public void SetState(MobBase.MobBase.MobState newState)
        {
            if (m_currentState == newState)
            {
                return;
            }

            m_currentState = newState;
            OnStateChanged?.Invoke(newState);

            if (newState == MobBase.MobBase.MobState.Die)
            {
                OnDie?.Invoke();
            }
        }

        /// <summary>
        /// [설명]: 데미지를 입고 상태를 생존 여부에 따라 갱신합니다.
        /// </summary>
        public void TakeDamage(float damage, float stunTime = 0f)
        {
            if (m_currentState == MobBase.MobBase.MobState.Die)
            {
                return;
            }

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

        #region 내부 도우미 메서드

        /// <summary>
        /// [설명]: 전략 패턴에 따른 실제 이동량 계산 및 위치 업데이트를 처리합니다.
        /// </summary>
        private void UpdateMovement(float deltaTime)
        {
            if (m_movementStrategy == null)
            {
                return;
            }

            Vector3 nextPos = m_movementStrategy.CalculateNextPosition(m_position, m_targetPosition, m_stats.MoveSpeed, deltaTime);
            
            // [Refine]: 이동 가능 여부 판단 (복귀 방향은 허용, 이탈 방향은 차단)
            if (IsMovementBlocked(m_position, nextPos))
            {
                OnMovementBlocked?.Invoke();
                return;
            }

            if (m_position != nextPos)
            {
                m_position = nextPos;
                OnPositionUpdated?.Invoke(m_position);
            }
        }

        /// <summary>
        /// [설명]: 이동이 차단되어야 하는지 판단합니다. 
        /// 맵 밖에서 맵 안으로(또는 가까워지는 방향으로) 들어오는 이동은 허용합니다.
        /// </summary>
        private bool IsMovementBlocked(Vector3 current, Vector3 next)
        {
            if (m_mapBounds == default || m_mapBounds.extents == Vector3.zero)
            {
                return false;
            }

            bool currentInside = IsInside(current);
            bool nextInside = IsInside(next);

            // 1. 이미 안에 있고 다음 위치도 안이면 통과
            if (currentInside && nextInside) return false;

            // 2. 안에 있는데 밖으로 나가려고 하면 차단
            if (currentInside && !nextInside) return true;

            // 3. 이미 밖인 경우: 맵의 중심(또는 가장 가까운 점)에 더 가까워지는 방향이면 허용
            float currentDist = Vector3.SqrMagnitude(current - m_mapBounds.center);
            float nextDist = Vector3.SqrMagnitude(next - m_mapBounds.center);

            return nextDist >= currentDist; // 더 멀어지거나 거리가 같으면 차단
        }

        /// <summary>
        /// [설명]: 해당 좌표가 맵 경계 밖인지 확인합니다. (기존 로직 유지/호환용)
        /// </summary>
        public bool IsOutOfBounds(Vector3 pos)
        {
            return !IsInside(pos);
        }

        /// <summary>
        /// [설명]: 좌표를 맵 경계 내로 제한합니다.
        /// </summary>
        private Vector3 ClampPosition(Vector3 pos)
        {
            if (m_mapBounds == default || m_mapBounds.extents == Vector3.zero)
            {
                return pos;
            }

            float clampedX = Mathf.Clamp(pos.x, m_mapBounds.min.x, m_mapBounds.max.x);
            float clampedY = Mathf.Clamp(pos.y, m_mapBounds.min.y, m_mapBounds.max.y);

            return new Vector3(clampedX, clampedY, pos.z);
        }

        #endregion
    }
}
