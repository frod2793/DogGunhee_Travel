using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace InGame.Mob.BehaviorTree
{
    /// <summary>
    /// 자식 노드 중 하나라도 성공하면 Success를 반환하는 분기 노드입니다. (OR Logic)
    /// </summary>
    public class Selector : INode
    {
        private readonly List<INode> m_children = new List<INode>();

        public Selector Add(INode node)
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
                    case NodeStatus.Success:
                        return NodeStatus.Success;
                    case NodeStatus.Running:
                        return NodeStatus.Running;
                    case NodeStatus.Failure:
                        continue;
                }
            }

            return NodeStatus.Failure;
        }
    }
}
