using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 무기 공격 전략을 정의하는 핵심 인터페이스입니다.
    /// 구체적인 공격 방식(원거리, 근거리, 오라 등)을 캡슐화합니다.
    /// </summary>
    public interface IWeaponStrategy
    {
        /// <summary>
        /// 데이터 기반으로 전략을 초기화합니다.
        /// </summary>
        void Init(WeaponDataSO data);

        /// <summary>
        /// 무기 공격을 실제 실행합니다.
        /// </summary>
        void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction);

        /// <summary>
        /// 매 프레임 업데이트가 필요한 로직을 처리합니다.
        /// </summary>
        void OnUpdate(WeaponRuntimeStats stats, float deltaTime);
    }
}
