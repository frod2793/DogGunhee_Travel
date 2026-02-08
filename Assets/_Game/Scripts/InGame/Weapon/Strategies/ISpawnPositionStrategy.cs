using UnityEngine;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 소환 위치를 결정하는 전략 인터페이스입니다.
    /// </summary>
    public interface ISpawnPositionStrategy
    {
        /// <summary>
        /// 소환할 위치를 계산하여 반환합니다.
        /// </summary>
        /// <param name="owner">발사 주체 (플레이어)</param>
        /// <param name="camera">메인 카메라 (필요 시 사용)</param>
        /// <returns>월드 좌표 상의 소환 위치</returns>
        Vector3 GetSpawnPosition(Transform owner, Camera camera = null);
    }
}
