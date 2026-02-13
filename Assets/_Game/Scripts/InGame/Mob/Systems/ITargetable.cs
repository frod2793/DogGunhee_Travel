using UnityEngine;

namespace InGame.Mob.Systems
{
    /// <summary>
    /// 공격 타겟이 될 수 있는 객체들의 공통 인터페이스입니다.
    /// <br/> 플레이어의 자동 공격 시스템 및 무기 시스템에서 타겟을 탐색할 때 사용됩니다.
    /// </summary>
    public interface ITargetable
    {
        #region 데이터 접근자 (Accessors)

        /// <summary>
        /// 타겟의 현재 월드 좌표
        /// </summary>
        Vector3 Position { get; }

        /// <summary>
        /// 타겟의 게임 오브젝트 트랜스폼
        /// </summary>
        Transform Transform { get; }

        /// <summary>
        /// 타겟이 현재 활성화 상태인지 여부 (풀링된 객체 여부 확인)
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// 타겟이 사망 상태인지 여부
        /// </summary>
        bool IsDead { get; }

        #endregion
    }
}
