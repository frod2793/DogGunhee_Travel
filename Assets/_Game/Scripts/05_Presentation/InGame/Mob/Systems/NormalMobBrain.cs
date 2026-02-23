using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.Mob.BehaviorTree;
using InGame.Mob.Data;

namespace InGame.Mob.Systems
{
    /// <summary>
    /// [설명]: 일반 몬스터(근접 추적형)의 AI 브레인을 구현한 클래스입니다.
    /// 비헤이비어 트리를 사용하여 플레이어 탐지 시 추적, 평시에는 랜덤 배회 행동을 수행합니다.
    /// </summary>
    public class NormalMobBrain : MobBrain
    {
        #region AI 설정 및 상태 필드

        /// <summary> 몬스터 기본 스탯 데이터 </summary>
        private readonly MobStatsData m_statsData;

        /// <summary> 비헤이비어 트리 루트 노드 </summary>
        private INode m_btRoot;

        /// <summary> 현재 추적 중인지 여부 </summary>
        private bool m_isChasing;

        /// <summary> 현재 배회 중인지 여부 </summary>
        private bool m_isWandering;

        /// <summary> 배회 중 대기 타이머 </summary>
        private float m_wanderWaitTimer;

        /// <summary> 마지막으로 추적 목표 위치를 갱신한 시간 </summary>
        private float m_lastChaseUpdateTime;

        /// <summary> 추적 위치 갱신 주기 (부하 분산) </summary>
        private const float k_ChaseUpdateInterval = 0.2f;

        /// <summary> 플레이어 트랜스폼 참조 </summary>
        private Transform m_playerTransform;

        /// <summary> 랜덤 배회 범위를 결정하는 지형 경계 </summary>
        private Bounds m_mapBounds;

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 일반 몬스터 브레인을 초기화하고 필수 데이터를 주입받습니다.
        /// </summary>
        public NormalMobBrain(MobLogic logic, MobView view, MobStatsData statsData, Bounds mapBounds)
            : base(logic, view)
        {
            m_statsData = statsData;
            m_mapBounds = mapBounds;
        }

        #endregion

        #region 초기화 및 실행 로직

        /// <summary>
        /// [설명]: 비헤이비어 트리(BT)의 구조를 정의합니다.
        /// (플레이어 발견 시 추적 로직을 우선하며, 실패 시 배회 로직을 실행합니다.)
        /// </summary>
        public override void Initialize()
        {
            m_logic.OnMovementBlocked += HandleMovementBlocked;

            // Behavior Tree 구성
            m_btRoot = new Selector()
                .Add(new BehaviorTree.Sequence()
                    .Add(new ConditionNode(CheckPlayerDetected)) // 조건: 플레이어 발견?
                    .Add(new ActionNode(ChasePlayerAsync)))      // 행동: 추적
                .Add(new ActionNode(WanderAsync));               // 행동: 배회 (기본)
        }

        /// <summary>
        /// [설명]: 매 틱마다 비헤이비어 트리를 평가하여 행동을 결정합니다.
        /// </summary>
        public override async UniTask EvaluateAsync()
        {
            if (m_btRoot == null)
            {
                return;
            }
            await m_btRoot.Evaluate();
        }

        /// <summary>
        /// [설명]: AI가 추적할 타겟 플레이어의 트랜스폼을 설정합니다.
        /// </summary>
        public void SetPlayerTransform(Transform playerTransform)
        {
            m_playerTransform = playerTransform;
        }

        #endregion

        #region BT 조건 및 행동 구현 (Node Methods)

        /// <summary>
        /// [설명]: [BT 조건] 감지 범위 내에 플레이어가 있는지 확인합니다.
        /// </summary>
        private bool CheckPlayerDetected()
        {
            if (m_playerTransform == null)
            {
                return false;
            }

            float distSqr = (m_logic.Position - m_playerTransform.position).sqrMagnitude;
            return distSqr <= (m_statsData.SearchRange * m_statsData.SearchRange);
        }

        /// <summary>
        /// [설명]: [BT 행동] 플레이어를 추적하여 목표 지점을 갱신합니다.
        /// </summary>
        private UniTask<NodeStatus> ChasePlayerAsync()
        {
            if (!m_isChasing)
            {
                m_isChasing = true;
                m_isWandering = false;
                m_logic.SetState(MobBase.MobBase.MobState.Move);
            }
            // [BugFix] 경직 종료 등으로 인해 상태가 Idle로 풀린 경우 이동 상태로 복구
            else if (m_logic.CurrentState == MobBase.MobBase.MobState.Idle)
            {
                m_logic.SetState(MobBase.MobBase.MobState.Move);
            }

            if (Time.time < m_lastChaseUpdateTime + k_ChaseUpdateInterval)
            {
                return UniTask.FromResult(NodeStatus.Running);
            }
            m_lastChaseUpdateTime = Time.time;

            if (m_playerTransform != null)
            {
                m_logic.SetTargetPosition(m_playerTransform.position);
            }

            return UniTask.FromResult(NodeStatus.Running);
        }

        /// <summary>
        /// [설명]: [BT 행동] 맵 내 임의의 지점을 배회합니다.
        /// </summary>
        private UniTask<NodeStatus> WanderAsync()
        {
            // 추적 -> 배회 전환 시 초기화
            if (m_isChasing)
            {
                m_isChasing = false;
                m_isWandering = false;
                m_logic.SetState(MobBase.MobBase.MobState.Idle);
            }

            if (m_isWandering)
            {
                // [Refine]: 현재 위치가 맵 밖인데 목적지가 맵 밖이면 즉시 재탐색 (복귀 유도)
                if (!m_logic.IsInside(m_logic.Position) && !m_logic.IsInside(m_logic.TargetPosition))
                {
                    m_isWandering = false; 
                }
                else if (m_logic.CurrentState == MobBase.MobBase.MobState.Move)
                {
                    if (m_logic.HasReachedTarget())
                    {
                        m_logic.SetState(MobBase.MobBase.MobState.Idle);
                        m_wanderWaitTimer = UnityEngine.Random.Range(m_statsData.WanderWaitRange.x, m_statsData.WanderWaitRange.y);
                    }
                }
                else if (m_logic.CurrentState == MobBase.MobBase.MobState.Idle)
                {
                    m_wanderWaitTimer -= 0.1f; // AI Loop 틱 기준 대략적 보정
                    if (m_wanderWaitTimer <= 0)
                    {
                        m_isWandering = false;
                    }
                }
                else if (m_logic.CurrentState == MobBase.MobBase.MobState.Idle && m_wanderWaitTimer <= 0)
                {
                    m_logic.SetState(MobBase.MobBase.MobState.Move);
                }

                return UniTask.FromResult(NodeStatus.Running);
            }

            // [Refine]: 새로운 목적지 설정 전 현재 위치가 맵 밖인지 체크
            Vector3 dest;
            if (!m_logic.IsInside(m_logic.Position))
            {
                // 맵 밖이라면 가장 가까운 맵 내부 지점으로 복귀 시도
                dest = m_mapBounds.ClosestPoint(m_logic.Position);
                dest.z = 0;
            }
            else
            {
                // 맵 안이라면 일반적인 랜덤 배회 목적지 산출 (선 검증 포함)
                dest = GetRandomPositionInMap();
                int retryCount = 0;
                while (!m_logic.IsInside(dest) && retryCount < 5)
                {
                    dest = GetRandomPositionInMap();
                    retryCount++;
                }
            }

            m_logic.SetTargetPosition(dest);
            m_logic.SetState(MobBase.MobBase.MobState.Move);
            m_isWandering = true;

            return UniTask.FromResult(NodeStatus.Running);
        }

        /// <summary>
        /// [설명]: 이동이 차단되었을 때(경계 충돌 등) 배회 중이라면 즉시 새로운 목적지를 찾습니다.
        /// </summary>
        private void HandleMovementBlocked()
        {
            if (m_isWandering)
            {
                m_isWandering = false; // Evaluate 시 다시 배회 로직을 타면서 새로운 목적지 설정 유도
            }
        }

        public override void OnDisable()
        {
            base.OnDisable();
            if (m_logic != null)
            {
                m_logic.OnMovementBlocked -= HandleMovementBlocked;
            }
        }

        /// <summary>
        /// [설명]: 맵 경계 데이터를 갱신하여 배회 타겟팅에 반영합니다.
        /// </summary>
        public override void UpdateMapBounds(Bounds bounds)
        {
            m_mapBounds = bounds;
        }

        /// <summary>
        /// [설명]: 지형 경계 내에서 유효한 임의의 위치를 산출합니다.
        /// </summary>
        private Vector3 GetRandomPositionInMap()
        {
            float padding = 1.0f; // 맵 가장자리 여유 공간
            
            // 맵 크기가 패딩보다 작을 경우를 대비한 방어 코드
            float halfWidth = m_mapBounds.extents.x;
            float halfHeight = m_mapBounds.extents.y;
            padding = Mathf.Min(padding, halfWidth * 0.5f, halfHeight * 0.5f);

            float x = UnityEngine.Random.Range(m_mapBounds.min.x + padding, m_mapBounds.max.x - padding);
            float y = UnityEngine.Random.Range(m_mapBounds.min.y + padding, m_mapBounds.max.y - padding);
            return new Vector3(x, y, 0);
        }

        #endregion
    }
}
