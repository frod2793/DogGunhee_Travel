using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Core.Interfaces;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// [설명]: 개별 투사체(Projectile)의 공통 초기화 인터페이스입니다.
    /// </summary>
    public interface IProjectile
    {
        void Init(Vector3 direction, float damage, float speed, float duration, bool isEvolved);
    }

    /// <summary>
    /// [설명]: 제네릭 타입의 투사체를 발사하는 범용 전략입니다.
    /// </summary>
    /// <typeparam name="TProjectile">IProjectile을 구현한 MonoBehaviour</typeparam>
    public class GenericProjectileStrategy<TProjectile> : IWeaponStrategy
        where TProjectile : MonoBehaviour, IProjectile
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

            if (m_poolManager == null) return;

            // 투사체 풀 자동 등록
            if (data.ProjectilePrefab != null)
            {
                m_poolManager.GetOrAddPool<TProjectile>(
                    createFunc: () => Object.Instantiate(data.ProjectilePrefab).GetComponent<TProjectile>(),
                    actionOnGet: p => p.gameObject.SetActive(true),
                    actionOnRelease: p => p.gameObject.SetActive(false),
                    actionOnDestroy: p => Object.Destroy(p.gameObject),
                    maxSize: 20
                );
            }
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            if (m_poolManager == null) return;

            int count = stats.CurrentProjectileCount;

            for (int i = 0; i < count; i++)
            {
                var projectile = m_poolManager.Get<TProjectile>();
                if (projectile != null)
                {
                    projectile.transform.position = owner.position;

                    // 투사체 초기화
                    projectile.Init(
                        direction,
                        stats.CurrentAttackPower,
                        stats.CurrentAttackSpeed,
                        stats.CurrentDuration,
                        stats.IsEvolved
                    );
                }
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 투사체는 발사 후 자체 로직으로 동작하므로 업데이트 없음
        }

        #endregion
    }
}