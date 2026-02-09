using System;
using Cysharp.Threading.Tasks;

namespace InGame.Mob.BehaviorTree
{
    /// <summary>
    /// 실제 행동을 수행하는 말단(Leaf) 노드입니다.
    /// </summary>
    public class ActionNode : INode
    {
        private readonly Func<UniTask<NodeStatus>> m_action;
        private readonly Func<NodeStatus> m_syncAction;

        /// <summary>
        /// 비동기 액션을 수행하는 노드를 생성합니다.
        /// </summary>
        public ActionNode(Func<UniTask<NodeStatus>> action)
        {
            m_action = action;
        }

        /// <summary>
        /// 동기 액션을 수행하는 노드를 생성합니다. (자동으로 Task로 래핑됨)
        /// </summary>
        public ActionNode(Func<NodeStatus> action)
        {
            m_syncAction = action;
        }

        public async UniTask<NodeStatus> Evaluate()
        {
            if (m_action != null)
            {
                return await m_action.Invoke();
            }
            
            if (m_syncAction != null)
            {
                return m_syncAction.Invoke();
            }

            return NodeStatus.Failure;
        }
    }
}
