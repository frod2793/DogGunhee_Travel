using UnityEngine;
using System.Collections.Generic;
using InGame.Mob.MobBase;
using InGame.Weapon.Base;
using InGame.Weapon.Logic;
using InGame.Weapon.Controllers;

namespace InGame.Weapon
{
    /// <summary>
    /// 저렴한 진주 무기(Pearl)의 핵심 투사체 컴포넌트입니다.
    /// 화면 외곽 바운스 및 레벨별 비주얼 효과를 처리합니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
    [RequireComponent(typeof(TrailRenderer), typeof(Animator))]
    public class PearlProjectile : MonoBehaviour
    {
        #region 내부 상태 및 캐시

        /// <summary>
        /// 활성화된 진주 투사체에 대한 정적 참조입니다. (Legacy Support)
        /// </summary>
        public static PearlProjectile Instance { get; private set; }

        private PearlWeaponLogic m_logic;
        private PearlWeaponView m_view; 
        private Vector3 m_velocity;
        private Camera m_mainCamera;
        private TrailRenderer m_trailRenderer;
        private Animator m_animator;
        private Renderer m_renderer; 
        private float m_radius = 0.5f; 

        // 적별 타격 쿨다운 관리
        private readonly Dictionary<int, float> m_hitCooldowns = new Dictionary<int, float>();

        // 애니메이터 파라미터 해시
        private static readonly int k_AnimTriggerLv1 = Animator.StringToHash("Level1");
        private static readonly int k_AnimTriggerLv2 = Animator.StringToHash("Level2");

        #endregion

        #region 프로퍼티

        public float CurrentSpeed => m_velocity.magnitude;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_mainCamera = Camera.main;
            m_trailRenderer = GetComponent<TrailRenderer>();
            m_animator = GetComponent<Animator>();
            m_renderer = GetComponent<Renderer>(); 
            
            if (m_renderer != null) m_radius = m_renderer.bounds.extents.x;
            if (TryGetComponent<Collider2D>(out var col)) col.isTrigger = true;
        }

        private void OnEnable()
        {
            m_mainCamera = Camera.main;
            if (Instance != null && Instance != this) return;
            Instance = this;

            m_hitCooldowns.Clear();
            if (m_trailRenderer != null) m_trailRenderer.Clear();
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        private void LateUpdate()
        {
            if (m_logic == null) return;

            Vector3 nextPos = transform.position + m_velocity * Time.deltaTime;
            nextPos.z = 0f;
            transform.position = nextPos;

            transform.Rotate(0, 0, -360f * Time.deltaTime);
            BounceOffScreenEdges();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (m_logic == null || !other.CompareTag("Mob")) return;

            int id = other.gameObject.GetInstanceID();
            float hitCooldown = (m_view != null) ? m_view.HitCooldown : 0.5f;

            if (!m_hitCooldowns.TryGetValue(id, out float nextTime) || Time.time >= nextTime)
            {
                if (other.TryGetComponent(out MobBase mob) && !mob.IsDead)
                {
                    mob.TakeDamage(m_logic.AttackPower, m_logic.IsEvolved ? m_logic.StunTime : 0f);
                    m_hitCooldowns[id] = Time.time + hitCooldown;
                }
            }
        }

        #endregion

        #region 초기화 및 제어

        /// <summary>
        /// 진주 투사체를 초기화하고 초기 속도를 설정합니다.
        /// </summary>
        public void Initialize(PearlWeaponLogic logic, PearlWeaponView view, Vector3 initialVelocity)
        {
            m_logic = logic;
            m_view = view;
            
            m_velocity = initialVelocity;
            m_velocity.z = 0f;

            UpdateState();
            transform.rotation = Quaternion.identity;
            SetupTrailBase();
        }
        
        /// <summary>
        /// 무기 레벨업이나 스탯 변경 시 투사체의 상태를 갱신합니다.
        /// </summary>
        public void UpdateState()
        {
            if (m_logic == null) return;
            m_velocity = m_velocity.normalized * m_logic.AttackSpeed;
            UpdateVisualsByLevel();
        }
        
        #endregion

        #region 제어 로직 (비주얼 및 물리)
        
        private void UpdateVisualsByLevel()
        {
            bool isEvolved = m_logic.IsEvolved;
            if (m_animator != null) m_animator.SetTrigger(isEvolved ? k_AnimTriggerLv2 : k_AnimTriggerLv1);
            if (m_trailRenderer != null && m_view != null) SetTrailColor(isEvolved ? m_view.TrailColorLv2 : m_view.TrailColorLv1);
        }

        private void SetupTrailBase()
        {
            if (m_trailRenderer == null || m_view == null) return;
            
            m_trailRenderer.time = m_view.TrailTime;
            m_trailRenderer.startWidth = m_view.TrailStartWidth;
            m_trailRenderer.endWidth = m_view.TrailEndWidth;
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

            float camHalfHeight = m_mainCamera.orthographicSize;
            float camHalfWidth = camHalfHeight * m_mainCamera.aspect;
            Vector3 camPos = m_mainCamera.transform.position;

            float minX = camPos.x - camHalfWidth + m_radius;
            float maxX = camPos.x + camHalfWidth - m_radius;
            float minY = camPos.y - camHalfHeight + m_radius;
            float maxY = camPos.y + camHalfHeight - m_radius;

            Vector3 currentPosition = transform.position;
            bool bounced = false;

            if (currentPosition.x <= minX) { currentPosition.x = minX; if (m_velocity.x < 0) m_velocity.x *= -1; bounced = true; }
            else if (currentPosition.x >= maxX) { currentPosition.x = maxX; if (m_velocity.x > 0) m_velocity.x *= -1; bounced = true; }

            if (currentPosition.y <= minY) { currentPosition.y = minY; if (m_velocity.y < 0) m_velocity.y *= -1; bounced = true; }
            else if (currentPosition.y >= maxY) { currentPosition.y = maxY; if (m_velocity.y > 0) m_velocity.y *= -1; bounced = true; }

            if (bounced) transform.position = currentPosition;
            if (Vector3.Distance(transform.position, camPos) > 40f) transform.position = new Vector3(camPos.x, camPos.y, 0f);
        }
        
        #endregion
    }
}