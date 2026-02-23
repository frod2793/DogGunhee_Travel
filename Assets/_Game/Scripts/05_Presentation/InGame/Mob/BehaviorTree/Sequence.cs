using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace InGame.Mob.BehaviorTree
{
    /// <summary>
    /// [설명]: 자식 노드들을 순차적으로 실행하며, 모든 자식이 성공(Success)해야만 최종적으로 Success를 반환하는 노드입니다.
    /// (AND Logic: A를 하고, 성공하면 B를 하고, 성공하면 C를 한다...)
    /// 중간에 하나라도 실패(Failure)하면 즉시 중단하고 Failure를 반환합니다.
    /// </summary>
    public class Sequence : INode
    {
        #region 내부 필드

        /// <summary> 실행할 자식 노드 리스트 </summary>
        private readonly List<INode> m_children = new List<INode>();

        #endregion

        #region 자식 노드 관리

        /// <summary>
        /// [설명]: 자식 노드를 순서대로 추가합니다. 체이닝(Chaining)을 지원합니다.
        /// </summary>
        /// <param name="node">추가할 행동 트리 노드</param>
        /// <returns>Sequence 인스턴스 자신</returns>
        public Sequence Add(INode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node), "Sequence에 추가하려는 노드가 null입니다.");
            }

            m_children.Add(node);
            return this;
        }

        #endregion

        #region 인터페이스 구현

        /// <summary>
        /// [설명]: 자식들을 앞에서부터 순서대로 평가합니다.
        /// - Failure: 즉시 중단하고 Failure 반환 (단락 평가)
        /// - Running: 상태 유지를 위해 Running 반환
        /// - Success: 다음 자식으로 진행
        /// - 모든 자식이 Success라면 최종 Success 반환
        /// </summary>
        public async UniTask<NodeStatus> Evaluate()
        {
            foreach (var node in m_children)
            {
                var status = await node.Evaluate();

                switch (status)
                {
                    case NodeStatus.Failure:
                    {
                        return NodeStatus.Failure; // 하나라도 실패하면 전체 실패
                    }

                    case NodeStatus.Running:
                    {
                        return NodeStatus.Running; // 실행 중이면 대기
                    }

                    case NodeStatus.Success:
                    {
                        continue; // 성공하면 다음 단계(Next Step)로 진행
                    }
                }
            }

            // 모든 자식이 성공적으로 완료됨
            return NodeStatus.Success;
        }

        #endregion
    }
}