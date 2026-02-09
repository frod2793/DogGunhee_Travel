using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 플레이어 주위에서 회전하는 투사체를 관리하는 전략 클래스입니다.
    /// </summary>
    public class OrbitProjectileStrategy : IWeaponStrategy
    {
        #region 내부 상태

        private WeaponDataSO m_data;

        #endregion

        #region IWeaponStrategy 구현

        public void Initialize(WeaponDataSO data)
        {
            m_data = data;
            // 회전 무기(Ball 등) 관련 초기화 로직
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            // 투사체 생성 및 회전 궤도 진입 로직
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 회전 상태 업데이트
        }

        #endregion
    }
}
