using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Core.Interfaces;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// [설명]: 기본적인 투사체(Projectile)를 발사하는 공격 전략입니다.
    /// 제네릭 타입(TProjectile)을 사용하여 구체적인 투사체를 풀링하고 관리합니다.
    /// </summary>
    /// <typeparam name="TProjectile">MonoBehaviour를 상속받고 IProjectile을 구현한 투사체 클래스</typeparam>
    public class ProjectileStrategy<TProjectile> : IWeaponStrategy
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

            // 투사체 프리팹 기반 오브젝트 풀 등록
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
                // 풀에서 투사체 가져오기
                var projectile = m_poolManager.Get<TProjectile>();
                if (projectile != null)
                {
                    projectile.transform.position = owner.position;

                    // 투사체 초기화 및 발사
                    // (필요 시 각도 분산 로직 추가 가능)
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
            // 투사체는 발사 후 자체 로직(MonoBehaviour)으로 동작하므로 
            // 전략 클래스에서의 프레임 업데이트는 필요하지 않습니다.
        }

        #endregion
    }
}