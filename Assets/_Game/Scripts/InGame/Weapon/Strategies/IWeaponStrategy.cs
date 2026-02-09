using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 무기 공격 전략을 정의하는 핵심 인터페이스입니다.
    /// </summary>
    public interface IWeaponStrategy
    {
        /// <summary>
        /// 전략을 초기화합니다.
        /// </summary>
        void Initialize(WeaponDataSO data);

        /// <summary>
        /// 무기 공격을 실행합니다.
        /// </summary>
        void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction);

        /// <summary>
        /// 매 프레임 업데이트 로직을 처리합니다.
        /// </summary>
        void OnUpdate(WeaponRuntimeStats stats, float deltaTime);
    }
}
