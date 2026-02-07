using System;
using System.Collections.Generic;
using UnityEngine;
using InGame.Mob.MobBase;
using InGame.Manager;
using InGame.Weapon.Base;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 냄새 흔적 공격을 담당하는 POCO 컨트롤러입니다.
    /// </summary>
    public class SmellWeaponController : WeaponControllerBase
    {
        private struct TrailPoint
        {
            public Vector3 Position;
            public float CreationTime;
        }

        #region 설정 데이터

        private int m_maxTrailPoints;
        private float m_pointSpacing;
        private float m_trailWidth;
        private float m_trailLifetime;
        private int m_cloudDensity;
        private float m_cloudSpread;
        private float m_sizeVariation;
        private ParticleSystem m_particleSystem;
        private EdgeCollider2D m_trailCollider;
        private Transform m_playerTransform;
        private Transform m_selfTransform;

        #endregion

        #region 내부 상태

        private ParticleSystem.EmitParams m_emitParams;
        private TrailPoint[] m_points;
        private int m_head;
        private int m_tail;
        private int m_pointCount;

        private readonly List<Vector2> m_colliderPointsCache = new List<Vector2>(100);
        private readonly Dictionary<int, float> m_damageCooldowns = new Dictionary<int, float>();

        private Vector3 m_lastFramePlayerPos;

        #endregion

        #region 초기화

        /// <summary>
        /// SmellWeaponController를 초기화합니다.
        /// </summary>
        public void Init(
            WeaponDataSO data,
            Transform ownerTransform,
            Func<Vector3> getTargetDirection,
            Transform selfTransform,
            ParticleSystem particleSystem,
            EdgeCollider2D trailCollider,
            int maxTrailPoints = 50,
            float pointSpacing = 0.8f,
            float trailWidth = 1.5f,
            float trailLifetime = 5f,
            int cloudDensity = 3,
            float cloudSpread = 0.3f,
            float sizeVariation = 0.2f)
        {
            base.Init(data, ownerTransform, getTargetDirection);

            m_selfTransform = selfTransform;
            m_particleSystem = particleSystem;
            m_trailCollider = trailCollider;
            m_maxTrailPoints = maxTrailPoints;
            m_pointSpacing = pointSpacing;
            m_trailWidth = trailWidth;
            m_trailLifetime = trailLifetime;
            m_cloudDensity = cloudDensity;
            m_cloudSpread = cloudSpread;
            m_sizeVariation = sizeVariation;

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

        private void InitializeTrail()
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

        #region IWeaponController 구현

        public override void OnUpdate(float deltaTime)
        {
            // 게임 상태 체크
            if (PlayStateManager.instance != null && !PlayStateManager.instance.IsPlaying) return;

            if (m_playerTransform == null || m_trailCollider == null) return;

            float currentTime = Time.time;

            // 오래된 포인트 제거
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

        protected override void ExecuteAttack(Vector3 direction)
        {
            // Smell은 자동 흔적 생성이므로 수동 Attack은 무시됩니다.
        }

        public override void Dispose()
        {
            ResetTrailData();
        }

        #endregion

        #region 데미지 처리 (외부에서 호출)

        /// <summary>
        /// 트리거 충돌 시 데미지 처리를 위한 메서드입니다.
        /// View 측에서 OnTriggerStay2D에서 호출합니다.
        /// </summary>
        public void ProcessTriggerDamage(Collider2D other)
        {
            // 게임 상태 체크
            if (PlayStateManager.instance != null && !PlayStateManager.instance.IsPlaying) return;

            if (!other.CompareTag("Mob")) return;

            int id = other.gameObject.GetInstanceID();
            float currentTime = Time.time;

            if (!m_damageCooldowns.TryGetValue(id, out float nextTime) || currentTime >= nextTime)
            {
                if (other.TryGetComponent(out MobBase mob) && !mob.IsDead)
                {
                    mob.TakeDamage(m_runtimeStats.AttackPower, m_runtimeStats.MobStunTime);
                    m_damageCooldowns[id] = currentTime + m_runtimeStats.CoolTime;
                }
            }
        }

        #endregion

        #region 비주얼 및 물리 업데이트

        private void EmitPoisonCloud(Vector3 centerPos)
        {
            if (m_particleSystem == null) return;

            for (int i = 0; i < m_cloudDensity; i++)
            {
                Vector2 randomOffset2D = UnityEngine.Random.insideUnitCircle * m_cloudSpread;
                Vector3 finalPos = centerPos + new Vector3(randomOffset2D.x, randomOffset2D.y, 0f);

                float randomSizeFactor = UnityEngine.Random.Range(1f - m_sizeVariation, 1f + m_sizeVariation);
                float finalSize = m_trailWidth * randomSizeFactor;
                float randomRotationDeg = UnityEngine.Random.Range(0f, 360f);

                m_emitParams.position = finalPos;
                m_emitParams.startSize = finalSize;
                m_emitParams.rotation = randomRotationDeg;

                m_particleSystem.Emit(m_emitParams, 1);
            }
        }

        private void UpdateColliderWithDynamicTail(Vector3 currentPlayerPos)
        {
            if (m_trailCollider == null || m_selfTransform == null) return;

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
                Vector2 localPos = m_selfTransform.InverseTransformPoint(worldPos);
                m_colliderPointsCache.Add(localPos);

                idx = (idx + 1) % m_maxTrailPoints;
            }

            Vector2 localCurrentPos = m_selfTransform.InverseTransformPoint(currentPlayerPos);
            m_colliderPointsCache.Add(localCurrentPos);

            m_trailCollider.SetPoints(m_colliderPointsCache);
        }

        #endregion
    }
}
