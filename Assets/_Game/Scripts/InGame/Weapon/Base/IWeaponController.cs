using UnityEngine;

namespace InGame.Weapon.Base
{
    /// <summary>
    /// 모든 무기 컨트롤러(POCO Logic)가 구현해야 하는 인터페이스입니다.
    /// </summary>
    public interface IWeaponController
    {
        /// <summary>
        /// 무기를 초기화합니다.
        /// </summary>
        /// <param name="data">무기 정적 데이터</param>
        /// <param name="owner">무기 소유자(플레이어) Transform</param>
        /// <param name="getTargetDirection">공격 방향을 반환하는 델리게이트</param>
        void Init(WeaponDataSO data, Transform owner, System.Func<Vector3> getTargetDirection);

        /// <summary>
        /// 매 프레임 업데이트 로직을 수행합니다. (쿨타임 감소, 공격 시도 등)
        /// </summary>
        void OnUpdate(float deltaTime);

        /// <summary>
        /// 매 프레임 후반 업데이트 로직을 수행합니다. (위치 동기화 등)
        /// </summary>
        void OnLateUpdate();

        /// <summary>
        /// 무기를 레벨업시킵니다.
        /// </summary>
        void LevelUp();

        /// <summary>
        /// 무기 해제 시 정리 작업을 수행합니다.
        /// </summary>
        void Dispose();

        /// <summary>
        /// 무기를 사용하여 공격을 수행합니다.
        /// </summary>
        /// <param name="direction">공격 방향</param>
        void Attack(Vector3 direction);
    }
}
