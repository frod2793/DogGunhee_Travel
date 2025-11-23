using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

namespace DogGuns_Games.vamsir
{
    [RequireComponent(typeof(ParticleSystem), typeof(EdgeCollider2D))]
    public class WeaphonSmell : Weaphon_base
    {
        private struct TrailPoint
        {
            public Vector3 Position;
            public float CreationTime;
        }

        #region 인스펙터 필드

        [Header("기본 냄새 흔적 설정")]
        [Tooltip("흔적의 최대 길이")]
        [FormerlySerializedAs("maxTrailPoints")]
        [SerializeField] private int m_maxTrailPoints = 50;
        
        [Tooltip("새로운 포인트를 생성하기 위해 이동해야 하는 최소 거리")]
        [FormerlySerializedAs("pointSpacing")]
        [SerializeField] private float m_pointSpacing = 0.8f;
        
        [Tooltip("흔적의 기본 시각적 크기")]
        [FormerlySerializedAs("trailWidth")]
        [SerializeField] private float m_trailWidth = 1.5f;
        
        [Tooltip("흔적이 완전히 사라지기까지의 시간")]
        [FormerlySerializedAs("trailLifetime")]
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

        protected override void OnEnable()
        {
            base.OnEnable();
            if (VamserLikeGameManager.Instance != null)
            {
                m_playerTransform = VamserLikeGameManager.Instance.PlayerTransfrom();
                if (m_playerTransform != null)
                {
                    m_lastFramePlayerPos = m_playerTransform.position;
                }
            }
            InitializeTrail();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ResetTrailData();
        }

        private void Update()
        {
            if (m_playerTransform == null || !m_trailCollider) return;

            float currentTime = Time.time;

            // 1. 수명 다한 포인트 제거
            while (m_pointCount > 0 && currentTime - m_points[m_head].CreationTime > m_trailLifetime)
            {
                m_head = (m_head + 1) % m_maxTrailPoints;
                m_pointCount--;
            }

            Vector3 currentPos = m_playerTransform.position;

            // 2. 새 포인트 추가 판단
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

            // 3. 콜라이더 업데이트
            // 플레이어 움직임 감지
            bool playerMoved = Vector3.Distance(currentPos, m_lastFramePlayerPos) > Mathf.Epsilon;

            // 포인트가 있거나 플레이어가 움직였으면 꼬리를 갱신합니다.
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
                if (other.TryGetComponent(out VamserMobBase mob))
                {
                    if (!mob.IsDead)
                    {
                        mob.TakeDamage(attackPower, mobStunTime); 
                        m_damageCooldowns[id] = currentTime + coolTime;
                    }
                }
            }
        }

        #endregion

        #region 초기화 및 재설정

        private void InitializeTrail()
        {
            m_emitParams = new ParticleSystem.EmitParams{};
            
            // 콜라이더 트리거 설정 및 강제 활성화
            if (m_trailCollider != null)
            {
                // [강제 설정] 초기화 시 isTrigger를 무조건 true로 설정
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
                // [안전 장치] 리셋 시에도 isTrigger가 풀리지 않도록 재설정
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

        // 동적 꼬리를 포함한 콜라이더 업데이트
        private void UpdateColliderWithDynamicTail(Vector3 currentPlayerPos)
        {
            if (m_trailCollider == null) return;

            // [핵심 방어 코드] 매 업데이트마다 isTrigger 상태를 확인하고 강제로 true로 설정
            if (!m_trailCollider.isTrigger)
            {
                m_trailCollider.isTrigger = true;
            }

            // 혹시라도 꺼져있다면 켭니다.
            if (!m_trailCollider.enabled)
            {
                m_trailCollider.enabled = true;
            }

            int idx = m_head;
            m_colliderPointsCache.Clear();

            // 1. 기록된 과거의 점들을 추가
            for (int i = 0; i < m_pointCount; i++)
            {
                Vector3 worldPos = m_points[idx].Position;
                Vector2 localPos = transform.InverseTransformPoint(worldPos);
                m_colliderPointsCache.Add(localPos);

                idx = (idx + 1) % m_maxTrailPoints;
            }
            
            // 2. 현재 플레이어의 위치를 리스트의 가장 마지막 점으로 추가 (꼬리 만들기)
            Vector2 localCurrentPos = transform.InverseTransformPoint(currentPlayerPos);
            m_colliderPointsCache.Add(localCurrentPos);
            
            // 콜라이더에 최종 포인트 적용
            m_trailCollider.SetPoints(m_colliderPointsCache);
        }

        #endregion
    }
}