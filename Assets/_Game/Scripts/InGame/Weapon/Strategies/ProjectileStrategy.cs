using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 투사체(Projectile)를 발사하는 공격 전략입니다.
    /// </summary>
    public class ProjectileStrategy : IWeaponStrategy
    {
        #region 내부 변수

        private readonly string m_projectileKey; 

        #endregion

        #region 생성자

        public ProjectileStrategy(string projectileKey)
        {
            m_projectileKey = projectileKey;
        }

        #endregion

        public void Initialize(WeaponDataSO data)
        {
            // 데이터 기반 초기화
        }
        
        // 생성자 오버로딩: 직접 Prefab을 받아서 Pool에 등록하는 로직은 Factory에서 처리한다고 가정
        // 혹은 Strategy 내에서 Pool 관리가 필요할 수도 있음. 
        // 여기서는 PoolManager가 이미 초기화되었다고 가정하고 Key로 접근하거나, 
        // WeaponSystem에서 Pool을 관리하는 방식을 따름.
        // 현재 프로젝트 구조상 WeaponPoolManager가 타입을 Key로 쓰거나 별도 메서드를 씀.
        // 유연성을 위해 제네릭을 쓰기 어려우므로(타입을 여기서 알기 힘듦), 
        // 리팩토링 과정에서 ProjectileBase 같은 공통 부모를 활용해야 함.

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            int count = stats.CurrentProjectileCount;
            float damage = stats.CurrentAttackPower;
            float speed = stats.CurrentAttackSpeed; // 탄속으로 쓸지? 보통은 투사체 자체 속도가 있음.

            for (int i = 0; i < count; i++)
            {
                // TODO: 멀티샷 각도 계산 로직 필요
                Vector3 fireDirection = direction; 
                
                // WeaponPoolManager가 제네릭 기반이라 여기서 구체적 타입을 모르면 호출이 난해함.
                // 해결책: IProjectile 인터페이스를 도입하거나, Reflection을 쓰거나, 
                // Factory에서 생성 시점에 구체적인 Strategy<T>를 만들어줘야 함.
                
                // 여기서는 개념적으로 작성하고, 실제 구현 시에는 WeaponFactory에서 제네릭 Strategy를 생성하도록 설계.
                Debug.Log($"[ProjectileStrategy] Attack! Count: {count}, Dmg: {damage}");
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 투사체는 발사 후 자체 로직으로 동작하므로 여기서 업데이트할 내용 없음
        }
    }
}
