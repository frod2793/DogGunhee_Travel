using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace InGame.Mob.BehaviorTree
{
    /// <summary>
    /// 모든 자식 노드가 성공해야 Success를 반환하는 순차 노드입니다. (AND Logic)
    /// 하나라도 실패하면 Failure를 반환합니다.
    /// </summary>
    public class Sequence : INode
    {
        private readonly List<INode> m_children = new List<INode>();

        public Sequence Add(INode node)
        {
            m_children.Add(node);
            return this;
        }

        public async UniTask<NodeStatus> Evaluate()
        {
            foreach (var node in m_children)
            {
                var status = await node.Evaluate();

                switch (status)
                {
                    case NodeStatus.Failure:
                        return NodeStatus.Failure;
                    case NodeStatus.Running:
                        return NodeStatus.Running;
                    case NodeStatus.Success:
                        continue;
                }
            }

            return NodeStatus.Success;
        }
    }
}
