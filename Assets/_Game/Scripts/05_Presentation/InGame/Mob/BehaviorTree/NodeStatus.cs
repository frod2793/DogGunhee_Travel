namespace InGame.Mob.BehaviorTree
{
    /// <summary>
    /// [설명]: Behavior Tree 노드의 실행 상태 및 결과를 정의하는 열거형입니다.
    /// </summary>
    public enum NodeStatus
    {
        /// <summary> [설명]: 실행 성공 </summary>
        Success,
        /// <summary> [설명]: 실행 실패 </summary>
        Failure,
        /// <summary> [설명]: 실행 중 (비동기 작업 포함) </summary>
        Running
    }
}
