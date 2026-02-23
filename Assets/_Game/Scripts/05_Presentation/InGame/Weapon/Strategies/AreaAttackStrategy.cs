using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Core.Interfaces;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// [설명]: 범위 공격 효과를 정의하는 인터페이스입니다.
    /// </summary>
    public interface IAreaAttackEffect
    {
        /// <summary>
        /// 지정된 위치에서 이펙트를 활성화합니다.
        /// </summary>
        void Activate(Vector3 position, WeaponRuntimeStats stats);
    }

    /// <summary>
    /// [설명]: 특정 위치에 범위 공격(Area of Effect)을 생성하는 범용 전략 클래스입니다.
    /// </summary>
    /// <typeparam name="TEffect">IAreaAttackEffect를 구현하는 MonoBehaviour</typeparam>
    public class AreaAttackStrategy<TEffect> : IWeaponStrategy
        where TEffect : MonoBehaviour, IAreaAttackEffect
    {
        #region 내부 변수

        private WeaponDataSO m_data;
        private WeaponPoolManager m_poolManager;

        #endregion

        #region 인터페이스 구현

        public void Init(
            WeaponDataSO data, 
            WeaponPoolManager poolManager,
            IGameStateService gameState,
            ICombatContext combatContext,
            IPlayerContext playerContext)
        {
            m_data = data;
            m_poolManager = poolManager;
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            // 구체적인 범위 공격 로직은 상속받거나 이곳에 구현
            // 예: 풀에서 TEffect를 가져와 Activate 호출
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 지속 피해 로직 등이 필요할 경우 구현
        }

        #endregion
    }
}