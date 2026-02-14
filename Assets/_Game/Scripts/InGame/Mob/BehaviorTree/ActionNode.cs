using System;
using Cysharp.Threading.Tasks;

namespace InGame.Mob.BehaviorTree
{
    /// <summary>
    /// [설명]: 실제 행동(이동, 공격, 대기 등)을 수행하는 말단(Leaf) 노드입니다.
    /// 동기(Func) 및 비동기(UniTask) 델리게이트를 모두 지원하며, 실행 결과를 반환합니다.
    /// </summary>
    public class ActionNode : INode
    {
        #region 내부 필드

        /// <summary> 비동기 액션 델리게이트 </summary>
        private readonly Func<UniTask<NodeStatus>> m_asyncAction;

        /// <summary> 동기 액션 델리게이트 </summary>
        private readonly Func<NodeStatus> m_syncAction;

        #endregion

        #region 생성자

        /// <summary>
        /// [설명]: 비동기(UniTask) 액션을 수행하는 노드를 생성합니다.
        /// </summary>
        /// <param name="action">실행할 비동기 함수</param>
        public ActionNode(Func<UniTask<NodeStatus>> action)
        {
            m_asyncAction = action ?? throw new ArgumentNullException(nameof(action));
        }

        /// <summary>
        /// [설명]: 동기 액션을 수행하는 노드를 생성합니다.
        /// </summary>
        /// <param name="action">실행할 동기 함수</param>
        public ActionNode(Func<NodeStatus> action)
        {
            m_syncAction = action ?? throw new ArgumentNullException(nameof(action));
        }

        #endregion

        #region 인터페이스 구현

        /// <summary>
        /// [설명]: 할당된 액션을 순차적으로 실행하고 결과를 반환합니다.
        /// </summary>
        public async UniTask<NodeStatus> Evaluate()
        {
            // 1. 비동기 액션이 할당된 경우
            if (m_asyncAction != null)
            {
                return await m_asyncAction.Invoke();
            }

            // 2. 동기 액션이 할당된 경우
            if (m_syncAction != null)
            {
                // UniTask는 구조체 기반이므로 동기 반환값을 오버헤드 없이 처리 가능합니다.
                return m_syncAction.Invoke();
            }

            // 3. 예외 상황 (생성자 방어 코드로 인해 도달하지 않음)
            return NodeStatus.Failure;
        }

        #endregion
    }
}