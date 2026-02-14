using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon.Controllers;
using InGame.Weapon.Logic;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// [설명]: 부메랑(Boomerang) 무기의 공격 전략입니다.
    /// 비동기 루프를 통해 다수의 부메랑을 순차적으로 발사합니다.
    /// </summary>
    public class BoomerangStrategy : IWeaponStrategy
    {
        #region 내부 변수

        private BoomerangWeaponLogic m_logic;
        private WeaponPoolManager m_poolManager;
        
        private Transform m_firePoint;
        private bool m_isAttacking;
        private int m_currentActiveCount = 0;

        #endregion

        #region 인터페이스 구현

        public void Init(WeaponDataSO data, WeaponPoolManager poolManager)
        {
            m_poolManager = poolManager;
            if (m_poolManager == null) return;

            // 투사체 오브젝트 풀 등록
            if (data.ProjectilePrefab != null)
            {
                m_poolManager.GetOrAddPool<BoomerangProjectile>(
                    createFunc: () => Object.Instantiate(data.ProjectilePrefab).GetComponent<BoomerangProjectile>(),
                    actionOnGet: p => p.gameObject.SetActive(true),
                    actionOnRelease: p => p.gameObject.SetActive(false),
                    actionOnDestroy: p => Object.Destroy(p.gameObject),
                    defaultCapacity: 10,
                    maxSize: 20
                );
            }
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            if (m_isAttacking) return;

            // 비동기 발사 시작
            FireBoomerangAsync(stats, owner, direction).Forget();
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 부메랑은 발사 후 투사체 자체 로직으로 동작함
        }

        #endregion

        #region 상세 로직

        private async UniTaskVoid FireBoomerangAsync(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            m_isAttacking = true;
            m_firePoint = owner;

            if (direction == Vector3.zero) direction = Vector3.right;

            // 로직 초기화 및 갱신
            if (m_logic == null) m_logic = new BoomerangWeaponLogic(stats);
            else m_logic.UpdateStats(stats);

            if (m_poolManager == null)
            {
                m_isAttacking = false;
                return;
            }

            // 최대 활성 개수 제한
            if (m_currentActiveCount >= m_logic.MaxProjectiles)
            {
                m_isAttacking = false;
                return;
            }

            // 발사 개수 계산
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

                float currentAngle = m_logic.CalculateAngle(i, actualFireCount, baseAngle);
                Quaternion rotation = Quaternion.Euler(0, 0, currentAngle);

                var projectile = m_poolManager.Get<BoomerangProjectile>();
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
                        m_poolManager,
                        () => // onReturnComplete 콜백
                        {
                            m_currentActiveCount--;
                            if (m_currentActiveCount < 0) m_currentActiveCount = 0;
                        }
                    );
                }

                // 연사 딜레이
                // CancellationToken 처리는 owner가 파괴될 때 자동으로 취소되도록 함
                if (owner != null)
                {
                    await UniTask.Delay(m_logic.BurstDelayMs, cancellationToken: owner.GetCancellationTokenOnDestroy());
                }
                else
                {
                     // owner가 사라졌으면 루프 중단
                     m_isAttacking = false;
                     return;
                }
            }

            m_isAttacking = false;
        }

        #endregion
    }
}