using UnityEngine;
using System.Collections.Generic;
using InGame.Mob.MobBase;
using InGame.Managers;

namespace InGame.Weapon
{
    /// <summary>
    /// [설명]: 향기로운 발냄새(Smell) 무기의 독구름 흔적을 생성하고 관리하는 컴포넌트입니다.
    /// 원형 버퍼(Circular Buffer)를 사용하여 일정 시간 동안 유지되는 궤적 포인트와 충돌체를 관리합니다.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem), typeof(EdgeCollider2D))]
    public class PoisonTrailEmitter : MonoBehaviour
    {
        #region 내부 자료구조

        /// <summary>
        /// [설명]: 궤적의 한 지점을 나타내는 구조체입니다.
        /// </summary>
        private struct TrailPoint
        {
            public Vector3 Position;
            public float CreationTime;
        }

        #endregion

        #region 설정 데이터

        [Header("기본 흔적 설정")]
        [Tooltip("최대 유지 가능한 궤적 포인트 개수")]
        [SerializeField] private int m_maxTrailPoints = 50;

        [Tooltip("포인트 간의 최소 간격 (이 거리만큼 이동해야 새 포인트 생성)")]
        [SerializeField] private float m_pointSpacing = 0.8f;

        [Tooltip("흔적의 너비 (파티클 크기 및 판정에 영향)")]
        [SerializeField] private float m_trailWidth = 1.5f;

        [Tooltip("흔적의 지속 시간 (초)")]
        [SerializeField] private float m_trailLifetime = 5f;

        [Header("시각 효과(파티클) 설정")]
        [Tooltip("포인트당 방출할 파티클 개수")]
        [SerializeField] [Range(1, 10)] private int m_cloudDensity = 3; 

        [Tooltip("파티클이 퍼지는 범위")]
        [SerializeField] [Range(0f, 1f)] private float m_cloudSpread = 0.3f;

        [Tooltip("파티클 크기의 랜덤 변동폭")]
        [SerializeField] [Range(0f, 0.5f)] private float m_sizeVariation = 0.2f;

        #endregion

        #region 내부 상태 및 변수

        // 컴포넌트 및 트랜스폼 참조
        private ParticleSystem m_particleSystem;
        private ParticleSystem.EmitParams m_emitParams;
        private EdgeCollider2D m_trailCollider;
        private Transform m_playerTransform;

        // 원형 버퍼(Circular Buffer) 관리 변수
        private TrailPoint[] m_points;
        private int m_head;
        private int m_tail;
        private int m_pointCount;

        // 캐싱 및 최적화
        private readonly List<Vector2> m_colliderPointsCache = new List<Vector2>(100);
        private readonly Dictionary<int, float> m_damageCooldowns = new Dictionary<int, float>();
        private Vector3 m_lastFramePlayerPos;

        // 전투 스탯
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
            InitTrailData();
        }

        private void Update()
        {
            // 게임 플레이 중이 아닐 경우 업데이트 중단
            if (GameManager.Instance.State != null && !GameManager.Instance.State.IsPlaying) return;
            if (m_playerTransform == null || m_trailCollider == null) return;

            float currentTime = Time.time;

            // 1. 수명이 다한 포인트 제거 (Head 이동)
            while (m_pointCount > 0 && currentTime - m_points[m_head].CreationTime > m_trailLifetime)
            {
                m_head = (m_head + 1) % m_maxTrailPoints;
                m_pointCount--;
            }

            // 2. 새로운 포인트 추가 및 파티클 방출 (Tail 이동)
            Vector3 currentPos = m_playerTransform.position;
            int lastIndex = (m_tail - 1 + m_maxTrailPoints) % m_maxTrailPoints;
            bool shouldAdd = m_pointCount == 0 || Vector3.Distance(m_points[lastIndex].Position, currentPos) > m_pointSpacing;

            if (shouldAdd)
            {
                // 버퍼가 꽉 찼다면 가장 오래된 포인트를 덮어씀
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

            // 3. 콜라이더 외형 동적 갱신
            bool playerMoved = Vector3.Distance(currentPos, m_lastFramePlayerPos) > Mathf.Epsilon;
            if (m_pointCount > 0 || playerMoved)
            {
                UpdateColliderShape(currentPos);
            }

            m_lastFramePlayerPos = currentPos;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (GameManager.Instance.State != null && !GameManager.Instance.State.IsPlaying) return;
            if (!other.CompareTag("Mob")) return;

            int id = other.gameObject.GetInstanceID();
            
            // 적별 타격 쿨다운 체크
            if (!m_damageCooldowns.TryGetValue(id, out float nextDamageTime) || Time.time >= nextDamageTime)
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
            ResetTrailState();
        }

        #endregion

        #region 초기화 및 제어

        /// <summary>
        /// [설명]: 무기 정보를 기반으로 이미터의 초기 스탯을 설정합니다.
        /// </summary>
        public void Init(float damage, float stunTime, float coolTime)
        {
            m_damage = damage;
            m_stunTime = stunTime;
            m_coolTime = coolTime;
        }

        /// <summary>
        /// [설명]: 레벨업 등 스탯이 변경되었을 때 런타임 수치를 갱신합니다.
        /// </summary>
        public void UpdateStats(float damage, float stunTime, float coolTime)
        {
            m_damage = damage;
            m_stunTime = stunTime;
            m_coolTime = coolTime;
        }

        /// <summary>
        /// [설명]: 궤적 데이터 구조 및 초기 물리 설정을 수행합니다.
        /// </summary>
        private void InitTrailData()
        {
            m_emitParams = new ParticleSystem.EmitParams();

            if (m_trailCollider != null)
            {
                m_trailCollider.isTrigger = true;
                m_trailCollider.enabled = true;
            }

            if (m_points == null || m_points.Length != m_maxTrailPoints)
            {
                m_points = new TrailPoint[m_maxTrailPoints];
            }

            ResetTrailState();
        }

        /// <summary>
        /// [설명]: 현재 관리 중인 모든 궤적 데이터와 상태를 리셋합니다.
        /// </summary>
        private void ResetTrailState()
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
        /// [설명]: 특정 위치에 독구름 파티클을 뭉쳐서 방출합니다.
        /// </summary>
        private void EmitPoisonCloud(Vector3 centerPos)
        {
            if (m_particleSystem == null) return;

            for (int i = 0; i < m_cloudDensity; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * m_cloudSpread;
                Vector3 finalPos = centerPos + new Vector3(randomOffset.x, randomOffset.y, 0f);
                float sizeFactor = Random.Range(1f - m_sizeVariation, 1f + m_sizeVariation);

                m_emitParams.position = finalPos;
                m_emitParams.startSize = m_trailWidth * sizeFactor;
                m_emitParams.rotation = Random.Range(0f, 360f);

                m_particleSystem.Emit(m_emitParams, 1);
            }
        }

        /// <summary>
        /// [설명]: 저장된 궤적 포인트들을 이어 EdgeCollider2D의 정점을 실시간으로 갱신합니다.
        /// </summary>
        /// <param name="currentPlayerPos">플레이어의 현재 위치 (궤적의 끝점)</param>
        private void UpdateColliderShape(Vector3 currentPlayerPos)
        {
            if (m_trailCollider == null) return;

            m_colliderPointsCache.Clear();
            int index = m_head;

            // 1. 저장된 포인트들을 로컬 좌표로 변환하여 추가
            for (int i = 0; i < m_pointCount; i++)
            {
                m_colliderPointsCache.Add(transform.InverseTransformPoint(m_points[index].Position));
                index = (index + 1) % m_maxTrailPoints;
            }

            // 2. 현재 플레이어 위치를 마지막 점으로 추가하여 궤적 연결
            m_colliderPointsCache.Add(transform.InverseTransformPoint(currentPlayerPos));

            // 정점이 2개 이상일 때만 콜라이더 갱신
            if (m_colliderPointsCache.Count >= 2)
            {
                m_trailCollider.SetPoints(m_colliderPointsCache);
            }
        }

        #endregion
    }
}