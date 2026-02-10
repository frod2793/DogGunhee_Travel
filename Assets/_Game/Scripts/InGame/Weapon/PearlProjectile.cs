using UnityEngine;
using System.Collections.Generic;
using InGame.Mob.MobBase;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon.Logic;
using InGame.Weapon.Controllers;

namespace InGame.Weapon
{
    /// <summary>
    /// 저렴한 진주(Pearl) 무기의 핵심 투사체 컴포넌트입니다.
    /// <br/> 화면 외곽 바운스 처리, 자전 회전, 레벨별 비주얼(애니메이션/트레일) 효과를 처리합니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
    [RequireComponent(typeof(TrailRenderer), typeof(Animator))]
    public class PearlProjectile : MonoBehaviour
    {
        #region 1. 내부 변수 및 컴포넌트 (Components & State)

        // 로직 및 설정 데이터
        private PearlWeaponLogic m_logic;
        private PearlWeaponView m_view;

        // 컴포넌트 참조
        private Camera m_mainCamera;
        private TrailRenderer m_trailRenderer;
        private Animator m_animator;
        private Renderer m_renderer;

        // 물리 및 이동 상태
        private Vector3 m_velocity;
        private float m_radius = 0.5f;
        private readonly Dictionary<int, float> m_hitCooldowns = new Dictionary<int, float>();

        // 애니메이터 파라미터 해시
        private static readonly int k_AnimTriggerLv1 = Animator.StringToHash("Level1");
        private static readonly int k_AnimTriggerLv2 = Animator.StringToHash("Level2");

        #endregion

        #region 2. 프로퍼티 (Properties)

        /// <summary>
        /// 현재 투사체의 이동 속력
        /// </summary>
        public float CurrentSpeed => m_velocity.magnitude;

        #endregion

        #region 3. Unity 라이프사이클 (Lifecycle)

        private void Awake()
        {
            m_mainCamera = Camera.main;
            m_trailRenderer = GetComponent<TrailRenderer>();
            m_animator = GetComponent<Animator>();
            m_renderer = GetComponent<Renderer>();

            // 렌더러 기반 반지름 계산 (바운스 판정용)
            if (m_renderer != null)
            {
                m_radius = m_renderer.bounds.extents.x;
            }

            if (TryGetComponent<Collider2D>(out var col))
            {
                col.isTrigger = true;
            }
        }

        private void OnEnable()
        {
            m_mainCamera = Camera.main;
            m_hitCooldowns.Clear();
            
            if (m_trailRenderer != null)
            {
                m_trailRenderer.Clear();
            }
        }

        private void LateUpdate()
        {
            if (m_logic == null) return;

            // 1. 직선 이동 처리
            Vector3 nextPos = transform.position + m_velocity * Time.deltaTime;
            nextPos.z = 0f;
            transform.position = nextPos;

            // 2. 자전 효과 (도/초)
            transform.Rotate(0, 0, -360f * Time.deltaTime);

            // 3. 화면 외곽 바운스 체크
            BounceOffScreenEdges();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            // OnTriggerEnter보다 Stay가 연속 충돌 처리에 용이할 수 있으나 
            // 쿨타임 로직이 있으므로 로직에 맞춰 사용
            ProcessCollision(other);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            ProcessCollision(other);
        }

        #endregion

        #region 4. 초기화 및 상태 관리 (Init & Control)

        /// <summary>
        /// 진주 투사체를 초기화하고 초기 이동 방향 및 속도를 설정합니다.
        /// </summary>
        public void Init(PearlWeaponLogic logic, PearlWeaponView view, Vector3 initialVelocity, WeaponPoolManager poolManager)
        {
            m_logic = logic;
            m_view = view;

            m_velocity = initialVelocity;
            m_velocity.z = 0f;

            // 상태 및 비주얼 동기화
            UpdateState();
            transform.rotation = Quaternion.identity;
            SetupTrailBase();
        }

        /// <summary>
        /// 무기 레벨업이나 스탯 변경 시 투사체의 속력과 비주얼을 갱신합니다.
        /// </summary>
        public void UpdateState()
        {
            if (m_logic == null) return;

            // 로직의 AttackSpeed에 맞춰 현재 방향 유지하며 속력 갱신
            m_velocity = m_velocity.normalized * m_logic.AttackSpeed;
            
            UpdateVisualsByLevel();
        }

        #endregion

        #region 5. 충돌 처리 (Collision Logic)

        /// <summary>
        /// 적과 충돌 시 데미지를 적용하고 개별 쿨타임을 관리합니다.
        /// </summary>
        private void ProcessCollision(Collider2D other)
        {
            if (m_logic == null || !other.CompareTag("Mob")) return;

            int id = other.gameObject.GetInstanceID();
            float hitCooldown = (m_view != null) ? m_view.HitCooldown : 0.5f;

            // 적별 타격 쿨다운 체크
            if (!m_hitCooldowns.TryGetValue(id, out float nextTime) || Time.time >= nextTime)
            {
                if (other.TryGetComponent(out MobBase mob) && !mob.IsDead)
                {
                    // 데미지 적용 (진화 시 스턴 효과 추가)
                    mob.TakeDamage(m_logic.AttackPower, m_logic.IsEvolved ? m_logic.StunTime : 0f);
                    
                    // 다음 타격 가능 시간 기록
                    m_hitCooldowns[id] = Time.time + hitCooldown;
                }
            }
        }

        #endregion

        #region 6. 비주얼 및 연출 (Visuals)

        /// <summary>
        /// 현재 무기 상태(진화 여부)에 따라 애니메이션과 트레일 색상을 동기화합니다.
        /// </summary>
        private void UpdateVisualsByLevel()
        {
            if (m_logic == null) return;

            bool isEvolved = m_logic.IsEvolved;

            // 1. 애니메이션 트리거
            if (m_animator != null)
            {
                m_animator.SetTrigger(isEvolved ? k_AnimTriggerLv2 : k_AnimTriggerLv1);
            }

            // 2. 트레일 색상 변경
            if (m_trailRenderer != null && m_view != null)
            {
                SetTrailColor(isEvolved ? m_view.TrailColorLv2 : m_view.TrailColorLv1);
            }
        }

        /// <summary>
        /// 트레일 렌더러의 기본 속성 및 렌더링 순서를 설정합니다.
        /// </summary>
        private void SetupTrailBase()
        {
            if (m_trailRenderer == null || m_view == null) return;

            m_trailRenderer.time = m_view.TrailTime;
            m_trailRenderer.startWidth = m_view.TrailStartWidth;
            m_trailRenderer.endWidth = m_view.TrailEndWidth;
            m_trailRenderer.autodestruct = false;

            // 소팅 레이어 동기화 (본체보다 뒤에 그려지도록)
            if (TryGetComponent(out SpriteRenderer sr))
            {
                m_trailRenderer.sortingLayerID = sr.sortingLayerID;
                m_trailRenderer.sortingOrder = sr.sortingOrder - 1;
            }

            // 머티리얼 안전 장치
            if (m_trailRenderer.material == null || m_trailRenderer.material.name.StartsWith("Default-Material"))
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null) m_trailRenderer.material = new Material(shader);
            }
        }

        /// <summary>
        /// 트레일의 색상 그라디언트를 동적으로 설정합니다.
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

        #region 7. 화면 경계 물리 (Screen Physics)

        /// <summary>
        /// 메인 카메라의 화면 경계를 감지하여 투사체를 반사(Bounce)시킵니다.
        /// </summary>
        private void BounceOffScreenEdges()
        {
            if (m_mainCamera == null) m_mainCamera = Camera.main;
            if (m_mainCamera == null) return;

            // 카메라 영역 계산
            float camHalfHeight = m_mainCamera.orthographicSize;
            float camHalfWidth = camHalfHeight * m_mainCamera.aspect;
            Vector3 camPos = m_mainCamera.transform.position;

            // 바운스 한계선 (반지름 보정)
            float minX = camPos.x - camHalfWidth + m_radius;
            float maxX = camPos.x + camHalfWidth - m_radius;
            float minY = camPos.y - camHalfHeight + m_radius;
            float maxY = camPos.y + camHalfHeight - m_radius;

            Vector3 currentPos = transform.position;
            bool isBounced = false;

            // X축 충돌 체크 및 반사
            if (currentPos.x <= minX)
            {
                currentPos.x = minX;
                if (m_velocity.x < 0) m_velocity.x *= -1f;
                isBounced = true;
            }
            else if (currentPos.x >= maxX)
            {
                currentPos.x = maxX;
                if (m_velocity.x > 0) m_velocity.x *= -1f;
                isBounced = true;
            }

            // Y축 충돌 체크 및 반사
            if (currentPos.y <= minY)
            {
                currentPos.y = minY;
                if (m_velocity.y < 0) m_velocity.y *= -1f;
                isBounced = true;
            }
            else if (currentPos.y >= maxY)
            {
                currentPos.y = maxY;
                if (m_velocity.y > 0) m_velocity.y *= -1f;
                isBounced = true;
            }

            if (isBounced)
            {
                transform.position = currentPos;
            }

            // 안전 가드: 카메라로부터 너무 멀어질 경우 강제 복귀 (화면 밖 끼임 방지)
            if (Vector3.SqrMagnitude(transform.position - camPos) > 1600f) // 40^2
            {
                transform.position = new Vector3(camPos.x, camPos.y, 0f);
            }
        }

        #endregion
    }
}