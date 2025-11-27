using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

namespace DogGuns_Games.vamsir
{
    [RequireComponent(typeof(ParticleSystem), typeof(EdgeCollider2D))]
    public class WeaphonSmell : WeaphonBase
    {
        private struct TrailPoint
        {
            public Vector3 Position;
            public float CreationTime;
        }

        #region 인스펙터 필드

        [Header("기본 냄새 흔적 설정")]
        [Tooltip("흔적의 최대 길이")]
        [SerializeField] private int m_maxTrailPoints = 50;
        
        [Tooltip("새로운 포인트를 생성하기 위해 이동해야 하는 최소 거리")]
        [SerializeField] private float m_pointSpacing = 0.8f;
        
        [Tooltip("흔적의 기본 시각적 크기")]
        [SerializeField] private float m_trailWidth = 1.5f;
        
        [Tooltip("흔적이 완전히 사라지기까지의 시간")]
        [SerializeField] private float m_trailLifetime = 5f;


        [Header("뭉게뭉게 효과 설정")]
        [SerializeField] [Range(1, 10)] private int m_cloudDensity = 3;
        [SerializeField] [Range(0f, 1f)] private float m_cloudSpread = 0.3f;
        [SerializeField] [Range(0f, 0.5f)] private float m_sizeVariation = 0.2f;

        #endregion

        #region 내부 변수

        private ParticleSystem m_particleSystem;
        private ParticleSystem.EmitParams m_emitParams;
        private EdgeCollider2D m_trailCollider;
        private Transform m_playerTransform;

        private TrailPoint[] m_points;
        private int m_head;
        private int m_tail;
        private int m_pointCount;

        private readonly List<Vector2> m_colliderPointsCache = new List<Vector2>(100);
        private readonly Dictionary<int, float> m_damageCooldowns = new Dictionary<int, float>();
        
        private Vector3 m_lastFramePlayerPos;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_particleSystem = GetComponent<ParticleSystem>();
            m_trailCollider = GetComponent<EdgeCollider2D>();
        }

        private void OnEnable()
        {
            SetWeaphonState(WeaphonState.Idle);
            if (GameManager.Instance != null)
            {
                m_playerTransform = GameManager.Instance.PlayerTransfrom();
                if (m_playerTransform != null)
                {
                    m_lastFramePlayerPos = m_playerTransform.position;
                }
            }
            InitializeTrail();
        }

        private void OnDisable()
        {
            ResetTrailData();
        }

        private void Update()
        {
            if (m_playerTransform == null || !m_trailCollider) return;

            float currentTime = Time.time;

            while (m_pointCount > 0 && currentTime - m_points[m_head].CreationTime > m_trailLifetime)
            {
                m_head = (m_head + 1) % m_maxTrailPoints;
                m_pointCount--;
            }

            Vector3 currentPos = m_playerTransform.position;

            int lastIndex = (m_tail - 1 + m_maxTrailPoints) % m_maxTrailPoints;
            bool shouldAdd = m_pointCount == 0 || Vector3.Distance(m_points[lastIndex].Position, currentPos) > m_pointSpacing;

            if (shouldAdd)
            {
                if (m_pointCount == m_maxTrailPoints)
                {
                    m_head = (m_head + 1) % m_maxTrailPoints;
                    m_pointCount--;
                }

                m_points[m_tail] = new TrailPoint { Position = currentPos, CreationTime = currentTime };
                m_tail = (m_tail + 1) % m_maxTrailPoints;
                m_pointCount++;
                
                EmitPoisonCloud(currentPos);
            }

            bool playerMoved = Vector3.Distance(currentPos, m_lastFramePlayerPos) > Mathf.Epsilon;

            if (m_pointCount > 0 || playerMoved)
            {
                UpdateColliderWithDynamicTail(currentPos);
            }
            
            m_lastFramePlayerPos = currentPos;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!other.CompareTag("Mob")) return;

            int id = other.gameObject.GetInstanceID();
            float currentTime = Time.time;

            if (!m_damageCooldowns.TryGetValue(id, out float nextTime) || currentTime >= nextTime)
            {
                if (other.TryGetComponent(out VamserMobBase mob) && !mob.IsDead)
                {
                    mob.TakeDamage(attackPower, mobStunTime); 
                    m_damageCooldowns[id] = currentTime + coolTime;
                }
            }
        }

        #endregion

        #region 초기화 및 재설정

        private void InitializeTrail()
        {
            m_emitParams = new ParticleSystem.EmitParams{};
            
            if (m_trailCollider != null)
            {
                m_trailCollider.isTrigger = true;
                m_trailCollider.enabled = true; 
            }

            if (m_points == null || m_points.Length != m_maxTrailPoints)
            {
                m_points = new TrailPoint[m_maxTrailPoints];
            }

            ResetTrailData();
        }

        private void ResetTrailData()
        {
            m_head = 0;
            m_tail = 0;
            m_pointCount = 0;
            m_damageCooldowns.Clear();

            if (m_particleSystem != null) m_particleSystem.Clear();
            if (m_trailCollider != null)
            {
                m_trailCollider.Reset();
                m_trailCollider.isTrigger = true;
            }
        }

        #endregion

        #region 비주얼 및 물리 업데이트

        private void EmitPoisonCloud(Vector3 centerPos)
        {
            if (m_particleSystem == null) return;

            for (int i = 0; i < m_cloudDensity; i++)
            {
                Vector2 randomOffset2D = Random.insideUnitCircle * m_cloudSpread;
                Vector3 finalPos = centerPos + new Vector3(randomOffset2D.x, randomOffset2D.y, 0f);

                float randomSizeFactor = Random.Range(1f - m_sizeVariation, 1f + m_sizeVariation);
                float finalSize = m_trailWidth * randomSizeFactor;
                float randomRotationDeg = Random.Range(0f, 360f);

                m_emitParams.position = finalPos;
                m_emitParams.startSize = finalSize;
                m_emitParams.rotation = randomRotationDeg;
                
                m_particleSystem.Emit(m_emitParams, 1);
            }
        }

        private void UpdateColliderWithDynamicTail(Vector3 currentPlayerPos)
        {
            if (m_trailCollider == null) return;

            if (!m_trailCollider.isTrigger)
            {
                m_trailCollider.isTrigger = true;
            }

            if (!m_trailCollider.enabled)
            {
                m_trailCollider.enabled = true;
            }

            int idx = m_head;
            m_colliderPointsCache.Clear();

            for (int i = 0; i < m_pointCount; i++)
            {
                Vector3 worldPos = m_points[idx].Position;
                Vector2 localPos = transform.InverseTransformPoint(worldPos);
                m_colliderPointsCache.Add(localPos);

                idx = (idx + 1) % m_maxTrailPoints;
            }
            
            Vector2 localCurrentPos = transform.InverseTransformPoint(currentPlayerPos);
            m_colliderPointsCache.Add(localCurrentPos);
            
            m_trailCollider.SetPoints(m_colliderPointsCache);
        }

        #endregion
    }
}