using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.Mob.MobBase;
using InGame.ObjectPool;
using UnityEngine;
using InGame;
using InGame.Weapon.Base;

namespace InGame.Weapon
{
    public class BoneBullet : MonoBehaviour
    {
        #region Inspector 설정

        [Header("이동 설정")]
        [SerializeField] private float m_travelDistance = 20f;

        [Header("회전 설정")]
        [SerializeField] private float m_rotateSpeed = 360f;

        [Header("폭발 설정")]
        [SerializeField] private float m_explosionRadius = 1.5f;
        [SerializeField] private float m_explosionDamage = 10f;

        [Header("감지 설정")]
        [SerializeField] private LayerMask m_mobLayerMask;

        #endregion

        #region 런타임 상태

        public float BulletSpeed { get; set; }
        
        private float m_attackPower;
        private float m_stunTime;
        private bool m_isEvolved;
        private bool m_isActive;

        #endregion

        #region 캐시 및 내부 변수

        private Transform m_transform;
        private Vector3 m_attackAngle;
        private Tween m_moveTween;
        private System.Threading.CancellationTokenSource m_lifetimeCts;

        // 이동 감지 관련
        private Vector3 m_lastPosition;
        private float m_stoppedTime;

        // 충돌 감지 캐시
        private readonly Collider2D[] m_overlapResults = new Collider2D[k_MaxOverlapColliders];
        private ContactFilter2D m_contactFilter;

        #endregion

        #region 상수

        private const float k_StopThreshold = 0.01f;
        private const float k_StopThresholdSqr = k_StopThreshold * k_StopThreshold;
        private const float k_MaxStoppedDuration = 0.3f;
        private const float k_LifetimeBuffer = 2f;
        private const int k_MaxOverlapColliders = 10;

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

            // 이동 감지: 제곱 거리 비교로 Sqrt 연산 제거 (성능 최적화)
            Vector3 currentPos = m_transform.position;
            float sqrDistanceMoved = (currentPos - m_lastPosition).sqrMagnitude;
            
            if (sqrDistanceMoved < k_StopThresholdSqr)
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

            m_lastPosition = currentPos;
        }

        #endregion

        #region 핵심 로직

        /// <summary>
        /// 총알을 지정된 방향으로 발사합니다.
        /// </summary>
        /// <param name="direction">발사 방향 (정규화하여 사용)</param>
        public void ThrowBullet(Vector3 direction)
        {
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
            float maxLifetime = duration + k_LifetimeBuffer;
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

        /// <summary>
        /// 총알의 전투 파라미터를 초기화합니다.
        /// </summary>
        /// <param name="damage">공격력</param>
        /// <param name="stunTime">스턴 시간 (초)</param>
        /// <param name="speed">이동 속도</param>
        /// <param name="isEvolved">진화 여부 (폭발 효과)</param>
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

        /// <summary>
        /// 풀에서 재사용 시 상태를 초기화합니다.
        /// </summary>
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
