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

            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;

            SetupTrailBase();
        }

        private void OnEnable()
        {
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
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            transform.position += m_velocity * Time.deltaTime;
            transform.Rotate(0, 0, -360f * Time.deltaTime);
            BounceOffScreenEdges();
        }

        #endregion

        #region 초기화 및 상태 업데이트

        public void Initialize(WeaphonBase weapon, Vector3 initialVelocity)
        {
            m_velocity = initialVelocity;
            UpdateState(weapon);
            transform.rotation = Quaternion.identity;
        }
        
        public void UpdateState(WeaphonBase weapon)
        {
            m_damage = weapon.attackPower;
            m_stunTime = weapon.mobStunTime;
            m_isEvolved = weapon.isEvolved;

            float newSpeed = (weapon.attackSpeed > 0) ? weapon.attackSpeed : 1f;
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

        /// <summary>
        /// 카메라의 월드 좌표 경계를 계산하여 반사시키는 최종 로직
        /// </summary>
        private void BounceOffScreenEdges()
        {
            if (m_mainCamera == null) return;

            // 1. 카메라의 월드 좌표 경계를 직접 계산
            float camHalfHeight = m_mainCamera.orthographicSize;
            float camHalfWidth = camHalfHeight * m_mainCamera.aspect;
            Vector3 camPos = m_mainCamera.transform.position;

            float minX = camPos.x - camHalfWidth;
            float maxX = camPos.x + camHalfWidth;
            float minY = camPos.y - camHalfHeight;
            float maxY = camPos.y + camHalfHeight;

            Vector3 currentPosition = transform.position;

            // 2. 진주의 월드 좌표와 카메라의 월드 좌표 경계를 비교
            if ((currentPosition.x <= minX && m_velocity.x < 0) || (currentPosition.x >= maxX && m_velocity.x > 0))
            {
                m_velocity.x *= -1;
            }

            if ((currentPosition.y <= minY && m_velocity.y < 0) || (currentPosition.y >= maxY && m_velocity.y > 0))
            {
                m_velocity.y *= -1;
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