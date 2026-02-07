using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;
using InGame.Weapon.Base;

namespace InGame.Weapon
{
    /// <summary>
    /// 플레이어 주변을 회전하는 공(Ball)을 생성하고 관리하는 무기입니다.
    /// </summary>
    public class WeaponBallplay : WeaponBase
    {
        #region 인스펙터 필드

        [Header("공 설정")]
        [Tooltip("회전하는 공의 프리팹 (BallDamageDealer 컴포넌트 필수)")]
        [SerializeField] private GameObject m_ballPrefab;

        [Tooltip("생성할 공의 개수")]
        [SerializeField] private int m_ballCount = 2;

        [Tooltip("플레이어로부터 공까지의 회전 반경")]
        [SerializeField] private float m_rotationRadius = 2.5f;

        [Header("시각 보정 설정")]
        [Tooltip("공 스프라이트의 Z축 회전 보정값 (기본 0).")]
        [SerializeField] private float m_ballRotationOffset = 0f;

        #endregion

        #region 내부 상태 변수

        private float m_currentAngle = 0f;
        
        private IObjectPool<BallDamageDealer> m_ballPool;
        private readonly List<BallDamageDealer> m_activeBalls = new List<BallDamageDealer>();

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            InitializePool();
        }

        private new void OnEnable()
        {
            SetWeaponState(WeaponState.Idle);
            m_currentAngle = 0f;
            ClearBalls(); 
            SpawnBalls();
        }

        private new void OnDisable()
        {
            ClearBalls();
        }

        private void OnDestroy()
        {
            if (m_ballPool is System.IDisposable disposablePool)
            {
                disposablePool.Dispose();
            }
        }

        private void Update()
        {
            if (m_activeBalls.Count == 0) return;

            float rotationDelta = attackSpeed * Time.deltaTime;
            
            if (transform.lossyScale.x < 0)
            {
                rotationDelta *= -1;
            }

            m_currentAngle = (m_currentAngle + rotationDelta) % 360f;

            UpdateBallPositions();
        }

        #endregion

        #region 공 관리 로직

        private void SpawnBalls()
        {
            if (m_ballPrefab == null)
            {
                LogManager.LogError("[WeaponBallplay] 공(Ball) 프리팹이 할당되지 않았습니다!", LogManager.LogCategory.Weapon);
                return;
            }

            for (int i = 0; i < m_ballCount; i++)
            {
                var ball = m_ballPool.Get();
                ball.Initialize(this.attackPower, this.mobStunTime, this.coolTime);
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
                m_activeBalls[i].transform.localPosition = newPos;
                m_activeBalls[i].transform.localRotation = Quaternion.Euler(0, 0, orbitalAngle + m_ballRotationOffset);
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

        #region 오브젝트 풀링

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

        private BallDamageDealer CreateBall()
        {
            GameObject ballInstance = Instantiate(m_ballPrefab, transform);
            var comp = ballInstance.GetComponent<BallDamageDealer>();
            if (comp == null)
            {
                LogManager.LogError("[WeaponBallplay] 프리팹에 BallDamageDealer 컴포넌트가 없습니다!", LogManager.LogCategory.Weapon);
            }
            return comp;
        }

        private void OnGetBall(BallDamageDealer ball)
        {
            ball.gameObject.SetActive(true);
        }

        private void OnReleaseBall(BallDamageDealer ball)
        {
            ball.gameObject.SetActive(false);
        }

        private void OnDestroyBall(BallDamageDealer ball)
        {
            if (ball != null) Destroy(ball.gameObject);
        }

        #endregion
    }
}