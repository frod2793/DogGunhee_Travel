using UnityEngine;
using InGame.Weapon.Base;
using InGame.Manager;
using InGame.ObjectPool;
using InGame.Weapon.Controllers;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 플레이어 주위에서 회전하는 투사체를 관리하는 전략 클래스입니다.
    /// </summary>
    public class OrbitProjectileStrategy : IWeaponStrategy
    {
        #region 내부 상태

        private WeaponDataSO m_data;
        private readonly System.Collections.Generic.List<OrbitProjectile> m_activeBalls = new();
        private GameObject m_ballPrefab;
        private BallWeaponView m_view;

        #endregion

        #region IWeaponStrategy 구현

        public void Initialize(WeaponDataSO data)
        {
            m_data = data;
            m_ballPrefab = data.ProjectilePrefab;

            // 1. View 추출 (WeaponPoolManager에서 설정 컴포넌트 탐색)
            if (WeaponPoolManager.Instance != null)
            {
                m_view = WeaponPoolManager.Instance.GetComponent<BallWeaponView>();
            }

            // View가 없으면 경고 후 기본값 생성
            if (m_view == null && WeaponPoolManager.Instance != null)
            {
                m_view = WeaponPoolManager.Instance.gameObject.AddComponent<BallWeaponView>();
            }

            // 2. 풀 등록
            if (m_ballPrefab != null)
            {
                WeaponPoolManager.Instance.GetOrAddPool<OrbitProjectile>(
                    CreateBall,
                    OnGetBall,
                    OnReleaseBall,
                    OnDestroyBall,
                    defaultCapacity: 5,
                    maxSize: 20
                );
            }
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            SyncBallCount(stats, owner);
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            if (GameManager.Instance != null && GameManager.Instance.SpawnedPlayer != null)
            {
                SyncBallCount(stats, GameManager.Instance.SpawnedPlayer.transform);
            }
        }

        #endregion

        #region 상세 로직

        private void SyncBallCount(WeaponRuntimeStats stats, Transform owner)
        {
            int targetCount = stats.ProjectileCount;
            
            while (m_activeBalls.Count < targetCount)
            {
                var ball = WeaponPoolManager.Instance.Get<OrbitProjectile>();
                if (ball != null)
                {
                    float angle = (360f / targetCount) * m_activeBalls.Count;
                    
                    // View 설정값 반영
                    float baseSpeed = 180f * (m_view != null ? m_view.RotationSpeedMultiplier : 1.0f);
                    float rotSpeed = baseSpeed * stats.AttackSpeed;
                    float offset = m_view != null ? m_view.RotationOffset : 0f;
                    bool rotateAlong = m_view != null ? m_view.RotateWithOrbit : true;

                    ball.Initialize(owner, stats.AttackRange, rotSpeed, angle, stats, offset, rotateAlong);
                    m_activeBalls.Add(ball);
                }
                else break;
            }
        }

        #endregion

        #region 오브젝트 풀 델리게이트

        private OrbitProjectile CreateBall()
        {
            if (m_ballPrefab == null) return null;
            var go = Object.Instantiate(m_ballPrefab);
            return go.GetComponent<OrbitProjectile>() ?? go.AddComponent<OrbitProjectile>();
        }

        private void OnGetBall(OrbitProjectile ball) => ball.gameObject.SetActive(true);
        private void OnReleaseBall(OrbitProjectile ball) => ball.gameObject.SetActive(false);
        private void OnDestroyBall(OrbitProjectile ball)
        {
            if (ball != null) Object.Destroy(ball.gameObject);
        }

        #endregion
    }
}
