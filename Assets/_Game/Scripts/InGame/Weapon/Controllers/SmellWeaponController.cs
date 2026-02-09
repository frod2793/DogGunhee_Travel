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
        
        private GameObject m_weaponModelInstance;

        private readonly List<Vector2> m_colliderPointsCache = new List<Vector2>(100);
        private readonly Dictionary<int, float> m_damageCooldowns = new Dictionary<int, float>();

        private Vector3 m_lastFramePlayerPos;

        #endregion

        #region 초기화

        public override void Init(
            WeaponDataSO data,
            Transform ownerTransform,
            Func<Vector3> getTargetDirection)
        {
            base.Init(data, ownerTransform, getTargetDirection);

            // 1. 모델 인스턴스화
            if (data.ProjectilePrefab != null)
            {
                // 부모를 Player로 설정하여 따라다니게 함 (Smell은 꼬리에 남으므로 플레이어 자식이거나 따라다녀야 함)
                // 하지만 TrailRenderer나 ParticleSystem은 World Space여야 할 수도 있음.
                // 기존 로직: m_selfTransform 사용.
                // 모델을 생성하고 위치 동기화.
                m_weaponModelInstance = UnityEngine.Object.Instantiate(data.ProjectilePrefab, ownerTransform);
                m_weaponModelInstance.transform.localPosition = Vector3.zero;
                m_weaponModelInstance.transform.localRotation = Quaternion.identity;
                
                m_selfTransform = m_weaponModelInstance.transform;
                m_particleSystem = m_weaponModelInstance.GetComponentInChildren<ParticleSystem>();
                m_trailCollider = m_weaponModelInstance.GetComponentInChildren<EdgeCollider2D>();
                
                // 3. View 연결 (충돌 이벤트 수신용)
                var view = m_weaponModelInstance.GetComponent<SmellWeaponView>();
                if (view != null)
                {
                    view.Initialize(this);
                }
                else
                {
                    // View가 없으면 프리팹 루트에 자동 추가 시도 (임시 호환성)
                    view = m_weaponModelInstance.AddComponent<SmellWeaponView>();
                    view.Initialize(this);
                    LogManager.Log("[SmellWeaponController] Prefab에 SmellWeaponView가 없어 런타임에 추가했습니다.", LogManager.LogCategory.Weapon);
                }
            }

            if (m_particleSystem == null || m_trailCollider == null)
            {
                LogManager.LogError($"[SmellWeaponController] 프리팹에 필수 컴포넌트(ParticleSystem, EdgeCollider2D)가 누락되었습니다: {data.WeaponName}");
            }

            // 2. 튜닝 데이터 설정 (기본값)
            m_maxTrailPoints = 50;
            m_pointSpacing = 0.5f;
            m_trailWidth = 1.0f;
            m_trailLifetime = data.BaseDuration > 0 ? data.BaseDuration : 5.0f;
            
            // TODO: 추후 WeaponPoolManager에서 View 데이터 가져오기 구현 가능
            m_cloudDensity = 3;
            m_cloudSpread = 0.3f;
            m_sizeVariation = 0.2f;

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
            // 게임이 플레이 상태가 아니면 중단
            if (GameManager.Instance.State != null && !GameManager.Instance.State.IsPlaying)
            {
                if (m_particleSystem != null && m_particleSystem.isPlaying) m_particleSystem.Stop();
                return;
            }

            // [Optimization] 적이 없으면 흔적 생성 중단 (이동은 계속됨)
            if (!IsEnemyPresent) // Assuming IsEnemyPresent is defined elsewhere or will be added.
            {
                if (m_particleSystem != null && m_particleSystem.isPlaying) m_particleSystem.Stop();
                return;
            }

            // 파티클 시스템 관리
            if (m_particleSystem != null && !m_particleSystem.isPlaying)
            {
                m_particleSystem.Play();
            }

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
            
            if (m_weaponModelInstance != null)
            {
                UnityEngine.Object.Destroy(m_weaponModelInstance);
                m_weaponModelInstance = null;
            }
            
            base.Dispose();
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
            if (GameManager.Instance.State != null && !GameManager.Instance.State.IsPlaying) return;

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
