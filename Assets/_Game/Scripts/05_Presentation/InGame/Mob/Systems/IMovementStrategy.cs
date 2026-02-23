using UnityEngine;

namespace InGame.Mob.Systems
{
    /// <summary>
    /// [설명]: 몬스터의 이동 알고리즘을 추상화하는 전략 패턴 인터페이스입니다.
    /// </summary>
    public interface IMovementStrategy
    {
        /// <summary>
        /// [설명]: 현재 위치에서 목표 지점을 향해 이동할 다음 프레임의 위치를 계산합니다.
        /// </summary>
        /// <param name="currentPos">현재 월드 위치</param>
        /// <param name="targetPos">이동하려는 목표 위치</param>
        /// <param name="speed">이동 속도</param>
        /// <param name="deltaTime">직전 프레임과의 시간 간격</param>
        /// <returns>계산된 다음 월드 위치</returns>
        Vector3 CalculateNextPosition(Vector3 currentPos, Vector3 targetPos, float speed, float deltaTime);
    }
}
