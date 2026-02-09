using System;
using Cysharp.Threading.Tasks;

namespace InGame.Mob.BehaviorTree
{
    /// <summary>
    /// 조건을 검사하여 Success/Failure를 반환하는 말단(Leaf) 노드입니다.
    /// </summary>
    public class ConditionNode : INode
    {
        private readonly Func<bool> m_condition;

        public ConditionNode(Func<bool> condition)
        {
            m_condition = condition;
        }

        public UniTask<NodeStatus> Evaluate()
        {
            if (m_condition != null && m_condition.Invoke())
            {
                return UniTask.FromResult(NodeStatus.Success);
            }
            
            return UniTask.FromResult(NodeStatus.Failure);
        }
    }
}
