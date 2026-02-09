using System;
using System.Collections.Generic;
using UnityEngine;
using InGame.Mob.MobBase;
using InGame.Manager;
using InGame.Weapon.Base;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 플레이어의 이동 궤적을 따라 냄새 흔적을 남기고 데미지를 입히는 컨트롤러입니다.
    /// </summary>
    public class SmellWeaponController : WeaponControllerBase
    {
        #region 내부 구조체

        private struct TrailPoint
        {
            public Vector3 Position;
            public float CreationTime;
        }

        #endregion

        #region 내부 상태 및 변수

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

        #region 초기화 및 해제

        /// <summary>
        /// 무기를 초기화하고 궤적 데이터 및 파티클 시스템을 설정합니다.
        /// </summary>
        public override void Init(WeaponDataSO data, Transform ownerTransform, Func<Vector3> getTargetDirection)
        {
            base.Init(data, ownerTransform, getTargetDirection);

            // 1. 모델 인스턴스화 및 컴포넌트 캐싱
            if (data.ProjectilePrefab != null)
            {
                m_weaponModelInstance = UnityEngine.Object.Instantiate(data.ProjectilePrefab, ownerTransform);
                m_weaponModelInstance.transform.localPosition = Vector3.zero;
                m_weaponModelInstance.transform.localRotation = Quaternion.identity;
                
                m_selfTransform = m_weaponModelInstance.transform;
                m_particleSystem = m_weaponModelInstance.GetComponentInChildren<ParticleSystem>();
                m_trailCollider = m_weaponModelInstance.GetComponentInChildren<EdgeCollider2D>();
                
                // View 연결 (충돌 이벤트 수신용)
                var view = m_weaponModelInstance.GetComponent<SmellWeaponView>();
                if (view != null)
                {
                    view.Init(this); // Initialize -> Init
                }
                else
                {
                    view = m_weaponModelInstance.AddComponent<SmellWeaponView>();
                    view.Init(this);
                }
            }

            if (m_particleSystem == null || m_trailCollider == null)
            {
                LogManager.LogError($"[SmellWeaponController] 필수 컴포넌트 누락: {data.WeaponName}");
            }

            // 2. 튜닝 데이터 설정
            m_maxTrailPoints = 50;
            m_pointSpacing = 0.5f;
            m_trailWidth = 1.0f;
            m_trailLifetime = data.BaseDuration > 0 ? data.BaseDuration : 5.0f;
            
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

            InitTrail();
        }

        /// <summary>
        /// 궤적 판정을 위한 충돌체 및 버퍼를 초기화합니다.
        /// </summary>
        private void InitTrail()
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

        /// <summary>
        /// 궤적 데이터를 초기 상태로 리셋합니다.
        /// </summary>
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

        /// <summary>
        /// 무기 해제 시 모델을 파괴하고 데이터를 정리합니다.
        /// </summary>
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

        #region 업데이트 루프

        public override void OnUpdate(float deltaTime)
        {
            if (GameManager.Instance.State != null && !GameManager.Instance.State.IsPlaying)
            {
                if (m_particleSystem != null && m_particleSystem.isPlaying)
                {
                    m_particleSystem.Stop();
                }
                return;
            }

            if (!IsEnemyPresent)
            {
                if (m_particleSystem != null && m_particleSystem.isPlaying)
                {
                    m_particleSystem.Stop();
                }
                return;
            }

            if (m_particleSystem != null && !m_particleSystem.isPlaying)
            {
                m_particleSystem.Play();
            }

            if (m_playerTransform == null || m_trailCollider == null)
            {
                return;
            }

            float currentTime = Time.time;

            // 1. 유효 시간이 지난 궤적 포인트 제거 (Head 이동)
            while (m_pointCount > 0 && currentTime - m_points[m_head].CreationTime > m_trailLifetime)
            {
                m_head = (m_head + 1) % m_maxTrailPoints;
                m_pointCount--;
            }

            Vector3 currentPos = m_playerTransform.position;
            int lastIndex = (m_tail - 1 + m_maxTrailPoints) % m_maxTrailPoints;
            bool shouldAdd = m_pointCount == 0 || Vector3.Distance(m_points[lastIndex].Position, currentPos) > m_pointSpacing;

            // 2. 새로운 궤적포인트 추가 (Tail 이동)
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

            // 3. 콜라이더 외형 업데이트 (플레이어 현재 위치 포함)
            if (m_pointCount > 0 || playerMoved)
            {
                UpdateColliderWithDynamicTail(currentPos);
            }

            m_lastFramePlayerPos = currentPos;
        }

        protected override void ExecuteAttack(Vector3 direction)
        {
            // 루프 기반 자동 실행
        }

        #endregion

        #region 데미지 처리 인터페이스

        /// <summary>
        /// 트리거와 겹친 적들에게 주기적으로 데미지를 입힙니다.
        /// 외부 View(SmellWeaponView)에서 호출됩니다.
        /// </summary>
        public void ProcessTriggerDamage(Collider2D other)
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

        #region 비주얼 및 물리 갱신

        /// <summary>
        /// 특정 좌표에 독구름 파티클을 방출합니다.
        /// </summary>
        private void EmitPoisonCloud(Vector3 centerPos)
        {
            if (m_particleSystem == null)
            {
                return;
            }

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

        /// <summary>
        /// 궤적 포인트를 기반으로 EdgeCollider2D의 정점을 동적으로 재설정합니다.
        /// </summary>
        private void UpdateColliderWithDynamicTail(Vector3 currentPlayerPos)
        {
            if (m_trailCollider == null || m_selfTransform == null)
            {
                return;
            }

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
