using System;
using System.Collections.Generic;
using UnityEngine;
using InGame.Mob.MobBase;
using InGame.Manager;
using InGame.Weapon.Base;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 플레이어의 이동 경로를 따라 독가스(Smell) 궤적을 남기는 무기 컨트롤러입니다.
    /// <br/> 원형 버퍼(Circular Buffer)를 사용하여 일정 시간 동안 유지되는 궤적 포인트와 충돌체를 관리합니다.
    /// </summary>
    public class SmellWeaponController : WeaponControllerBase
    {
        #region 1. 내부 자료구조 (Data Structures)

        /// <summary>
        /// 궤적의 한 지점을 나타내는 구조체입니다.
        /// </summary>
        private struct TrailPoint
        {
            /// <summary>월드 좌표 위치</summary>
            public Vector3 Position;
            /// <summary>생성된 시간 (Time.time)</summary>
            public float CreationTime;
        }

        #endregion

        #region 2. 내부 변수 및 컴포넌트 (State & Components)

        // 설정 변수 (Init 시 설정됨)
        private int m_maxTrailPoints;
        private float m_pointSpacing;
        private float m_trailWidth;
        private float m_trailLifetime;
        
        // 비주얼 설정
        private int m_cloudDensity;
        private float m_cloudSpread;
        private float m_sizeVariation;

        // 컴포넌트 참조
        private GameObject m_weaponModelInstance;
        private Transform m_selfTransform;
        private Transform m_playerTransform;
        private ParticleSystem m_particleSystem;
        private EdgeCollider2D m_trailCollider;

        // 런타임 상태 (원형 버퍼 관리)
        private TrailPoint[] m_points; // 궤적 포인트 버퍼
        private int m_head;            // 가장 오래된 포인트 인덱스 (삭제 예정)
        private int m_tail;            // 가장 최근 포인트 인덱스 (추가 위치)
        private int m_pointCount;      // 현재 활성화된 포인트 개수

        // 캐싱 및 최적화
        private ParticleSystem.EmitParams m_emitParams;
        private Vector3 m_lastFramePlayerPos;
        private readonly List<Vector2> m_colliderPointsCache = new List<Vector2>(100);
        private readonly Dictionary<int, float> m_damageCooldowns = new Dictionary<int, float>();

        #endregion

        #region 3. 초기화 및 해제 (Init & Dispose)

        /// <summary>
        /// 무기를 초기화하고 궤적 관리 시스템을 설정합니다.
        /// </summary>
        public override void Init(WeaponDataSO data, Transform ownerTransform,
            InGame.ObjectPool.WeaponPoolManager poolManager, Func<Vector3> getTargetDirection)
        {
            base.Init(data, ownerTransform, poolManager, getTargetDirection);

            // 1. 모델 인스턴스화 및 컴포넌트 캐싱
            if (data.ProjectilePrefab != null)
            {
                m_weaponModelInstance = UnityEngine.Object.Instantiate(data.ProjectilePrefab, ownerTransform);
                m_weaponModelInstance.transform.localPosition = Vector3.zero;
                m_weaponModelInstance.transform.localRotation = Quaternion.identity;

                m_selfTransform = m_weaponModelInstance.transform;
                m_particleSystem = m_weaponModelInstance.GetComponentInChildren<ParticleSystem>();
                m_trailCollider = m_weaponModelInstance.GetComponentInChildren<EdgeCollider2D>();

                // View 컴포넌트 연결 (충돌 이벤트 수신용)
                var view = m_weaponModelInstance.GetComponent<SmellWeaponView>();
                if (view == null)
                {
                    view = m_weaponModelInstance.AddComponent<SmellWeaponView>();
                }
                view.Init(this);
            }

            if (m_particleSystem == null || m_trailCollider == null)
            {
                LogManager.LogError($"[SmellWeaponController] 필수 컴포넌트(ParticleSystem/EdgeCollider2D) 누락: {data.WeaponName}", LogManager.LogCategory.Weapon);
            }

            // 2. 튜닝 데이터 설정 (하드코딩된 값은 추후 View나 DataSO로 이동 권장)
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

            // 3. 런타임 데이터 초기화
            InitTrailData();
        }

        /// <summary>
        /// 궤적 데이터 구조와 물리 설정을 초기화합니다.
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
        /// 궤적 상태를 완전히 리셋합니다. (재사용 또는 해제 시)
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

        public override void Dispose()
        {
            ResetTrailState();

            if (m_weaponModelInstance != null)
            {
                UnityEngine.Object.Destroy(m_weaponModelInstance);
                m_weaponModelInstance = null;
            }

            base.Dispose();
        }

        #endregion

        #region 4. 업데이트 루프 (Update Loop)

        public override void OnUpdate(float deltaTime)
        {
            // 1. 게임 상태 및 적 존재 여부 체크
            bool isGamePlaying = GameManager.Instance.State != null && GameManager.Instance.State.IsPlaying;
            if (!isGamePlaying || !IsEnemyPresent)
            {
                if (m_particleSystem != null && m_particleSystem.isPlaying)
                {
                    m_particleSystem.Stop();
                }
                return;
            }

            // 파티클 시스템 재생 재개
            if (m_particleSystem != null && !m_particleSystem.isPlaying)
            {
                m_particleSystem.Play();
            }

            if (m_playerTransform == null || m_trailCollider == null) return;

            float currentTime = Time.time;

            // 2. 수명(Lifetime)이 다 된 궤적 포인트 제거 (Head 이동)
            // Head가 가리키는 포인트가 너무 오래되었으면 제거
            while (m_pointCount > 0 && currentTime - m_points[m_head].CreationTime > m_trailLifetime)
            {
                m_head = (m_head + 1) % m_maxTrailPoints;
                m_pointCount--;
            }

            // 3. 플레이어 이동에 따른 새 궤적 포인트 추가 (Tail 이동)
            Vector3 currentPos = m_playerTransform.position;
            
            // 마지막으로 추가된 포인트(Tail의 직전)와 현재 위치 거리 비교
            int lastIndex = (m_tail - 1 + m_maxTrailPoints) % m_maxTrailPoints;
            bool shouldAddPoint = m_pointCount == 0 ||
                                  Vector3.Distance(m_points[lastIndex].Position, currentPos) > m_pointSpacing;

            if (shouldAddPoint)
            {
                // 버퍼가 꽉 찼다면 가장 오래된(Head) 포인트를 강제로 덮어씀
                if (m_pointCount == m_maxTrailPoints)
                {
                    m_head = (m_head + 1) % m_maxTrailPoints;
                    m_pointCount--;
                }

                // 새 포인트 기록
                m_points[m_tail] = new TrailPoint { Position = currentPos, CreationTime = currentTime };
                m_tail = (m_tail + 1) % m_maxTrailPoints;
                m_pointCount++;

                // 시각 효과(파티클) 방출
                EmitPoisonCloud(currentPos);
            }

            // 4. 물리 콜라이더 갱신
            // 포인트가 변경되었거나, 플레이어가 미세하게라도 움직였으면 콜라이더 모양 업데이트
            bool playerMoved = Vector3.Distance(currentPos, m_lastFramePlayerPos) > Mathf.Epsilon;
            
            if (m_pointCount > 0 || playerMoved)
            {
                UpdateColliderShape(currentPos);
            }

            m_lastFramePlayerPos = currentPos;
        }

        #endregion

        #region 5. 공격 및 데미지 로직 (Attack & Damage)

        protected override void ExecuteAttack(Vector3 direction)
        {
            // Smell 무기는 플레이어 이동에 따라 자동으로 흔적을 남기므로
            // 명시적인 공격(ExecuteAttack) 호출은 무시하거나 처리하지 않음
        }

        /// <summary>
        /// 콜라이더 트리거와 충돌한 적에게 데미지를 입힙니다.
        /// <br/> SmellWeaponView의 OnTriggerStay2D 이벤트에서 호출됩니다.
        /// </summary>
        /// <param name="other">충돌한 객체의 Collider2D</param>
        public void ProcessTriggerDamage(Collider2D other)
        {
            if (GameManager.Instance.State != null && !GameManager.Instance.State.IsPlaying) return;
            if (!other.CompareTag("Mob")) return;

            int id = other.gameObject.GetInstanceID();
            float currentTime = Time.time;

            // 쿨타임 체크 (TryGetValue로 조회 및 검사)
            if (!m_damageCooldowns.TryGetValue(id, out float nextDamageTime) || currentTime >= nextDamageTime)
            {
                if (other.TryGetComponent(out MobBase mob) && !mob.IsDead)
                {
                    mob.TakeDamage(m_runtimeStats.AttackPower, m_runtimeStats.MobStunTime);
                    
                    // 다음 데미지 시간 설정
                    m_damageCooldowns[id] = currentTime + m_runtimeStats.CoolTime;
                }
            }
        }

        #endregion

        #region 6. 비주얼 및 물리 유틸리티 (Visuals & Physics)

        /// <summary>
        /// 지정된 위치에 독구름 파티클을 랜덤하게 방출합니다.
        /// </summary>
        private void EmitPoisonCloud(Vector3 centerPos)
        {
            if (m_particleSystem == null) return;

            // 설정된 밀도만큼 파티클 생성
            for (int i = 0; i < m_cloudDensity; i++)
            {
                // 랜덤 위치 및 크기 계산
                Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * m_cloudSpread;
                Vector3 finalPos = centerPos + new Vector3(randomOffset.x, randomOffset.y, 0f);

                float sizeFactor = UnityEngine.Random.Range(1f - m_sizeVariation, 1f + m_sizeVariation);
                float finalSize = m_trailWidth * sizeFactor;
                float rotation = UnityEngine.Random.Range(0f, 360f);

                // 파티클 파라미터 설정 및 방출
                m_emitParams.position = finalPos;
                m_emitParams.startSize = finalSize;
                m_emitParams.rotation = rotation;

                m_particleSystem.Emit(m_emitParams, 1);
            }
        }

        /// <summary>
        /// 현재 활성화된 궤적 포인트들을 이어 EdgeCollider2D의 형태를 갱신합니다.
        /// </summary>
        /// <param name="currentPlayerPos">플레이어의 현재 위치 (궤적의 끝점)</param>
        private void UpdateColliderShape(Vector3 currentPlayerPos)
        {
            if (m_trailCollider == null || m_selfTransform == null) return;

            // 콜라이더 상태 보장
            if (!m_trailCollider.isTrigger) m_trailCollider.isTrigger = true;
            if (!m_trailCollider.enabled) m_trailCollider.enabled = true;

            m_colliderPointsCache.Clear();
            int index = m_head;

            // 1. 저장된 궤적 포인트 추가
            for (int i = 0; i < m_pointCount; i++)
            {
                // 월드 좌표를 로컬 좌표로 변환 (Weapon 모델 기준)
                Vector3 worldPos = m_points[index].Position;
                Vector2 localPos = m_selfTransform.InverseTransformPoint(worldPos);
                m_colliderPointsCache.Add(localPos);

                index = (index + 1) % m_maxTrailPoints;
            }

            // 2. 현재 플레이어 위치를 마지막 점으로 추가하여 궤적이 끊기지 않게 함
            Vector2 localCurrentPos = m_selfTransform.InverseTransformPoint(currentPlayerPos);
            m_colliderPointsCache.Add(localCurrentPos);

            // 3. 콜라이더 갱신
            m_trailCollider.SetPoints(m_colliderPointsCache);
        }

        #endregion
    }
}