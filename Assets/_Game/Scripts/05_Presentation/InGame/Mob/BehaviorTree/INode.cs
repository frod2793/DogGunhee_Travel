using Cysharp.Threading.Tasks;

namespace InGame.Mob.BehaviorTree
{
    /// <summary>
    /// [설명]: 모든 Behavior Tree 노드가 구현해야 하는 기본 인터페이스입니다.
    /// </summary>
    public interface INode
    {
        /// <summary>
        /// [설명]: 노드를 평가하고 실행 결과를 반환합니다.
        /// </summary>
        UniTask<NodeStatus> Evaluate();
    }
}
