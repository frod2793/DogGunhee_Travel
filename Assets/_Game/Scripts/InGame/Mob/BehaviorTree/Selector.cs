using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace InGame.Mob.BehaviorTree
{
    /// <summary>
    /// [설명]: 자식 노드들을 순차적으로 실행하며, 그 중 하나라도 성공(Success)하면 즉시 Success를 반환하는 분기 노드입니다.
    /// (OR Logic: A가 안되면 B, B가 안되면 C...)
    /// </summary>
    public class Selector : INode
    {
        #region 내부 필드

        /// <summary> 자식 노드 리스트 </summary>
        private readonly List<INode> m_children = new List<INode>();

        #endregion

        #region 자식 노드 관리

        /// <summary>
        /// [설명]: 자식 노드를 추가합니다. 체이닝(Chaining)을 지원합니다.
        /// </summary>
        /// <param name="node">추가할 행동 트리 노드</param>
        /// <returns>Selector 인스턴스 자신</returns>
        public Selector Add(INode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node), "Selector에 추가하려는 노드가 null입니다.");
            }

            m_children.Add(node);
            return this;
        }

        #endregion

        #region 인터페이스 구현

        /// <summary>
        /// [설명]: 자식들을 앞에서부터 순서대로 평가합니다.
        /// - Success: 즉시 종료하고 Success 반환
        /// - Running: 즉시 종료하고 Running 반환
        /// - Failure: 다음 자식으로 넘어감
        /// - 모든 자식이 Failure라면 Failure 반환
        /// </summary>
        public async UniTask<NodeStatus> Evaluate()
        {
            foreach (var node in m_children)
            {
                var status = await node.Evaluate();

                switch (status)
                {
                    case NodeStatus.Success:
                    {
                        return NodeStatus.Success; // 하나라도 성공하면 전체 성공
                    }

                    case NodeStatus.Running:
                    {
                        return NodeStatus.Running; // 실행 중이면 상태 유지
                    }

                    case NodeStatus.Failure:
                    {
                        continue; // 실패하면 다음 후보(대안) 실행
                    }
                }
            }

            // 모든 자식이 실패한 경우
            return NodeStatus.Failure;
        }

        #endregion
    }
}