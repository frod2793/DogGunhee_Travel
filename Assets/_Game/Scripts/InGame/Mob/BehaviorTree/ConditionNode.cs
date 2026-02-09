using System;
using Cysharp.Threading.Tasks;

namespace InGame.Mob.BehaviorTree
{
    /// <summary>
    /// 조건을 검사하여 Success/Failure를 반환하는 말단(Leaf) 노드입니다.
    /// </summary>
    public class ConditionNode : INode
    {
        #region 내부 변수

        private readonly Func<bool> m_condition;

        #endregion

        #region 생성자

        public ConditionNode(Func<bool> condition)
        {
            m_condition = condition;
        }

        #endregion

        #region 인터페이스 구현

        public UniTask<NodeStatus> Evaluate()
        {
            if (m_condition != null && m_condition.Invoke())
            {
                return UniTask.FromResult(NodeStatus.Success);
            }

            return UniTask.FromResult(NodeStatus.Failure);
        }

        #endregion
    }
}
