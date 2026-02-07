using UnityEngine;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 플레이어의 현재 위치에 이펙트를 소환하는 전략입니다.
    /// AreaAttackStrategy에서 사용됩니다.
    /// </summary>
    public class PlayerPositionSpawnStrategy : ISpawnPositionStrategy
    {
        public Vector3 GetSpawnPosition(Camera camera)
        {
            // 메인 카메라 대신 플레이어 Transform을 직접 사용하도록 수정 필요 시
            // 현재는 카메라 중심(플레이어 위치)을 반환
            if (camera != null)
            {
                return camera.transform.position;
            }
            return Vector3.zero;
        }
    }
}
