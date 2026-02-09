using UnityEngine;
using System.Collections.Generic;
using InGame.Mob.MobBase;
using InGame.Manager;

namespace InGame.Weapon
{
    /// <summary>
    /// 향기로운 발냄새 무기(Smell)의 독구름 흔적을 생성하고 관리하는 컴포넌트입니다.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem), typeof(EdgeCollider2D))]
    public class PoisonTrailEmitter : MonoBehaviour
    {
        #region 데이터 구조

        private struct TrailPoint
        {
            public Vector3 Position;
            public float CreationTime;
        }

        #endregion

        #region 설정 데이터

        [Header("기본 흔적 설정")]
        [SerializeField] private int m_maxTrailPoints = 50;
        [SerializeField] private float m_pointSpacing = 0.8f;
        [SerializeField] private float m_trailWidth = 1.5f;
        [SerializeField] private float m_trailLifetime = 5f;

        [Header("시각 효과(파티클) 설정")]
        [SerializeField] [Range(1, 10)] private int m_cloudDensity = 3; 
        [SerializeField] [Range(0f, 1f)] private float m_cloudSpread = 0.3f;
        [SerializeField] [Range(0f, 0.5f)] private float m_sizeVariation = 0.2f;

        #endregion

        #region 내부 상태 및 변수

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

        private float m_damage;
        private float m_stunTime;
        private float m_coolTime;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_particleSystem = GetComponent<ParticleSystem>();
            m_trailCollider = GetComponent<EdgeCollider2D>();
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null)
            {
                m_playerTransform = GameManager.Instance.PlayerTransfrom();
                if (m_playerTransform != null)
                {
                    m_lastFramePlayerPos = m_playerTransform.position;
                }
            }
            InitTrail();
        }

        private void Update()
        {
            if (GameManager.Instance.State != null && !GameManager.Instance.State.IsPlaying)
            {
                return;
            }

            if (m_playerTransform == null || !m_trailCollider)
            {
                return;
            }

            float currentTime = Time.time;

            // 수명이 다한 포인트 제거 (순환 큐)
            while (m_pointCount > 0 && currentTime - m_points[m_head].CreationTime > m_trailLifetime)
            {
                m_head = (m_head + 1) % m_maxTrailPoints;
                m_pointCount--;
            }

            Vector3 currentPos = m_playerTransform.position;
            int lastIndex = (m_tail - 1 + m_maxTrailPoints) % m_maxTrailPoints;
            bool shouldAdd = m_pointCount == 0 || Vector3.Distance(m_points[lastIndex].Position, currentPos) > m_pointSpacing;

            // 새로운 포인트 추가 및 파티클 방출
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

            // 플레이어가 이동 중이거나 포인트가 있을 때 콜라이더 갱신
            bool playerMoved = Vector3.Distance(currentPos, m_lastFramePlayerPos) > Mathf.Epsilon;
            if (m_pointCount > 0 || playerMoved)
            {
                UpdateColliderWithDynamicTail(currentPos);
            }
            m_lastFramePlayerPos = currentPos;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (GameManager.Instance.State != null && !GameManager.Instance.State.IsPlaying)
            {
                return;
            }

            if (!other.CompareTag("Mob"))
            {
                return;
            }

            int id = other.gameObject.GetInstanceID();
            
            // 타격 쿨다운 적용
            if (!m_damageCooldowns.TryGetValue(id, out float nextTime) || Time.time >= nextTime)
            {
                if (other.TryGetComponent(out MobBase mob) && !mob.IsDead)
                {
                    mob.TakeDamage(m_damage, m_stunTime);
                    m_damageCooldowns[id] = Time.time + m_coolTime;
                }
            }
        }

        private void OnDisable()
        {
            ResetTrailData();
        }

        #endregion

        #region 초기화 및 제어

        /// <summary>
        /// 무기 정보를 기반으로 이미터를 초기화합니다.
        /// </summary>
        public void Init(float damage, float stunTime, float coolTime)
        {
            m_damage = damage;
            m_stunTime = stunTime;
            m_coolTime = coolTime;
        }

        /// <summary>
        /// 레벨업 등 스탯이 변경되었을 때 호출됩니다.
        /// </summary>
        public void UpdateStats(float damage, float stunTime, float coolTime)
        {
            m_damage = damage;
            m_stunTime = stunTime;
            m_coolTime = coolTime;
        }

        private void InitTrail()
        {
            m_emitParams = new ParticleSystem.EmitParams { };
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

            if (m_particleSystem != null)
            {
                m_particleSystem.Clear();
            }

            if (m_trailCollider != null)
            {
                m_trailCollider.Reset();
                m_trailCollider.isTrigger = true;
            }
        }

        #endregion

        #region 연출 및 물리 로직

        /// <summary>
        /// 특정 위치에 독구름 파티클을 뭉쳐서 방출합니다.
        /// </summary>
        private void EmitPoisonCloud(Vector3 centerPos)
        {
            if (m_particleSystem == null)
            {
                return;
            }

            for (int i = 0; i < m_cloudDensity; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * m_cloudSpread;
                Vector3 finalPos = centerPos + new Vector3(randomOffset.x, randomOffset.y, 0f);
                float sizeVar = Random.Range(1f - m_sizeVariation, 1f + m_sizeVariation);

                m_emitParams.position = finalPos;
                m_emitParams.startSize = m_trailWidth * sizeVar;
                m_emitParams.rotation = Random.Range(0f, 360f);
                m_particleSystem.Emit(m_emitParams, 1);
            }
        }

        /// <summary>
        /// 추적 중인 포인트 목록을 기반으로 EdgeCollider의 모양을 갱신합니다.
        /// </summary>
        private void UpdateColliderWithDynamicTail(Vector3 currentPlayerPos)
        {
            if (m_trailCollider == null)
            {
                return;
            }

            m_colliderPointsCache.Clear();
            int idx = m_head;
            for (int i = 0; i < m_pointCount; i++)
            {
                m_colliderPointsCache.Add(transform.InverseTransformPoint(m_points[idx].Position));
                idx = (idx + 1) % m_maxTrailPoints;
            }

            // 플레이어의 현재 위치까지 선을 잇기 위해 추가
            m_colliderPointsCache.Add(transform.InverseTransformPoint(currentPlayerPos));

            if (m_colliderPointsCache.Count >= 2)
            {
                m_trailCollider.SetPoints(m_colliderPointsCache);
            }
        }

        #endregion
    }
}
