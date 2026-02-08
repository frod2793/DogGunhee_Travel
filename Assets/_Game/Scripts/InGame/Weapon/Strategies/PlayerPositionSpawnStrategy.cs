using UnityEngine;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 플레이어(Owner)의 현재 위치에 소환하는 전략입니다.
    /// 장판형 무기(BlackWater 등)에 사용됩니다.
    /// </summary>
    public class PlayerPositionSpawnStrategy : ISpawnPositionStrategy
    {
        public Vector3 GetSpawnPosition(Transform owner, Camera camera = null)
        {
            if (owner != null)
            {
                return owner.position;
            }
            return Vector3.zero;
        }
    }
}
