using UnityEngine;
using System.Collections.Generic;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 화면 가장자리에서 튕기며 이동하는 진주 투사체입니다.
    /// 레벨에 따라 애니메이션과 궤적 색상이 변경됩니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
    [RequireComponent(typeof(TrailRenderer), typeof(Animator))] 
    public class PearlProjectile : MonoBehaviour
    {
        #region 내부 변수

        private Vector3 m_velocity;
        private float m_damage;
        private float m_stunTime;
        private bool m_isUpgraded;

        private Camera m_mainCamera;
        private TrailRenderer m_trailRenderer;
        private Animator m_animator; // [추가]

        // 중복 타격 방지 쿨타임
        private readonly Dictionary<int, float> m_hitCooldowns = new Dictionary<int, float>();
        private const float k_HitCooldown = 0.5f;

        // 애니메이션 해시
        private static readonly int k_AnimTriggerLv1 = Animator.StringToHash("Level1");
        private static readonly int k_AnimTriggerLv2 = Animator.StringToHash("Level2");

        #endregion

        #region 인스펙터 설정 (Visual)

        [Header("Trail Settings")]
        [SerializeField] private float m_trailTime = 0.3f;
        [SerializeField] private float m_trailStartWidth = 0.4f;
        [SerializeField] private float m_trailEndWidth = 0.0f;

        [Header("Level Colors")]
        [Tooltip("레벨 1 궤적 색상 (기본 흰색)")]
        [SerializeField] private Color m_trailColorLv1 = new Color(1f, 1f, 1f, 0.5f);
        
        [Tooltip("레벨 2 궤적 색상 (보라색/분홍색)")]
        [SerializeField] private Color m_trailColorLv2 = new Color(1f, 0f, 1f, 0.5f); // Magenta

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_mainCamera = Camera.main;
            m_trailRenderer = GetComponent<TrailRenderer>();
            m_animator = GetComponent<Animator>(); // [추가]

            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
            
            // 초기 궤적 설정 (색상은 Initialize에서 덮어씌워짐)
            SetupTrailBase();
        }

        private void OnEnable()
        {
            m_hitCooldowns.Clear();
            if (m_trailRenderer != null) m_trailRenderer.Clear();
        }

        private void Update()
        {
            // 이동 및 회전
            transform.position += m_velocity * Time.deltaTime;
            
            float rotateSpeed = 360f * Time.deltaTime;
            transform.Rotate(0, 0, -rotateSpeed);

            BounceOffScreenEdges();
        }

        #endregion

        #region 초기화 및 비주얼 업데이트

        public void Initialize(Weaphon_base weapon, Vector3 initialVelocity)
        {
            m_damage = weapon.attackPower;
            m_stunTime = weapon.mobStunTime;
            m_isUpgraded = weapon.isUpgradelv2;
            m_velocity = initialVelocity;
            
            transform.rotation = Quaternion.identity;

            // [핵심] 레벨에 따른 비주얼(애니메이션 + 색상) 업데이트
            UpdateVisualsByLevel();
        }

        private void UpdateVisualsByLevel()
        {
            // 1. 애니메이션 트리거 발동
            if (m_animator != null)
            {
                int trigger = m_isUpgraded ? k_AnimTriggerLv2 : k_AnimTriggerLv1;
                m_animator.SetTrigger(trigger);
            }

            // 2. 궤적 색상 변경
            if (m_trailRenderer != null)
            {
                Color targetColor = m_isUpgraded ? m_trailColorLv2 : m_trailColorLv1;
                SetTrailColor(targetColor);
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

        /// <summary>
        /// 궤적의 색상(그라데이션)을 변경합니다.
        /// </summary>
        private void SetTrailColor(Color color)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(color, 0.0f), new GradientColorKey(color, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(color.a, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            m_trailRenderer.colorGradient = gradient;
        }

        #endregion

        #region 물리 및 충돌

        private void BounceOffScreenEdges()
        {
            if (m_mainCamera == null) return;

            Vector3 viewPos = m_mainCamera.WorldToViewportPoint(transform.position);
            
            if ((viewPos.x <= 0.02f && m_velocity.x < 0) || (viewPos.x >= 0.98f && m_velocity.x > 0))
                m_velocity.x *= -1;

            if ((viewPos.y <= 0.02f && m_velocity.y < 0) || (viewPos.y >= 0.98f && m_velocity.y > 0))
                m_velocity.y *= -1;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Mob"))
            {
                int id = other.gameObject.GetInstanceID();
                
                if (!m_hitCooldowns.TryGetValue(id, out float nextTime) || Time.time >= nextTime)
                {
                    if (other.TryGetComponent(out VamserMobBase mob) && !mob.IsDead)
                    {
                        float appliedStun = m_isUpgraded ? m_stunTime : 0f;
                        mob.TakeDamage(m_damage, appliedStun);
                        m_hitCooldowns[id] = Time.time + k_HitCooldown;
                    }
                }
            }
        }
        /// <summary>
        /// 이미 활동 중인 진주의 상태(공격력, 레벨 비주얼 등)를 갱신합니다.
        /// </summary>
        public void UpdateState(Weaphon_base weapon)
        {
            // 스탯 갱신
            m_damage = weapon.attackPower;
            m_stunTime = weapon.mobStunTime;
            m_isUpgraded = weapon.isUpgradelv2;

            // 비주얼(색상, 애니메이션) 즉시 업데이트
            UpdateVisualsByLevel();
        }
        #endregion
    }
}