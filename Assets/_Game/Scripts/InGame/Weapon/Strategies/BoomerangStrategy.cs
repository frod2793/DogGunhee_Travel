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
        private int m_currentActiveCount = 0;

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

            // [Active Limit Logic]
            // 현재 활성화된 투사체 수가 최대 허용 수(스탯) 이상이면 발사하지 않음
            int maxProjectiles = Mathf.Max(1, stats.CurrentProjectileCount);
            if (m_currentActiveCount >= maxProjectiles)
            {
                m_isAttacking = false;
                return;
            }

            // 이번에 발사할 수 있는 최대 개수 계산 (남은 슬롯만큼만 발사)
            // 예: Max 3, Current 2 -> 1개 발사 가능
            // 하지만 게임적 허용으로 Burst Count는 유지하되, Total Limit를 넘지 않도록 조정할 수도 있음.
            // 여기서는 "남은 슬롯만큼만 발사"하는 방식으로 구현.
            int availableSlots = maxProjectiles - m_currentActiveCount;
            
            // 기존 발사 로직: Evolution(진화) 시 +2, 기본 +알파
            // 하지만 "투사체 개수" 스탯이 곧 Max Limit라면, Burst Count도 이에 맞춰야 함.
            // 보통 뱀서류 게임에서 Projectile Amount는 "한 번에 발사하는 수"이자 "동시에 존재 가능한 수"일 수 있음.
            // 사용자의 요청 "투사체 개수에 한해서 추가 발사를 막는 로직"을 "Total Active Limit <= ProjectileCount"로 해석.
            
            int burstCount = stats.IsEvolved ? (int)m_baseCount + 2 : (int)m_baseCount;
            if (stats.CurrentProjectileCount > 0) burstCount = Mathf.Max(burstCount, stats.CurrentProjectileCount);

            // 실제 발사할 개수 = Min(Burst, Available)
            int actualFireCount = Mathf.Min(burstCount, availableSlots);

            if (actualFireCount <= 0)
            {
                m_isAttacking = false;
                return;
            }

            float startAngle = -15f * (actualFireCount - 1);
            float angleStep = (actualFireCount > 1) ? 30f : 0f;

            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

            for (int i = 0; i < actualFireCount; i++)
            {
                m_currentActiveCount++; // 발사 시 카운트 증가

                float currentAngle = baseAngle + startAngle + (angleStep * i);
                Quaternion rotation = Quaternion.Euler(0, 0, currentAngle);

                var projectile = WeaponPoolManager.Instance.Get<BoomerangProjectile>();
                if (projectile != null)
                {
                    projectile.transform.position = m_firePoint.position;
                    projectile.transform.rotation = rotation;

                    float finalSpeed = (stats.CurrentAttackSpeed > 0) ? stats.CurrentAttackSpeed : 1f;
                    
                    // Initialize w/ Callback
                    projectile.Initialize(
                        m_firePoint, 
                        stats.CurrentAttackPower, 
                        stats.MobStunTime, 
                        finalSpeed, 
                        stats.CurrentAttackRange,
                        () => 
                        {
                            // 투사체 회수 시 카운트 감소
                            m_currentActiveCount--;
                            if (m_currentActiveCount < 0) m_currentActiveCount = 0;
                        }
                    );
                }

                await UniTask.Delay(50, cancellationToken: owner.GetCancellationTokenOnDestroy());
            }
            
            m_isAttacking = false;
        }
    }
}
