using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.Mob.MobBase;
using InGame.ObjectPool;
using UnityEngine;

namespace InGame.Weapon
{
    /// <summary>
    /// WeaponShield에서 발사되는 부메랑 투사체의 동작을 관리합니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class shieldProjectile : MonoBehaviour
    {
        private Transform m_playerTransform;

        // 부모의 참조 대신 능력치를 직접 저장하여 NullReferenceException을 방지합니다.
        private float m_attackPower;
        private float m_mobStunTime;

        // [Refactoring] 중복 타격 방지
        private readonly HashSet<int> m_hitHistory = new HashSet<int>();

        // [Refactoring] 시각 효과
        private SpriteRenderer m_spriteRenderer;
        private TrailRenderer m_trailRenderer;
        private Tween m_rotateTween;
        private Tween m_moveTween;

        private void Awake()
        {
            // 충돌 시 물리적 영향 없이 이벤트만 발생시키도록 트리거로 설정합니다.
            GetComponent<Collider2D>().isTrigger = true;
            m_spriteRenderer = GetComponent<SpriteRenderer>();
            m_trailRenderer = GetComponent<TrailRenderer>();
        }

        private void OnEnable()
        {
            m_hitHistory.Clear();
            if (m_trailRenderer != null)
            {
                m_trailRenderer.Clear();
                m_trailRenderer.emitting = true;
            }
            if (m_spriteRenderer != null)
            {
                 var c = m_spriteRenderer.color;
                 c.a = 1f;
                 m_spriteRenderer.color = c;
            }
        }

        /// <summary>
        /// 부메랑을 초기화하고 애니메이션을 시작합니다.
        /// </summary>
        public void Initialize(float attackPower, float mobStunTime, Transform playerTransform, Vector3 direction, float speed, float distance, float returnDelay, float rotationsPerSecond)
        {
            m_playerTransform = playerTransform;
            m_attackPower = attackPower;
            m_mobStunTime = mobStunTime;
            
            AnimateBoomerangAsync(direction, speed, distance, returnDelay, rotationsPerSecond).Forget();
        }

        private async UniTaskVoid AnimateBoomerangAsync(Vector3 direction, float speed, float distance, float returnDelay, float rotationsPerSecond)
        {
            var token = this.GetCancellationTokenOnDestroy();
            try
            {
                Vector3 originPosition = transform.position;
                float outwardDuration = distance / speed;
                float totalDuration = outwardDuration * 2 + returnDelay; // 대략적인 총 시간

                // 무한 회전
                m_rotateTween?.Kill();
                m_rotateTween = transform.DORotate(new Vector3(0, 0, 360f), 1f / rotationsPerSecond, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Incremental)
                    .SetLink(gameObject);

                // 1. 바깥으로 이동
                m_moveTween?.Kill();
                await transform.DOMove(originPosition + (direction * distance), outwardDuration)
                    .SetEase(Ease.OutQuad)
                    .ToUniTask(cancellationToken: token);

                // 2. 복귀 전 딜레이 & 히트 기록 초기화 (돌아올 때 다시 타격 가능)
                m_hitHistory.Clear();
                await UniTask.Delay(System.TimeSpan.FromSeconds(returnDelay), cancellationToken: token);

                // 3. 플레이어의 현재 위치를 동적으로 추적하며 복귀
                bool hasStartedFadeOut = false;
                while (gameObject.activeInHierarchy && m_playerTransform != null)
                {
                    Vector3 myPos = transform.position;
                    Vector3 targetPos = m_playerTransform.position;
                    float step = speed * Time.deltaTime;
                    
                    transform.position = Vector3.MoveTowards(myPos, targetPos, step);
                    
                    float distToPlayer = Vector3.Distance(myPos, targetPos);

                    // 플레이어 근처 도달 시 페이드 아웃
                    if (!hasStartedFadeOut && distToPlayer <= 1.5f)
                    {
                        hasStartedFadeOut = true;
                        if (m_spriteRenderer != null) _ = m_spriteRenderer.DOFade(0f, 0.2f);
                        if (m_trailRenderer != null) m_trailRenderer.emitting = false;
                    }

                    // 복귀 완료
                    if (distToPlayer < 0.2f) break;

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally
            {
                // 애니메이션이 끝나거나 취소되면 풀에 반환합니다.
                if (gameObject.activeInHierarchy)
                {
                    WeaponPoolManager.Instance.Release(this); // WeaponPoolManager를 통해 자신을 풀로 반환
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Mob") && other.TryGetComponent<MobBase>(out var mob))
            {
                int id = other.GetInstanceID();
                if (!m_hitHistory.Contains(id) && !mob.IsDead)
                {
                    m_hitHistory.Add(id);
                    mob.TakeDamage(m_attackPower, m_mobStunTime);
                    // [Refactoring] 부메랑 타격 시 PlayDamageEffect 호출 (내부적으로 Flash 효과 재생)
                    mob.PlayDamageEffect();
                }
            }
        }
        
        private void OnDisable()
        {
            // 오브젝트가 비활성화될 때 모든 DOTween 애니메이션을 확실히 정리합니다.
            m_rotateTween?.Kill();
            m_moveTween?.Kill();
            transform.DOKill();
        }
    }
}
