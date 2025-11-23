using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Pool;
using System.Collections.Generic;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 플레이어 주변을 회전하는 공(Ball)을 생성하고 관리하는 무기입니다.
    /// 반시계 방향으로 회전하며, 공 자체도 궤도에 맞춰 회전합니다. (오프셋 보정 가능)
    /// </summary>
    public class WeaponBallplay : Weaphon_base
    {
        #region 인스펙터 필드

        [Header("공 설정")]
        [Tooltip("회전하는 공의 프리팹 (BallDamageDealer 컴포넌트 필수)")]
        [FormerlySerializedAs("ballPrefab")]
        [SerializeField] private GameObject m_ballPrefab;

        [Tooltip("생성할 공의 개수")]
        [FormerlySerializedAs("ballCount")]
        [SerializeField] private int m_ballCount = 2;

        [Tooltip("플레이어로부터 공까지의 회전 반경")]
        [FormerlySerializedAs("rotationRadius")]
        [SerializeField] private float m_rotationRadius = 2.5f;

        [Header("시각 보정 설정")]
        [Tooltip("공 스프라이트의 Z축 회전 보정값 (기본 0). 이미지가 90도 돌아가 있다면 여기서 조절하세요.")]
        [SerializeField] private float m_ballRotationOffset = 0f; // [추가됨]

        #endregion

        #region 내부 상태 변수

        private float m_currentAngle = 0f;
        
        // 오브젝트 풀 및 활성 리스트
        private IObjectPool<BallDamageDealer> m_ballPool;
        private readonly List<BallDamageDealer> m_activeBalls = new List<BallDamageDealer>();

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            InitializePool();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            
            m_currentAngle = 0f; // 각도 초기화
            
            ClearBalls(); 
            SpawnBalls();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
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

            // 1. 회전 각도 업데이트 (반시계 방향)
            float rotationDelta = attackSpeed * Time.deltaTime;
            
            // 플레이어가 좌우 반전(Scale X < 0)되었을 때 시각적 보정
            if (transform.lossyScale.x < 0)
            {
                rotationDelta *= -1;
            }

            m_currentAngle = (m_currentAngle + rotationDelta) % 360f;

            // 2. 공 위치 및 회전 업데이트
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
                ball.Initialize(this);
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
                // 현재 공의 궤도 각도 (Degree)
                float orbitalAngle = m_currentAngle + (i * angleStep);
                
                // 위치 계산 (Radian)
                float angleRad = orbitalAngle * Mathf.Deg2Rad;
                Vector3 newPos = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0) * radius;
                m_activeBalls[i].transform.localPosition = newPos;

                // [수정됨] 회전 설정 (자전 + 오프셋 보정)
                // 공이 궤도를 돌면서 자신의 머리가 궤도 바깥쪽(또는 안쪽)을 향하게 하려면 오프셋을 조절하면 됩니다.
                // 예: 스프라이트가 위를 보고 있다면 -90, 오른쪽을 보고 있다면 0 등
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