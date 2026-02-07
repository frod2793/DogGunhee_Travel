using UnityEngine;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 무기의 구체적인 공격 동작을 정의하는 전략 인터페이스입니다.
    /// Strategy Pattern이 적용되었습니다.
    /// </summary>
    public interface IWeaponStrategy
    {
        /// <summary>
        /// 전략을 초기화합니다. (오브젝트 풀 등록 등 1회성 작업)
        /// Factory에서 생성 직후 호출됩니다.
        /// </summary>
        void Initialize(Weapon.Base.WeaponDataSO data);

        /// <summary>
        /// 무기가 공격을 수행할 때 호출됩니다.
        /// </summary>
        /// <param name="runtimeStats">현재 무기 스탯</param>
        /// <param name="owner">무기 소유자</param>
        /// <param name="direction">공격 방향</param>
        void Attack(Weapon.Base.WeaponRuntimeStats runtimeStats, Transform owner, Vector3 direction);

        /// <summary>
        /// 매 프레임 업데이트가 필요할 때 호출됩니다 (지속 데미지 등).
        /// </summary>
        void OnUpdate(Weapon.Base.WeaponRuntimeStats runtimeStats, float deltaTime);
    }
}
