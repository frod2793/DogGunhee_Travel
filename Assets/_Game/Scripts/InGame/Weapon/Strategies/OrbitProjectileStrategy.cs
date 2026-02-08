using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 플레이어 주변을 회전하는 투사체를 관리하는 전략입니다.
    /// 패시브 무기로, Attack은 호출되지 않고 OnUpdate에서 회전 로직을 처리합니다.
    /// </summary>
    public class OrbitProjectileStrategy : IWeaponStrategy
    {
        #region 상수

        private const float k_DefaultRadius = 2.5f;
        private const int k_DefaultBallCount = 2;

        #endregion

        #region 내부 변수

        private WeaponDataSO m_data;
        private Transform m_owner;
        private float m_currentAngle = 0f;

        private IObjectPool<BallDamageDealer> m_ballPool;
        private readonly List<BallDamageDealer> m_activeBalls = new List<BallDamageDealer>();
        private bool m_initialized = false;

        #endregion

        #region IWeaponStrategy 구현

        public void Initialize(WeaponDataSO data)
        {
            m_data = data;
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            // 패시브 무기: 최초 호출 시 공 생성
            if (!m_initialized && owner != null)
            {
                m_owner = owner;
                InitializePool();
                SpawnBalls(stats);
                m_initialized = true;
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            if (!m_initialized || m_activeBalls.Count == 0) return;

            // 회전 속도 계산
            float rotationDelta = stats.CurrentAttackSpeed * deltaTime * 100f;
            
            // 소유자 방향에 따라 회전 방향 반전
            if (m_owner != null && m_owner.lossyScale.x < 0)
            {
                rotationDelta *= -1;
            }

            m_currentAngle = (m_currentAngle + rotationDelta) % 360f;

            UpdateBallPositions(stats);
        }

        #endregion

        #region 내부 메서드

        private void InitializePool()
        {
            m_ballPool = new ObjectPool<BallDamageDealer>(
                createFunc: CreateBall,
                actionOnGet: OnGetBall,
                actionOnRelease: OnReleaseBall,
                actionOnDestroy: OnDestroyBall,
                collectionCheck: false,
                defaultCapacity: k_DefaultBallCount,
                maxSize: k_DefaultBallCount * 2
            );
        }

        private void SpawnBalls(WeaponRuntimeStats stats)
        {
            if (m_data?.ProjectilePrefab == null)
            {
                Debug.LogWarning("[OrbitProjectileStrategy] ProjectilePrefab이 없습니다.");
                return;
            }

            int ballCount = stats?.CurrentProjectileCount > 0 ? stats.CurrentProjectileCount : k_DefaultBallCount;

            for (int i = 0; i < ballCount; i++)
            {
                var ball = m_ballPool.Get();
                ball.Initialize(stats.CurrentAttackPower, stats.MobStunTime, stats.CurrentCoolTime);
                m_activeBalls.Add(ball);
            }

            UpdateBallPositions(stats);
        }

        private void UpdateBallPositions(WeaponRuntimeStats stats)
        {
            int count = m_activeBalls.Count;
            if (count == 0 || m_owner == null) return;

            float angleStep = 360f / count;
            float radius = stats?.CurrentAttackRange > 0 ? stats.CurrentAttackRange : k_DefaultRadius;

            for (int i = 0; i < count; i++)
            {
                float orbitalAngle = m_currentAngle + (i * angleStep);
                float angleRad = orbitalAngle * Mathf.Deg2Rad;
                Vector3 newPos = m_owner.position + new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0) * radius;
                m_activeBalls[i].transform.position = newPos;
                m_activeBalls[i].transform.rotation = Quaternion.Euler(0, 0, orbitalAngle + m_activeBalls[i].RotationOffset);
            }
        }

        private BallDamageDealer CreateBall()
        {
            if (m_data?.ProjectilePrefab == null) return null;

            GameObject ballInstance = Object.Instantiate(m_data.ProjectilePrefab, m_owner);
            var comp = ballInstance.GetComponent<BallDamageDealer>();
            if (comp == null)
            {
                Debug.LogWarning("[OrbitProjectileStrategy] BallDamageDealer 컴포넌트가 없습니다.");
            }
            return comp;
        }

        private void OnGetBall(BallDamageDealer ball)
        {
            if (ball != null) ball.gameObject.SetActive(true);
        }

        private void OnReleaseBall(BallDamageDealer ball)
        {
            if (ball != null) ball.gameObject.SetActive(false);
        }

        private void OnDestroyBall(BallDamageDealer ball)
        {
            if (ball != null) Object.Destroy(ball.gameObject);
        }

        #endregion
    }
}
