namespace InGame.Mob.BehaviorTree
{
    /// <summary>
    /// BT 노드의 실행 결과를 나타냅니다.
    /// </summary>
    public enum NodeStatus
    {
        /// <summary>실행 성공</summary>
        Success,
        /// <summary>실행 실패</summary>
        Failure,
        /// <summary>실행 중 (비동기 작업 등)</summary>
        Running
    }
}
