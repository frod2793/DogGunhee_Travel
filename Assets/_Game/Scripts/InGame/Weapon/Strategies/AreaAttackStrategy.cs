using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 범위 공격 무기를 위한 전략 인터페이스입니다.
    /// </summary>
    public interface IAreaAttackEffect
    {
        void Activate(Vector3 position, WeaponRuntimeStats stats);
    }

    /// <summary>
    /// 특정 위치에 범위 공격을 생성하는 전략 클래스입니다.
    /// </summary>
    public class AreaAttackStrategy<TEffect> : IWeaponStrategy 
        where TEffect : MonoBehaviour, IAreaAttackEffect
    {
        #region 내부 상태

        private WeaponDataSO m_data;

        #endregion

        #region IWeaponStrategy 구현

        public void Initialize(WeaponDataSO data)
        {
            m_data = data;
            
            // 프리팹이 있을 경우 풀 등록 (필요 시)
            if (data.ProjectilePrefab != null)
            {
                // TEffect 타입으로 풀링 처리 가능
            }
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            // 기본적인 위치 기반 범위 공격 로직 구현
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 지속 피해 등 시간 기반 업데이트 로직 필요 시 구현
        }

        #endregion
    }
}
