using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon.Controllers;
using InGame.Weapon.Logic;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 부메랑(Boomerang) 무기의 공격 전략을 담당하는 클래스입니다.
    /// </summary>
    public class BoomerangStrategy : IWeaponStrategy
    {
        #region 내부 상태 및 변수

        private BoomerangWeaponLogic m_logic;
        private Transform m_firePoint;
        private bool m_isAttacking;
        private int m_currentActiveCount = 0;

        #endregion

        #region IWeaponStrategy 구현

        public void Init(WeaponDataSO data)
        {
            // 2. 투사체 오브젝트 풀 등록
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
            if (m_isAttacking)
            {
                return;
            }

            FireBoomerangAsync(stats, owner, direction).Forget();
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 부메랑 전략은 별도의 프레임 업데이트가 필요 없음
        }

        #endregion

        #region 상세 공격 로직

        /// <summary>
        /// 비동기 방식으로 부메랑 투사체를 연속 발사합니다.
        /// </summary>
        private async UniTaskVoid FireBoomerangAsync(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            m_isAttacking = true;
            m_firePoint = owner;

            if (direction == Vector3.zero)
            {
                direction = Vector3.right;
            }

            // 비즈니스 로직 연동
            if (m_logic == null)
            {
                m_logic = new BoomerangWeaponLogic(stats);
            }
            else
            {
                m_logic.UpdateStats(stats);
            }

            // 활성화된 투사체 개수 제한 체크
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

            // 기준 발사 각도 계산 (90도 보정)
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

            for (int i = 0; i < actualFireCount; i++)
            {
                m_currentActiveCount++;

                // 전략에 따른 각도 계산 분산
                float currentAngle = m_logic.CalculateAngle(i, actualFireCount, baseAngle);
                Quaternion rotation = Quaternion.Euler(0, 0, currentAngle);

                var projectile = WeaponPoolManager.Instance.Get<BoomerangProjectile>();
                if (projectile != null)
                {
                    projectile.transform.position = m_firePoint.position;
                    projectile.transform.rotation = rotation;
                    
                    projectile.Init(
                        m_firePoint, 
                        m_logic.AttackPower, 
                        m_logic.StunTime, 
                        m_logic.Speed, 
                        m_logic.Range,
                        () => 
                        {
                            m_currentActiveCount--;
                            if (m_currentActiveCount < 0)
                            {
                                m_currentActiveCount = 0;
                            }
                        }
                    );
                }

                await UniTask.Delay(m_logic.BurstDelayMs, cancellationToken: owner.GetCancellationTokenOnDestroy());
            }
            
            m_isAttacking = false;
        }

        #endregion
    }
}
