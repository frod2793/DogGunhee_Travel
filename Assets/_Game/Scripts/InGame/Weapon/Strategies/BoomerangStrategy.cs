using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon.Controllers;
using InGame.Weapon.Logic;

namespace InGame.Weapon.Strategies
{
    public class BoomerangStrategy : IWeaponStrategy
    {
        private BoomerangWeaponLogic m_logic;
        private Transform m_firePoint;
        private bool m_isAttacking;
        private int m_currentActiveCount = 0;

        public void Initialize(WeaponDataSO data)
        {
            // 1. 비주얼 설정 추출 및 로직 초기화
            BoomerangWeaponTuningData? tuningData = null;
            if (data.ModelPrefab != null)
            {
                var view =   WeaponPoolManager.Instance.GetComponent<BoomerangWeaponView>();
                if (view != null)
                {
                    tuningData = new BoomerangWeaponTuningData
                    {
                        StartAngle = view.StartAngle,
                        AngleStep = view.AngleStep,
                        BurstDelayMs = view.BurstDelayMs
                    };
                }
            }

            // [Note] BoomerangStrategy는 Attack 시점에 Logic을 사용할 것이므로 
            // 실제 로직 생성은 Attack 호출 시 스탯과 함께 수행하거나, 
            // Initialize 시점에 기본 스탯으로 생성 후 UpdateStats 호출.
            // 여기서는 Attack 시점에 스탯과 함께 갱신하는 방식을 사용.
            
            // 2. Pool 등록
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
            m_firePoint = owner;

            if (direction == Vector3.zero) direction = Vector3.right;

            // 로직 생성/갱신
            if (m_logic == null) m_logic = new BoomerangWeaponLogic(stats);
            else m_logic.UpdateStats(stats);

            // [Active Limit Logic]
            if (m_currentActiveCount >= m_logic.MaxProjectiles)
            {
                m_isAttacking = false;
                return;
            }

            int availableSlots = m_logic.MaxProjectiles - m_currentActiveCount;
            int burstCount = m_logic.BurstCount;
            int actualFireCount = Mathf.Min(burstCount, availableSlots);

            if (actualFireCount <= 0)
            {
                m_isAttacking = false;
                return;
            }

            // 기준 각도 계산
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

            for (int i = 0; i < actualFireCount; i++)
            {
                m_currentActiveCount++;

                // 로직 클래스에 각도 계산 위임
                float currentAngle = m_logic.CalculateAngle(i, actualFireCount, baseAngle);
                Quaternion rotation = Quaternion.Euler(0, 0, currentAngle);

                var projectile = WeaponPoolManager.Instance.Get<BoomerangProjectile>();
                if (projectile != null)
                {
                    projectile.transform.position = m_firePoint.position;
                    projectile.transform.rotation = rotation;
                    
                    projectile.Initialize(
                        m_firePoint, 
                        m_logic.AttackPower, 
                        m_logic.StunTime, 
                        m_logic.Speed, 
                        m_logic.Range,
                        () => 
                        {
                            m_currentActiveCount--;
                            if (m_currentActiveCount < 0) m_currentActiveCount = 0;
                        }
                    );
                }

                await UniTask.Delay(m_logic.BurstDelayMs, cancellationToken: owner.GetCancellationTokenOnDestroy());
            }
            
            m_isAttacking = false;
        }
    }
}
