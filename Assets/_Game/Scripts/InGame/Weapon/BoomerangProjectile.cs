using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;
using InGame.Mob.MobBase;
using InGame.ObjectPool;

namespace InGame.Weapon
{
    /// <summary>
    /// 부메랑 투사체 클래스입니다.
    /// 일정 거리를 날아갔다가 플레이어에게 다시 돌아오는 로직을 수행합니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
    [RequireComponent(typeof(TrailRenderer))]
    public class BoomerangProjectile : MonoBehaviour
    {
        #region 설정 데이터

        [Header("시각 효과 설정")]
        [SerializeField] private float m_trailTime = 0.2f;
        [SerializeField] private float m_trailStartWidth = 0.5f;
        [SerializeField] private Color m_trailColor = new Color(1, 1, 1, 0.5f);

        [Header("이동 설정")]
        [SerializeField, Tooltip("날아갈 때의 속도 배율")] 
        private float m_outwardSpeedMultiplier = 1.5f;

        [SerializeField, Tooltip("돌아올 때의 속도 배율")]
        private float m_returnSpeedMultiplier = 1.2f;

        #endregion

        #region 내부 상태 및 캐시

        private Transform m_playerTransform;
        private SpriteRenderer m_spriteRenderer;
        private TrailRenderer m_trailRenderer;

        private float m_damage;
        private float m_stunTime;
        private float m_speed;
        private float m_distance;
        private System.Action m_onReturn;

        private readonly HashSet<int> m_hitHistory = new HashSet<int>();

        private Tween m_rotateTween;
        private Tween m_fadeTween;
        private Tween m_trailFadeTween;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_spriteRenderer = GetComponent<SpriteRenderer>();
            m_trailRenderer = GetComponent<TrailRenderer>();
            SetupTrail();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Mob"))
            {
                int id = other.gameObject.GetInstanceID();
                if (!m_hitHistory.Contains(id))
                {
                    if (other.TryGetComponent(out MobBase mob))
                    {
                        m_hitHistory.Add(id);
                        mob.TakeDamage(m_damage, m_stunTime);
                    }
                }
            }
        }

        #endregion

        #region 초기화 및 제어

        /// <summary>
        /// 투사체를 초기화하고 발사합니다.
        /// </summary>
        public void Initialize(Transform player, float damage, float stunTime, float speed, float distance, System.Action onReturn = null)
        {
            m_playerTransform = player;
            m_damage = damage;
            m_stunTime = stunTime;
            m_speed = speed;
            m_distance = distance;
            m_onReturn = onReturn;
            
            m_hitHistory.Clear();

            // Trail 초기화
            if (m_trailRenderer != null)
            {
                m_trailRenderer.Clear();
                if (m_trailRenderer.material != null)
                {
                    m_trailFadeTween?.Kill();
                    m_trailRenderer.material.color = Color.white;
                }
                m_trailRenderer.emitting = true;
            }

            // 회전 애니메이션 시작
            m_rotateTween?.Kill();
            m_rotateTween = transform.DORotate(new Vector3(0, 0, 720), 0.5f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear).SetLoops(-1, LoopType.Incremental);

            // 발사 로직 비동기 실행
            LaunchAsync().Forget();
        }

        private void SetupTrail()
        {
            if (m_trailRenderer == null) return;

            m_trailRenderer.time = m_trailTime;
            m_trailRenderer.startWidth = m_trailStartWidth;
            m_trailRenderer.endWidth = 0f;
            m_trailRenderer.autodestruct = false;
            
            if (m_trailRenderer.material == null || m_trailRenderer.material.name == "Default-Material")
            {
                m_trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(m_trailColor, 0.0f), new GradientColorKey(m_trailColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(m_trailColor.a, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            m_trailRenderer.colorGradient = gradient;
            m_trailRenderer.sortingOrder = m_spriteRenderer.sortingOrder - 1;
        }

        private void ReleaseToPool()
        {
            m_onReturn?.Invoke();
            m_onReturn = null;

            m_rotateTween?.Kill();
            m_fadeTween?.Kill();
            m_trailFadeTween?.Kill();

            if (m_trailRenderer != null)
            {
                m_trailRenderer.emitting = false;
                if (m_trailRenderer.material != null) m_trailRenderer.material.color = Color.white;
            }

            if (m_spriteRenderer != null)
            {
                Color c = m_spriteRenderer.color;
                c.a = 1f;
                m_spriteRenderer.color = c;
            }

            if (gameObject.activeSelf)
            {
                WeaponPoolManager.Instance.Release(this);
            }
        }

        #endregion

        #region 제어 로직 (비동기)

        private async UniTaskVoid LaunchAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();

            // 등장 페이드 인
            if (m_spriteRenderer != null)
            {
                Color c = m_spriteRenderer.color;
                c.a = 0f;
                m_spriteRenderer.color = c;
                m_fadeTween?.Kill();
                m_fadeTween = m_spriteRenderer.DOFade(1f, 0.15f).SetEase(Ease.OutQuad);
            }

            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos + transform.up * m_distance;

            try
            {
                // 1. [Outward] 목표 지점까지 이동
                float outwardSpeed = m_speed * m_outwardSpeedMultiplier;
                float outDuration = (outwardSpeed > 0) ? m_distance / outwardSpeed : 1f;

                await transform.DOMove(targetPos, outDuration)
                    .SetEase(Ease.OutSine)
                    .ToUniTask(cancellationToken: token);

                // 2. [Turn] 잠시 대기
                m_hitHistory.Clear();
                await UniTask.Delay(100, cancellationToken: token);

                // 3. [Return] 플레이어에게 복귀
                bool hasStartedFadeOut = false;
                float returnSpeed = m_speed * m_returnSpeedMultiplier;

                while (true)
                {
                    if (m_playerTransform == null) 
                    { 
                        ReleaseToPool(); 
                        return; 
                    }

                    Vector3 myPos = transform.position;
                    Vector3 playerPos = m_playerTransform.position;
                    float distToPlayer = Vector3.Distance(myPos, playerPos);
                    
                    float step = returnSpeed * Time.deltaTime;
                    transform.position = Vector3.MoveTowards(myPos, playerPos, step);

                    if (!hasStartedFadeOut && distToPlayer <= 1.5f)
                    {
                        hasStartedFadeOut = true;
                        if (m_spriteRenderer != null)
                        {
                            m_fadeTween?.Kill();
                            m_fadeTween = m_spriteRenderer.DOFade(0f, 0.2f).SetEase(Ease.InQuad);
                        }
                        if (m_trailRenderer != null && m_trailRenderer.material != null)
                        {
                            m_trailFadeTween?.Kill();
                            m_trailFadeTween = m_trailRenderer.material.DOFade(0f, 0.2f).SetEase(Ease.InQuad);
                        }
                    }

                    if (distToPlayer < 0.5f) break;
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                }
            }
            finally
            {
                ReleaseToPool();
            }
        }

        #endregion
    }
}