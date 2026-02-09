using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 기본적인 투사체(Projectile)를 발사하는 공격 전략입니다.
    /// 구체적인 투사체 타입(TProjectile)을 풀에서 관리합니다.
    /// </summary>
    /// <typeparam name="TProjectile">MonoBehaviour 상속 및 IProjectile 구현 클래스</typeparam>
    public class ProjectileStrategy<TProjectile> : IWeaponStrategy 
        where TProjectile : MonoBehaviour, IProjectile
    {
        #region 내부 상태 및 변수

        private WeaponDataSO m_data;

        #endregion

        #region IWeaponStrategy 구현

        public void Init(WeaponDataSO data)
        {
            m_data = data;

            // 투사체 프리팹 기반 오브젝트 풀 등록
            if (data.ProjectilePrefab != null)
            {
                WeaponPoolManager.Instance.GetOrAddPool<TProjectile>(
                    () => Object.Instantiate(data.ProjectilePrefab).GetComponent<TProjectile>(),
                    p => p.gameObject.SetActive(true),
                    p => p.gameObject.SetActive(false),
                    p => Object.Destroy(p.gameObject),
                    maxSize: 20
                );
            }
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            int count = stats.CurrentProjectileCount;

            for (int i = 0; i < count; i++)
            {
                var projectile = WeaponPoolManager.Instance.Get<TProjectile>();
                if (projectile != null)
                {
                    projectile.transform.position = owner.position;

                    // 투사체 초기화 및 발사 (각도 분산 로직은 필요 시 추가)
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
            // 투사체 발사 전략은 발사 후 업데이트가 필요 없음
        }

        #endregion
    }
}
