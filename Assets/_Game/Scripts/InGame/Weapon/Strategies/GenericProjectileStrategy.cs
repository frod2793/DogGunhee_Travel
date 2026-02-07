using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    public interface IProjectile
    {
        void Initialize(Vector3 direction, float damage, float speed, float duration, bool isEvolved);
    }

    /// <summary>
    /// 제네릭을 사용하여 구체적인 투사체 타입을 처리하는 전략입니다.
    /// 초기화 시 Pool을 등록하고, 공격 시 Pool에서 가져와 발사합니다.
    /// </summary>
    /// <typeparam name="TProjectile">MonoBehaviour 상속 및 IProjectile 구현 클래스</typeparam>
    public class GenericProjectileStrategy<TProjectile> : IWeaponStrategy 
        where TProjectile : MonoBehaviour, IProjectile
    {
        private WeaponDataSO m_data;

        public void Initialize(WeaponDataSO data)
        {
            m_data = data;
            
            // Pool 등록
            // 주의: Prefab이 Data에 있어야 함.
            if (data.ProjectilePrefab != null)
            {
                // WeaponPoolManager가 제네릭 생성 함수를 지원해야 함.
                // 현재 구조상 직접 Instantiate 로직을 전달해야 할 수도 있음.
                WeaponPoolManager.Instance.GetOrAddPool<TProjectile>(
                    () => Object.Instantiate(data.ProjectilePrefab).GetComponent<TProjectile>(),
                    OnGet,
                    OnRelease,
                    OnDestroy,
                    maxSize: 20
                );
            }
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            int count = stats.CurrentProjectileCount;
            // TODO: 멀티샷 로직 (각도 분산 등)

            for (int i = 0; i < count; i++)
            {
                var projectile = WeaponPoolManager.Instance.Get<TProjectile>();
                if (projectile != null)
                {
                    projectile.transform.position = owner.position;
                    // 방향 분산 로직이 필요하다면 여기서 direction 수정
                    
                    projectile.Initialize(
                        direction, 
                        stats.CurrentAttackPower, 
                        stats.CurrentAttackSpeed, // 투사체 속도로 사용
                        stats.CurrentDuration,
                        stats.IsEvolved
                    );
                }
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime) { }

        // Pool Events
        private void OnGet(TProjectile p) => p.gameObject.SetActive(true);
        private void OnRelease(TProjectile p) => p.gameObject.SetActive(false);
        private void OnDestroy(TProjectile p) => Object.Destroy(p.gameObject);
    }
}
