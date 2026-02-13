using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.Mob.BehaviorTree;
using InGame.Mob.Data;

namespace InGame.Mob.Systems
{
    /// <summary>
    /// 일반 몬스터(근접 추적)의 AI를 담당하는 구체 브레인 클래스입니다.
    /// </summary>
    public class NormalMobBrain : MobBrain
    {
        #region AI 설정 및 상태
        
        private readonly MobStatsData m_statsData;
        private INode m_btRoot;
        
        private bool m_isChasing;
        private bool m_isWandering;
        private float m_wanderWaitTimer;
        private float m_lastChaseUpdateTime;
        private const float k_ChaseUpdateInterval = 0.2f;

        private Transform m_playerTransform;
        private Bounds m_mapBounds;
        
        #endregion

        public NormalMobBrain(MobLogic logic, MobView view, MobStatsData statsData, Bounds mapBounds) 
            : base(logic, view)
        {
            m_statsData = statsData;
            m_mapBounds = mapBounds;
        }

        public override void Initialize()
        {
            // Behavior Tree 구성
            m_btRoot = new Selector()
                .Add(new BehaviorTree.Sequence()
                    .Add(new ConditionNode(CheckPlayerDetected)) // 조건: 플레이어 발견?
                    .Add(new ActionNode(ChasePlayerAsync)))      // 행동: 추적
                .Add(new ActionNode(WanderAsync));               // 행동: 배회 (기본)
        }

        public override async UniTask EvaluateAsync()
        {
            if (m_btRoot == null) return;
            await m_btRoot.Evaluate();
        }

        public void SetPlayerTransform(Transform playerTransform)
        {
            m_playerTransform = playerTransform;
        }

        #region BT Conditions & Actions

        private bool CheckPlayerDetected()
        {
            if (m_playerTransform == null) return false;

            float distSqr = (m_logic.Position - m_playerTransform.position).sqrMagnitude;
            return distSqr <= (m_statsData.SearchRange * m_statsData.SearchRange);
        }

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
                if (m_logic.CurrentState == MobBase.MobBase.MobState.Move)
                {
                    if (m_logic.HasReachedTarget())
                    {
                        m_logic.SetState(MobBase.MobBase.MobState.Idle);
                        m_wanderWaitTimer = UnityEngine.Random.Range(m_statsData.WanderWaitRange.x, m_statsData.WanderWaitRange.y);
                    }
                }
                else if (m_logic.CurrentState == MobBase.MobBase.MobState.Idle)
                {
                    m_wanderWaitTimer -= 0.1f; // AI Loop 틱 기준
                    if (m_wanderWaitTimer <= 0)
                    {
                        m_isWandering = false;
                    }
                }
                // [BugFix] 배회 중 위치 이동 단계인데 경직 종료 등으로 Idle이 된 경우 복구
                else if (m_logic.CurrentState == MobBase.MobBase.MobState.Idle && m_wanderWaitTimer <= 0)
                {
                    m_logic.SetState(MobBase.MobBase.MobState.Move);
                }

                return UniTask.FromResult(NodeStatus.Running);
            }

            // 새로운 배회 목표 설정
            Vector3 dest = GetRandomPositionInMap();
            m_logic.SetTargetPosition(dest);
            m_logic.SetState(MobBase.MobBase.MobState.Move);
            m_isWandering = true;

            return UniTask.FromResult(NodeStatus.Running);
        }

        private Vector3 GetRandomPositionInMap()
        {
            float x = UnityEngine.Random.Range(m_mapBounds.min.x, m_mapBounds.max.x);
            float y = UnityEngine.Random.Range(m_mapBounds.min.y, m_mapBounds.max.y);
            return new Vector3(x, y, 0);
        }

        #endregion
    }
}
