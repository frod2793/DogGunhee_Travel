using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 실제 날아가는 부메랑 투사체입니다.
    /// 페이드 인/아웃 효과, 관통 공격, 플레이어 추적 복귀 로직이 포함되어 있습니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
    public class BoomerangProjectile : MonoBehaviour
    {
        private IObjectPool<BoomerangProjectile> m_pool;
        private Transform m_playerTransform;
        private SpriteRenderer m_spriteRenderer; // 페이드 효과를 위해 추가
        
        // 스탯
        private float m_damage;
        private float m_stunTime;
        private float m_speed; 
        private float m_distance; 
        private float m_outDuration; 

        private bool m_isReturning;
        
        // 트윈 참조 (중복 실행 방지 및 정리용)
        private Tween m_rotateTween;
        private Tween m_fadeTween;

        private readonly HashSet<int> m_hitHistory = new HashSet<int>();

        private void Awake()
        {
            m_spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Initialize(IObjectPool<BoomerangProjectile> pool, Transform player, float damage, float stunTime, float speed, float distance)
        {
            m_pool = pool;
            m_playerTransform = player;
            m_damage = damage;
            m_stunTime = stunTime;
            m_speed = speed;
            m_distance = distance;
            
            // 거리 비례 시간 계산 (최소 0.5초)
            m_outDuration = Mathf.Max(0.5f, distance / speed);

            m_isReturning = false;
            m_hitHistory.Clear();

            // 회전 트윈 시작
            m_rotateTween?.Kill();
            m_rotateTween = transform.DORotate(new Vector3(0, 0, 720), 0.5f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear).SetLoops(-1, LoopType.Incremental);

            LaunchAsync().Forget();
        }

        private async UniTaskVoid LaunchAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();

            // 0. [Fade In] 시작 시 투명하게 시작해서 빠르게 나타남
            if (m_spriteRenderer != null)
            {
                // 알파값 0으로 초기화
                Color c = m_spriteRenderer.color;
                c.a = 0f;
                m_spriteRenderer.color = c;

                // 0.15초 동안 알파값 1로 페이드 인
                m_fadeTween?.Kill();
                m_fadeTween = m_spriteRenderer.DOFade(1f, 0.15f).SetEase(Ease.OutQuad);
            }

            // 1. [Outward] 밖으로 던지기
            Vector3 targetPos = transform.position + transform.up * m_distance;

            try
            {
                await transform.DOMove(targetPos, m_outDuration)
                    .SetEase(Ease.OutSine)
                    .ToUniTask(cancellationToken: token);

                // 2. [Turn] 복귀 준비
                m_isReturning = true;
                m_hitHistory.Clear(); // 돌아올 때 다시 타격 가능하도록 초기화
                
                await UniTask.Delay(100, cancellationToken: token);

                // 3. [Return] 플레이어 추적 복귀
                bool hasStartedFadeOut = false; // 페이드 아웃 시작 여부 체크

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
                    
                    // 이동 (갈 때보다 1.5배 빠르게 복귀)
                    float step = m_speed * 1.5f * Time.deltaTime;
                    transform.position = Vector3.MoveTowards(myPos, playerPos, step);

                    // [Fade Out] 플레이어에게 가까워지면 페이드 아웃 시작 (회수 직전)
                    if (!hasStartedFadeOut && distToPlayer <= 1.5f)
                    {
                        hasStartedFadeOut = true;
                        if (m_spriteRenderer != null)
                        {
                            m_fadeTween?.Kill();
                            // 남은 거리에 비례해 빠르게 사라짐 (0.2초)
                            m_fadeTween = m_spriteRenderer.DOFade(0f, 0.2f).SetEase(Ease.InQuad);
                        }
                    }

                    // 회수 판정 (거리가 0.5 이내면 잡은 것으로 간주)
                    if (distToPlayer < 0.5f)
                    {
                        break;
                    }

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
                
                // 갈 때 한 번, 올 때 한 번 타격 가능
                if (!m_hitHistory.Contains(id))
                {
                    if (other.TryGetComponent(out VamserMobBase mob))
                    {
                        m_hitHistory.Add(id);
                        mob.TakeDamage(m_damage, m_stunTime);
                    }
                }
            }
        }

        private void ReleaseToPool()
        {
            // 트윈 정리
            m_rotateTween?.Kill();
            m_fadeTween?.Kill();

            // 알파값 원복 (혹시 모르니 1로 복구해두고 반환)
            if (m_spriteRenderer != null)
            {
                Color c = m_spriteRenderer.color;
                c.a = 1f;
                m_spriteRenderer.color = c;
            }

            if (gameObject.activeSelf)
            {
                m_pool.Release(this);
            }
        }
    }
}