using UnityEngine;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// [설명]: 무기나 소환수의 스폰 위치를 결정하는 전략 인터페이스입니다.
    /// 전략 패턴을 통해 다양한 스폰 방식(플레이어 기준, 화면 랜덤, 특정 지점 등)을 캡슐화합니다.
    /// </summary>
    public interface ISpawnPositionStrategy
    {
        /// <summary>
        /// 소유자(Owner) 정보를 바탕으로 최종 스폰 위치를 계산하여 반환합니다.
        /// </summary>
        /// <param name="owner">무기 소유자의 Transform</param>
        /// <returns>계산된 월드 좌표 (Vector3)</returns>
        Vector3 GetSpawnPosition(Transform owner);
    }

    /// <summary>
    /// [설명]: 플레이어(소유자)의 현재 위치를 그대로 스폰 위치로 사용하는 기본 전략입니다.
    /// </summary>
    public class PlayerPositionSpawnStrategy : ISpawnPositionStrategy
    {
        #region 인터페이스 구현

        public Vector3 GetSpawnPosition(Transform owner)
        {
            // 소유자가 존재하면 해당 위치, 아니면 (0,0,0) 반환
            return owner != null ? owner.position : Vector3.zero;
        }

        #endregion
    }
}