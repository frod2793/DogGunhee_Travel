using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

using System.Collections.Generic;
using InGame.ObjectPool;
using Vamser_like.Mob.MobBase;

namespace Vamser_like.Weaphon
{
    [RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
    [RequireComponent(typeof(TrailRenderer))]
    public class BoomerangProjectile : MonoBehaviour
    {
        private Transform m_playerTransform;
        private SpriteRenderer m_spriteRenderer;
        private TrailRenderer m_trailRenderer;

        // 스탯
        private float m_damage;
        private float m_stunTime;
        private float m_speed;
        private float m_distance;
        private float m_outDuration;

        private bool m_isReturning;
        
        private Tween m_rotateTween;
        private Tween m_fadeTween;

        private readonly HashSet<int> m_hitHistory = new HashSet<int>();

        [Header("Visual Settings")]
        [SerializeField] private float m_trailTime = 0.2f;
        [SerializeField] private float m_trailStartWidth = 0.5f;
        [SerializeField] private Color m_trailColor = new Color(1, 1, 1, 0.5f);

        private void Awake()
        {
            m_spriteRenderer = GetComponent<SpriteRenderer>();
            m_trailRenderer = GetComponent<TrailRenderer>();
            
            SetupTrail();
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

        // Initialize 메서드에서 IObjectPool<BoomerangProjectile> pool 매개변수 제거
        public void Initialize(Transform player, float damage, float stunTime, float speed, float distance)
        {
            // m_pool = pool; // 제거
            m_playerTransform = player;
            m_damage = damage;
            m_stunTime = stunTime;
            m_speed = speed;
            m_distance = distance;
            
            m_outDuration = Mathf.Max(0.5f, distance / speed);
            m_isReturning = false;
            m_hitHistory.Clear();

            if (m_trailRenderer != null)
            {
                m_trailRenderer.Clear();
                m_trailRenderer.emitting = true;
            }

            m_rotateTween?.Kill();
            m_rotateTween = transform.DORotate(new Vector3(0, 0, 720), 0.5f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear).SetLoops(-1, LoopType.Incremental);

            LaunchAsync().Forget();
        }

        private async UniTaskVoid LaunchAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();

            if (m_spriteRenderer != null)
            {
                Color c = m_spriteRenderer.color;
                c.a = 0f;
                m_spriteRenderer.color = c;
                m_fadeTween?.Kill();
                m_fadeTween = m_spriteRenderer.DOFade(1f, 0.15f).SetEase(Ease.OutQuad);
            }

            Vector3 targetPos = transform.position + transform.up * m_distance;

            try
            {
                // [Outward]
                await transform.DOMove(targetPos, m_outDuration)
                    .SetEase(Ease.OutSine)
                    .ToUniTask(cancellationToken: token);

                // [Turn]
                m_isReturning = true;
                m_hitHistory.Clear();
                
                await UniTask.Delay(100, cancellationToken: token);

                // [Return]
                bool hasStartedFadeOut = false;

                while (true)
                {
                    if (m_playerTransform == null) { ReleaseToPool(); return; }

                    Vector3 myPos = transform.position;
                    Vector3 playerPos = m_playerTransform.position;
                    float distToPlayer = Vector3.Distance(myPos, playerPos);
                    
                    float step = m_speed * 1.5f * Time.deltaTime;
                    transform.position = Vector3.MoveTowards(myPos, playerPos, step);

                    if (!hasStartedFadeOut && distToPlayer <= 1.5f)
                    {
                        hasStartedFadeOut = true;
                        if (m_spriteRenderer != null)
                        {
                            m_fadeTween?.Kill();
                            m_fadeTween = m_spriteRenderer.DOFade(0f, 0.2f).SetEase(Ease.InQuad);
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

        private void ReleaseToPool()
        {
            m_rotateTween?.Kill();
            m_fadeTween?.Kill();

            if (m_trailRenderer != null)
            {
                m_trailRenderer.emitting = false;
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
    }
}