using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using InGame.Weapon.Base;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 플레이어 주변을 회전하는 공(Ball)을 관리하는 POCO 컨트롤러입니다.
    /// </summary>
    public class BallplayWeaponController : WeaponControllerBase
    {
        #region 설정 데이터

        private GameObject m_ballPrefab;
        private int m_ballCount;
        private float m_rotationRadius;
        private float m_ballRotationOffset;

        #endregion

        #region 내부 상태

        private float m_currentAngle;
        private IObjectPool<BallDamageDealer> m_ballPool;
        private readonly List<BallDamageDealer> m_activeBalls = new List<BallDamageDealer>();

        #endregion

        #region 초기화

        /// <summary>
        /// BallplayWeaponController를 초기화합니다.
        /// </summary>
        /// <param name="data">무기 데이터 ScriptableObject</param>
        /// <param name="ownerTransform">소유자(플레이어)의 Transform</param>
        /// <param name="getTargetDirection">공격 방향을 가져오는 델리게이트 (Ballplay는 사용하지 않음)</param>
        /// <param name="ballPrefab">Ball 프리팹</param>
        /// <param name="ballCount">생성할 공 개수</param>
        /// <param name="rotationRadius">회전 반경</param>
        /// <param name="ballRotationOffset">스프라이트 회전 보정값</param>
        public void Init(
            WeaponDataSO data,
            Transform ownerTransform,
            Func<Vector3> getTargetDirection,
            GameObject ballPrefab,
            int ballCount = 2,
            float rotationRadius = 2.5f,
            float ballRotationOffset = 0f)
        {
            base.Init(data, ownerTransform, getTargetDirection);

            m_ballPrefab = ballPrefab;
            m_ballCount = ballCount;
            m_rotationRadius = rotationRadius;
            m_ballRotationOffset = ballRotationOffset;

            m_currentAngle = 0f;

            // 풀 초기화
            InitializePool();

            // 공 생성
            SpawnBalls();
        }

        private void InitializePool()
        {
            m_ballPool = new ObjectPool<BallDamageDealer>(
                createFunc: CreateBall,
                actionOnGet: OnGetBall,
                actionOnRelease: OnReleaseBall,
                actionOnDestroy: OnDestroyBall,
                collectionCheck: false,
                defaultCapacity: m_ballCount,
                maxSize: m_ballCount * 2
            );
        }

        #endregion

        #region IWeaponController 구현

        public override void OnUpdate(float deltaTime)
        {
            if (m_activeBalls.Count == 0) return;

            float rotationDelta = m_runtimeStats.AttackSpeed * deltaTime;

            // 부모 스케일에 따른 방향 반전
            if (m_ownerTransform.lossyScale.x < 0)
            {
                rotationDelta *= -1;
            }

            m_currentAngle = (m_currentAngle + rotationDelta) % 360f;

            UpdateBallPositions();
        }

        public override void Attack(Vector3 direction)
        {
            // Ballplay는 자동 궤도 공격이므로 수동 Attack은 무시됩니다.
        }

        public override void Dispose()
        {
            ClearBalls();

            if (m_ballPool is IDisposable disposablePool)
            {
                disposablePool.Dispose();
            }
        }

        #endregion

        #region 공 관리 로직

        private void SpawnBalls()
        {
            if (m_ballPrefab == null)
            {
                LogManager.LogError("BallplayWeaponController: Ball 프리팹이 할당되지 않았습니다!", LogManager.LogCategory.Weapon);
                return;
            }

            for (int i = 0; i < m_ballCount; i++)
            {
                var ball = m_ballPool.Get();
                ball.Initialize(m_runtimeStats.AttackPower, m_runtimeStats.MobStunTime, m_runtimeStats.CoolTime);
                m_activeBalls.Add(ball);
            }

            UpdateBallPositions();
        }

        private void UpdateBallPositions()
        {
            int count = m_activeBalls.Count;
            if (count == 0) return;

            float angleStep = 360f / count;
            float radius = m_rotationRadius;

            for (int i = 0; i < count; i++)
            {
                float orbitalAngle = m_currentAngle + (i * angleStep);
                float angleRad = orbitalAngle * Mathf.Deg2Rad;
                Vector3 newPos = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0) * radius;

                // 공은 ownerTransform 기준으로 배치
                m_activeBalls[i].transform.position = m_ownerTransform.position + newPos;
                m_activeBalls[i].transform.rotation = Quaternion.Euler(0, 0, orbitalAngle + m_ballRotationOffset);
            }
        }

        private void ClearBalls()
        {
            foreach (var ball in m_activeBalls)
            {
                if (ball != null)
                {
                    m_ballPool.Release(ball);
                }
            }
            m_activeBalls.Clear();
        }

        #endregion

        #region 오브젝트 풀 델리게이트

        private BallDamageDealer CreateBall()
        {
            if (m_ballPrefab == null)
            {
                LogManager.LogError("BallplayWeaponController: Ball 프리팹이 할당되지 않았습니다!", LogManager.LogCategory.Weapon);
                return null;
            }

            GameObject ballInstance = UnityEngine.Object.Instantiate(m_ballPrefab);
            var comp = ballInstance.GetComponent<BallDamageDealer>();
            if (comp == null)
            {
                LogManager.LogError("BallplayWeaponController: 프리팹에 BallDamageDealer 컴포넌트가 없습니다!", LogManager.LogCategory.Weapon);
            }
            return comp;
        }

        private void OnGetBall(BallDamageDealer ball) => ball.gameObject.SetActive(true);
        private void OnReleaseBall(BallDamageDealer ball) => ball.gameObject.SetActive(false);
        private void OnDestroyBall(BallDamageDealer ball)
        {
            if (ball != null) UnityEngine.Object.Destroy(ball.gameObject);
        }

        #endregion
    }
}
