using UnityEngine;

namespace InGame.Mob.Systems
{
    /// <summary>
    /// 몬스터의 이동 알고리즘을 추상화하는 전략 인터페이스입니다.
    /// </summary>
    public interface IMovementStrategy
    {
        /// <summary>
        /// 현재 상태와 목적지를 기반으로 다음 프레임의 위치를 계산합니다.
        /// </summary>
        /// <param name="currentPos">현재 위치</param>
        /// <param name="targetPos">목표 위치</param>
        /// <param name="speed">이동 속도</param>
        /// <param name="deltaTime">프레임 경과 시간</param>
        /// <returns>계산된 다음 위치</returns>
        Vector3 CalculateNextPosition(Vector3 currentPos, Vector3 targetPos, float speed, float deltaTime);
    }
}
