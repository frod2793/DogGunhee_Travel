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
        private bool m_isEvolved;

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
            m_hitCooldowns.Clear();
            if (m_trailRenderer != null) m_trailRenderer.Clear();
        }

        private void Update()
        {
            transform.position += m_velocity * Time.deltaTime;
            
            float rotateSpeed = 360f * Time.deltaTime;
            transform.Rotate(0, 0, -rotateSpeed);

            BounceOffScreenEdges();
        }

        #endregion

        #region 초기화 및 비주얼 업데이트

        public void Initialize(WeaphonBase weapon, Vector3 initialVelocity)
        {
            m_damage = weapon.attackPower;
            m_stunTime = weapon.mobStunTime;
            m_isEvolved = weapon.isEvolved;
            m_velocity = initialVelocity;
            
            transform.rotation = Quaternion.identity;

            UpdateVisualsByLevel();
        }

        private void UpdateVisualsByLevel()
        {
            if (m_animator != null)
            {
                int trigger = m_isEvolved ? k_AnimTriggerLv2 : k_AnimTriggerLv1;
                m_animator.SetTrigger(trigger);
            }

            if (m_trailRenderer != null)
            {
                Color targetColor = m_isEvolved ? m_trailColorLv2 : m_trailColorLv1;
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
                    if (other.TryGetComponent(out MobBase mob) && !mob.IsDead)
                    {
                        float appliedStun = m_isEvolved ? m_stunTime : 0f;
                        mob.TakeDamage(m_damage, appliedStun);
                        m_hitCooldowns[id] = Time.time + k_HitCooldown;
                    }
                }
            }
        }
        
        public void UpdateState(WeaphonBase weapon)
        {
            m_damage = weapon.attackPower;
            m_stunTime = weapon.mobStunTime;
            m_isEvolved = weapon.isEvolved;

            UpdateVisualsByLevel();
        }
        #endregion
    }
}