using UnityEngine;

namespace InGame.Mob.Systems
{
    /// <summary>
    /// 목표 지점을 향해 선형적으로 이동하는 가장 기본적인 이동 전략입니다.
    /// </summary>
    public class LinearMovementStrategy : IMovementStrategy
    {
        public Vector3 CalculateNextPosition(Vector3 currentPos, Vector3 targetPos, float speed, float deltaTime)
        {
            float step = speed * deltaTime;
            float distance = Vector3.Distance(currentPos, targetPos);

            if (distance <= step)
            {
                return targetPos;
            }

            return Vector3.MoveTowards(currentPos, targetPos, step);
        }
    }
}
