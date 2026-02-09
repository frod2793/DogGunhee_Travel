using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 범위 공격 무기를 위한 이펙트 인터페이스입니다.
    /// </summary>
    public interface IAreaAttackEffect
    {
        /// <summary>
        /// 특정 위치에서 공격 효과를 활성화합니다.
        /// </summary>
        void Activate(Vector3 position, WeaponRuntimeStats stats);
    }

    /// <summary>
    /// 특정 위치에 범위 공격(TEffect)을 생성하는 범용 공격 전략 클래스입니다.
    /// </summary>
    public class AreaAttackStrategy<TEffect> : IWeaponStrategy 
        where TEffect : MonoBehaviour, IAreaAttackEffect
    {
        #region 내부 상태 및 변수

        private WeaponDataSO m_data;

        #endregion

        #region IWeaponStrategy 구현

        public void Init(WeaponDataSO data)
        {
            m_data = data;
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            // 기본 로직은 상속받거나 구체화하여 사용
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 지속 피해 등 필요 시 업데이트
        }

        #endregion
    }
}
