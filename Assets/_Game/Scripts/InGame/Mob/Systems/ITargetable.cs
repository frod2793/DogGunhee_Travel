using UnityEngine;

namespace InGame.Mob.Systems
{
    /// <summary>
    /// [설명]: 공격 타겟이 될 수 있는 객체들이 구현해야 하는 공통 인터페이스입니다.
    /// 플레이어의 자동 공격 시스템이나 무기 시스템에서 조준 대상을 탐색할 때 사용됩니다.
    /// </summary>
    public interface ITargetable
    {
        #region 공개 프로퍼티

        /// <summary>
        /// [설명]: 타겟의 현재 월드 좌표
        /// </summary>
        Vector3 Position { get; }

        /// <summary>
        /// [설명]: 타겟의 트랜스폼 참조
        /// </summary>
        Transform Transform { get; }

        /// <summary>
        /// [설명]: 타겟이 현재 하이라이어키 상에서 활성화 상태인지 여부
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// [설명]: 타겟이 사망 상태인지 여부
        /// </summary>
        bool IsDead { get; }

        #endregion
    }
}
