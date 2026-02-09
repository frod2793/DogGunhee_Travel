using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 무기 공격의 스폰 위치를 결정하는 전략 인터페이스입니다.
    /// </summary>
    public interface ISpawnPositionStrategy
    {
        Vector3 GetSpawnPosition(Transform owner);
    }

    /// <summary>
    /// 플레이어의 현재 위치를 스폰 위치로 사용하는 전략입니다.
    /// </summary>
    public class PlayerPositionSpawnStrategy : ISpawnPositionStrategy
    {
        public Vector3 GetSpawnPosition(Transform owner)
        {
            return owner != null ? owner.position : Vector3.zero;
        }
    }
}
