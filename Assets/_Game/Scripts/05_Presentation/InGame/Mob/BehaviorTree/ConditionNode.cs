using System;
using Cysharp.Threading.Tasks;

namespace InGame.Mob.BehaviorTree
{
    /// <summary>
    /// [설명]: 특정 조건을 검사하여 성공(Success) 또는 실패(Failure)를 반환하는 리프(Leaf) 노드입니다.
    /// 주로 Selector나 Sequence의 흐름을 제어하는 분기점 역할을 합니다.
    /// </summary>
    public class ConditionNode : INode
    {
        #region 내부 필드

        /// <summary> 조건을 검사할 델리게이트 </summary>
        private readonly Func<bool> m_condition;

        #endregion

        #region 생성자

        /// <summary>
        /// [설명]: 조건 검사 노드를 생성합니다.
        /// </summary>
        /// <param name="condition">true/false를 반환하는 조건 함수</param>
        public ConditionNode(Func<bool> condition)
        {
            m_condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        #endregion

        #region 인터페이스 구현

        /// <summary>
        /// [설명]: 조건을 평가하고 결과를 반환합니다.
        /// </summary>
        public UniTask<NodeStatus> Evaluate()
        {
            // 조건이 참이면 Success, 거짓이면 Failure 반환
            bool result = m_condition.Invoke();

            return UniTask.FromResult(result ? NodeStatus.Success : NodeStatus.Failure);
        }

        #endregion
    }
}