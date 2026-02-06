using UnityEngine;
using System.Collections.Generic;
using InGame.Mob.MobBase;
using InGame.Weaphon.Base;

namespace InGame.Weaphon
{
    [RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
    [RequireComponent(typeof(TrailRenderer), typeof(Animator))]
    public class PearlProjectile : MonoBehaviour
    {
        #region Static Instance
        
        public static PearlProjectile Instance { get; private set; }

        #endregion

        #region 내부 변수

        private Vector3 m_velocity;
        private float m_damage;
        private float m_stunTime;
        private bool m_isEvolved;
        
        public float CurrentSpeed => m_velocity.magnitude;

        private Camera m_mainCamera;
        private TrailRenderer m_trailRenderer;
        private Animator m_animator;
        private Renderer m_renderer; 
        private float m_radius = 0.5f; 

        private readonly Dictionary<int, float> m_hitCooldowns = new Dictionary<int, float>();
        private const float k_HitCooldown = 0.5f;

        private static readonly int k_AnimTriggerLv1 = Animator.StringToHash("Level1");
        private static readonly int k_AnimTriggerLv2 = Animator.StringToHash("Level2");

        #endregion

        #region 인스펙터 설정 (Visual)

        [Header("Trail Settings")]
        [SerializeField] private float m_trailTime = 0.3f;
        [SerializeField] private float m_trailStartWidth = 0.4f;
        [SerializeField] private float m_trailEndWidth = 0.0f;

        [Header("Level Colors")]
        [SerializeField] private Color m_trailColorLv1 = new Color(1f, 1f, 1f, 0.5f);
        [SerializeField] private Color m_trailColorLv2 = new Color(1f, 0f, 1f, 0.5f);

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_mainCamera = Camera.main;
            m_trailRenderer = GetComponent<TrailRenderer>();
            m_animator = GetComponent<Animator>();
            m_renderer = GetComponent<Renderer>(); 
            if (m_renderer != null) m_radius = m_renderer.bounds.extents.x;

            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;

            SetupTrailBase();
        }

        private void OnEnable()
        {
            m_mainCamera = Camera.main; // 카메라 참조 갱신

            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PearlProjectile] 두 개 이상의 진주가 활성화되려 합니다. 기존 인스턴스를 파괴합니다.");
                Destroy(Instance.gameObject);
            }
            Instance = this;

            m_hitCooldowns.Clear();
            if (m_trailRenderer != null) m_trailRenderer.Clear();
        }

        private void OnDisable()
        {
            Debug.Log($"[PearlProjectile] OnDisable called. GameObject active: {gameObject.activeSelf}");
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnDestroy()
        {
            Debug.Log("[PearlProjectile] OnDestroy called.");
        }

        private void LateUpdate()
        {
            // 1. 이동 및 Z축 강제 고정
            Vector3 nextPos = transform.position + m_velocity * Time.deltaTime;
            nextPos.z = 0f;
            transform.position = nextPos;

            transform.Rotate(0, 0, -360f * Time.deltaTime);
            BounceOffScreenEdges();
        }

        #endregion

        #region 초기화 및 상태 업데이트

        /// <summary>
        /// PearlProjectile을 초기화합니다.
        /// </summary>
        /// <param name="damage">공격력</param>
        /// <param name="stunTime">스턴 시간</param>
        /// <param name="speed">이동 속도</param>
        /// <param name="isEvolved">진화 여부</param>
        /// <param name="initialVelocity">초기 속도 벡터</param>
        public void Initialize(float damage, float stunTime, float speed, bool isEvolved, Vector3 initialVelocity)
        {
            m_velocity = initialVelocity;
            m_velocity.z = 0f; // Velocity의 Z축 성분 제거
            UpdateState(damage, stunTime, speed, isEvolved);
            transform.rotation = Quaternion.identity;
        }
        
        /// <summary>
        /// 진주의 상태(스탯 및 비주얼)를 업데이트합니다.
        /// </summary>
        public void UpdateState(float damage, float stunTime, float speed, bool isEvolved)
        {
            m_damage = damage;
            m_stunTime = stunTime;
            m_isEvolved = isEvolved;

            float newSpeed = (speed > 0) ? speed : 1f;
            m_velocity = m_velocity.normalized * newSpeed;

            UpdateVisualsByLevel();
        }
        
        #endregion

        #region 비주얼 및 물리
        
        private void UpdateVisualsByLevel()
        {
            if (m_animator != null)
            {
                m_animator.SetTrigger(m_isEvolved ? k_AnimTriggerLv2 : k_AnimTriggerLv1);
            }

            if (m_trailRenderer != null)
            {
                SetTrailColor(m_isEvolved ? m_trailColorLv2 : m_trailColorLv1);
            }
        }

        private void SetupTrailBase()
        {
            if (m_trailRenderer == null) return;
            m_trailRenderer.time = m_trailTime;
            m_trailRenderer.startWidth = m_trailStartWidth;
            m_trailRenderer.endWidth = m_trailEndWidth;
            m_trailRenderer.autodestruct = false;

            if (TryGetComponent(out SpriteRenderer sr))
            {
                m_trailRenderer.sortingLayerID = sr.sortingLayerID;
                m_trailRenderer.sortingOrder = sr.sortingOrder - 1;
            }

            if (m_trailRenderer.material == null || m_trailRenderer.material.name == "Default-Material")
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null) m_trailRenderer.material = new Material(shader);
            }
        }

        private void SetTrailColor(Color color)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(color, 0.0f), new GradientColorKey(color, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(color.a, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            m_trailRenderer.colorGradient = gradient;
        }

        private void BounceOffScreenEdges()
        {
            if (m_mainCamera == null) m_mainCamera = Camera.main;
            if (m_mainCamera == null) return;

            // 1. 카메라의 월드 좌표 경계 계산 (radius 고려)
            float camHalfHeight = m_mainCamera.orthographicSize;
            float camHalfWidth = camHalfHeight * m_mainCamera.aspect;
            Vector3 camPos = m_mainCamera.transform.position;

            float minX = camPos.x - camHalfWidth + m_radius;
            float maxX = camPos.x + camHalfWidth - m_radius;
            float minY = camPos.y - camHalfHeight + m_radius;
            float maxY = camPos.y + camHalfHeight - m_radius;

            Vector3 currentPosition = transform.position;
            bool bounced = false;

            // 2. Clamp 및 바운스 로직 (Strict)
            if (currentPosition.x <= minX)
            {
                currentPosition.x = minX;
                if (m_velocity.x < 0) m_velocity.x *= -1;
                bounced = true;
            }
            else if (currentPosition.x >= maxX)
            {
                currentPosition.x = maxX;
                if (m_velocity.x > 0) m_velocity.x *= -1;
                bounced = true;
            }

            if (currentPosition.y <= minY)
            {
                currentPosition.y = minY;
                if (m_velocity.y < 0) m_velocity.y *= -1;
                bounced = true;
            }
            else if (currentPosition.y >= maxY)
            {
                currentPosition.y = maxY;
                if (m_velocity.y > 0) m_velocity.y *= -1;
                bounced = true;
            }

            // 바운스가 발생했으면 위치를 강제 조정 (Clamping)
            if (bounced)
            {
                transform.position = currentPosition;
            }
            
            // 3. 안전장치: Clamp 로직이 실패할 경우를 대비한 2차 방어선
            if (Vector3.Distance(transform.position, camPos) > 40f)
            {
                // Z축은 유지하고 X, Y만 카메라 위치로 이동 (카메라 Z값(-10 등)을 그대로 가져오면 안보일 수 있음)
                transform.position = new Vector3(camPos.x, camPos.y, 0f);
                Debug.LogWarning("[PearlProjectile] 진주가 안전거리를 벗어나 복귀했습니다. (Z=0 보정)");
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Mob"))
            {
                int id = other.gameObject.GetInstanceID();
                if (!m_hitCooldowns.TryGetValue(id, out float nextTime) || Time.time >= nextTime)
                {
                    if (other.TryGetComponent(out MobBase mob) && !mob.IsDead)
                    {
                        mob.TakeDamage(m_damage, m_isEvolved ? m_stunTime : 0f);
                        m_hitCooldowns[id] = Time.time + k_HitCooldown;
                    }
                }
            }
        }
        
        #endregion
    }
}