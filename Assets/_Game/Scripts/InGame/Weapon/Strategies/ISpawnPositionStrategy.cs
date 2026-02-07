using UnityEngine;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 소환 위치를 계산하는 전략 인터페이스입니다.
    /// </summary>
    public interface ISpawnPositionStrategy
    {
        /// <summary>
        /// 주어진 카메라를 기준으로 소환 위치를 계산합니다.
        /// </summary>
        /// <param name="camera">기준이 되는 카메라 (주로 MainCamera)</param>
        /// <returns>월드 좌표계의 소환 위치</returns>
        Vector3 GetSpawnPosition(Camera camera);
    }
}
