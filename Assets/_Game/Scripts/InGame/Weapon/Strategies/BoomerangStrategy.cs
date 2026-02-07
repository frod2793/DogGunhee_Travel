using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    public class BoomerangStrategy : IWeaponStrategy
    {
        private float m_baseCount = 1;
        private Transform m_firePoint;
        private bool m_isAttacking;

        public void Initialize(WeaponDataSO data)
        {
            // 데이터에서 기본 발사체 개수 등을 가져올 수 있음
            // 현재는 상수가 아닌 데이터 기반으로 변경 권장
            
            // Pool 등록
            if (data.ProjectilePrefab != null)
            {
                WeaponPoolManager.Instance.GetOrAddPool<BoomerangProjectile>(
                    () => Object.Instantiate(data.ProjectilePrefab).GetComponent<BoomerangProjectile>(),
                    p => p.gameObject.SetActive(true),
                    p => p.gameObject.SetActive(false),
                    p => Object.Destroy(p.gameObject),
                    defaultCapacity: 10,
                    maxSize: 20
                );
            }
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            if (m_isAttacking) return;
            fireBoomerangAsync(stats, owner, direction).Forget();
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime) { }

        private async UniTaskVoid fireBoomerangAsync(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            m_isAttacking = true;
            m_firePoint = owner; // 혹은 별도의 FirePoint Transform을 주입받아야 함

            if (direction == Vector3.zero) direction = Vector3.right;

            // Evolution Check
            int count = stats.IsEvolved ? (int)m_baseCount + 2 : (int)m_baseCount;
            // stats.CurrentProjectileCount를 사용하는 것이 더 좋음
            count = Mathf.Max(count, stats.CurrentProjectileCount);

            float startAngle = -15f * (count - 1);
            float angleStep = (count > 1) ? 30f : 0f;

            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

            for (int i = 0; i < count; i++)
            {
                float currentAngle = baseAngle + startAngle + (angleStep * i);
                Quaternion rotation = Quaternion.Euler(0, 0, currentAngle);

                var projectile = WeaponPoolManager.Instance.Get<BoomerangProjectile>();
                if (projectile != null)
                {
                    projectile.transform.position = m_firePoint.position;
                    projectile.transform.rotation = rotation;

                    float finalSpeed = (stats.CurrentAttackSpeed > 0) ? stats.CurrentAttackSpeed : 1f;
                    
                    // Initialize 호출 (BoomerangProjectile 수정 필요 가능성 있음)
                    projectile.Initialize(m_firePoint, stats.CurrentAttackPower, stats.MobStunTime, finalSpeed, stats.CurrentAttackRange);
                }

                await UniTask.Delay(50, cancellationToken: owner.GetCancellationTokenOnDestroy());
            }
            
            // CoolTime은 WeaponController에서 관리하므로 여기서는 공격 시퀀스 딜레이만 관리하면 됨
            // 하지만 WeaponController가 CoolTime을 관리하므로 여기서는 굳이 딜레이를 줄 필요가 없음
            // 단, 연속 발사 중 재진입 방지를 위해 m_isAttacking 사용
            
           // 공격 종료 후 플래그 해제 (쿨타임은 외부에서 처리)
           m_isAttacking = false;
        }
    }
}
