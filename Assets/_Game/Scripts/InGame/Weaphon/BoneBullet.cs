using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.Mob.MobBase;
using InGame.ObjectPool;
using UnityEngine;
using InGame;
using InGame.Weaphon.Base;

namespace InGame.Weaphon
{
    public class BoneBullet : MonoBehaviour
    {
        #region 필드 및 변수

        public float BulletSpeed { get; set; }
        
        private float m_attackPower;
        private float m_stunTime;
        private bool m_isEvolved;

        [Header("이동 설정")]
        [SerializeField]
        private float m_travelDistance = 20f;

        [Header("회전 설정")]
        [SerializeField]
        private float m_rotateSpeed = 360f;

        [Header("폭발 설정")]
        [SerializeField]
        private float m_explosionRadius = 1.5f;
        [SerializeField]
        private float m_explosionDamage = 10f;

        [Header("감지 설정")]
        [SerializeField]
        private LayerMask m_mobLayerMask;

        private Transform m_transform;
        private Vector3 m_attackAngle;
        private bool m_isActive;
        private Tween m_moveTween;
        private System.Threading.CancellationTokenSource m_lifetimeCts;

        // 이동 감지 관련
        private Vector3 m_lastPosition;
        private float m_stoppedTime;
        private const float k_StopThreshold = 0.01f; // 정지 판정 임계값
        private const float k_MaxStoppedDuration = 0.3f; // 정지 상태 최대 허용 시간 (초)

        private const int k_MaxOverlapColliders = 10;
        private readonly Collider2D[] m_overlapResults = new Collider2D[k_MaxOverlapColliders];
        private ContactFilter2D m_contactFilter;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_transform = transform;
            
            m_contactFilter = new ContactFilter2D();
            m_contactFilter.useTriggers = true;
            m_contactFilter.SetLayerMask(m_mobLayerMask);
            m_contactFilter.useLayerMask = true;
        }

        private void OnEnable()
        {
            m_isActive = true;
            m_lastPosition = m_transform.position;
            m_stoppedTime = 0f;
            SoundManager.PlaySound(Sound.SFX, SoundKeys.Throwbone);
        }

        private void OnDisable()
        {
            m_isActive = false;
            m_moveTween?.Kill();
            m_lifetimeCts?.Cancel();
            m_lifetimeCts?.Dispose();
            m_lifetimeCts = null;
        }

        private void Update()
        {
            if (!m_isActive) return;

            // 이동 감지: 현재 위치와 이전 프레임 위치 비교
            float distanceMoved = Vector3.Distance(m_transform.position, m_lastPosition);
            
            if (distanceMoved < k_StopThreshold)
            {
                // 이동이 거의 없음 (정지 상태)
                m_stoppedTime += Time.deltaTime;
                
                if (m_stoppedTime >= k_MaxStoppedDuration)
                {
                    // 일정 시간 동안 정지 상태 → 풀로 반환
                    LogManager.Log("[BoneBullet] Movement stopped, returning to pool", LogManager.LogCategory.Weapon);
                    ReleaseToPool();
                    return;
                }
            }
            else
            {
                // 이동 중이면 타이머 리셋
                m_stoppedTime = 0f;
            }

            m_lastPosition = m_transform.position;
        }

        #endregion

        #region 핵심 로직

        public void ThrowBullet(Vector3 direction)
        {
            // UniTask로 구현된 비동기 로직을 실행하고, 결과룰 기다리지 않습니다 (Fire and Forget)
            ThrowAndTrackLifecycleAsync(direction).Forget();
        }

        private async UniTaskVoid ThrowAndTrackLifecycleAsync(Vector3 direction)
        {
            m_attackAngle = direction.normalized;
            Vector3 targetPosition = m_transform.position + m_attackAngle * m_travelDistance;
            float duration = m_travelDistance / BulletSpeed;

            // 최대 생명 시간 보장 (풀 누수 방지)
            m_lifetimeCts?.Cancel();
            m_lifetimeCts?.Dispose();
            m_lifetimeCts = new System.Threading.CancellationTokenSource();
            float maxLifetime = duration + 2f; // 여유 시간 추가
            LifetimeGuardAsync(maxLifetime, m_lifetimeCts.Token).Forget();

            var token = this.GetCancellationTokenOnDestroy();

            m_moveTween?.Kill();
            m_moveTween = DOTween.Sequence()
                .Append(m_transform.DOMove(targetPosition, duration).SetEase(Ease.Linear))
                .Join(m_transform.DORotate(new Vector3(0, 0, m_rotateSpeed), 1f / (m_rotateSpeed / 360f),
                        RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Incremental));

            // 트윈이 끝까지 재생되거나, 외부 요인(충돌)에 의해 취소될 때까지 기다립니다.
            bool cancelled = await m_moveTween.ToUniTask(cancellationToken: token).SuppressCancellationThrow();

            if (!cancelled)
            {
                ReleaseToPool();
            }
        }

        private void BulletExplosion()
        {
            Vector3 currentPosition = m_transform.position;
            EffectManager.Instance.PlayEffect(EffectType.BoneExplosion, currentPosition);

            int numColliders =
                Physics2D.OverlapCircle(currentPosition, m_explosionRadius, m_contactFilter, m_overlapResults);

            for (int i = 0; i < numColliders; i++)
            {
                if (m_overlapResults[i].TryGetComponent(out MobBase mob))
                {
                    mob.TakeDamage(m_explosionDamage);
                }
            }

            ReleaseToPool();
        }

        /// <summary>
        /// 최대 생명 시간 보장 (풀 반환 누락 방지)
        /// </summary>
        private async UniTaskVoid LifetimeGuardAsync(float maxLifetime, System.Threading.CancellationToken token)
        {
            try
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(maxLifetime), cancellationToken: token);
                
                // 시간 초과 시 강제로 풀 반환
                if (m_isActive)
                {
                    LogManager.LogWarning("[BoneBullet] Lifetime expired, forcing pool return", LogManager.LogCategory.Weapon);
                    ReleaseToPool();
                }
            }
            catch (System.OperationCanceledException)
            {
                // 정상 취소 (총알이 이미 반환됨)
            }
        }

        private void ReleaseToPool()
        {
            if (!m_isActive) return;
            m_isActive = false;
            m_moveTween?.Kill();
            m_lifetimeCts?.Cancel();

            // WeaponPoolManager를 통해 자신을 풀로 반환합니다.
            WeaponPoolManager.Instance.Release(this);
        }

        public void Initialize(float damage, float stunTime, float speed, bool isEvolved)
        {
            m_attackPower = damage;
            m_stunTime = stunTime;
            BulletSpeed = speed;
            m_isEvolved = isEvolved;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!m_isActive || !other.CompareTag("Mob")) return;

            if (other.TryGetComponent(out MobBase mob))
            {
                mob.TakeDamage(m_attackPower, m_stunTime);
            }

            if (m_isEvolved)
            {
                BulletExplosion();
            }
            else
            {
                ReleaseToPool();
            }
        }

        public void ResetState()
        {
            m_isActive = true;
            m_transform.DOKill();
            m_transform.rotation = Quaternion.identity;
            m_lifetimeCts?.Cancel();
            m_lifetimeCts?.Dispose();
            m_lifetimeCts = null;
            m_lastPosition = m_transform.position;
            m_stoppedTime = 0f;
        }

        #endregion
    }
}
