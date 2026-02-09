using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.Mob.MobBase;
using InGame.ObjectPool;
using UnityEngine;

namespace InGame.Weapon
{
    /// <summary>
    /// 개다귀(Bone) 무기의 투사체 로직을 처리하는 컴포넌트입니다.
    /// 이동, 회전, 충돌 감지 및 폭발 효과를 관리합니다.
    /// </summary>
    public class BoneBullet : MonoBehaviour
    {
        #region 내부 상태 및 변수

        [Header("이동 설정")]
        [Tooltip("투사체가 날아가는 최대 거리")]
        [SerializeField] private float m_travelDistance = 20f;

        [Header("회전 설정")]
        [Tooltip("초당 회전 속도 (도/s)")]
        [SerializeField] private float m_rotateSpeed = 720f;

        [Header("폭발 설정 (진화 시)")]
        [Tooltip("폭발 반경")]
        [SerializeField] private float m_explosionRadius = 1.5f;
        [Tooltip("폭발 시 발생하는 고정 데미지")]
        [SerializeField] private float m_explosionDamage = 10f;

        [Header("감지 설정")]
        [Tooltip("적(Mob)으로 인식할 레이어")]
        [SerializeField] private LayerMask m_mobLayerMask;

        private const float k_StopThreshold = 0.01f;
        private const float k_StopThresholdSqr = k_StopThreshold * k_StopThreshold;
        private const float k_MaxStoppedDuration = 0.3f;
        private const float k_LifetimeBuffer = 2f;
        private const int k_MaxOverlapColliders = 10;

        private Transform m_transform;
        private Vector3 m_attackDirection;
        private Tween m_moveTween;
        private CancellationTokenSource m_lifetimeCts;

        // 이동 감지 관련
        private Vector3 m_lastPosition;
        private float m_stoppedTime;

        // 충돌 감지 캐시
        private readonly Collider2D[] m_overlapResults = new Collider2D[k_MaxOverlapColliders];
        private ContactFilter2D m_contactFilter;

        private float m_attackPower;
        private float m_stunTime;
        private bool m_isEvolved;
        private bool m_isActive;

        #endregion

        #region 프로퍼티

        /// <summary>투사체의 비행 속도입니다.</summary>
        public float BulletSpeed { get; set; }

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_transform = transform;
            
            // 물리 감지용 필터 설정
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
            
            // 모든 연출 및 비동기 작업 정리
            m_transform.DOKill();
            CancelLifetimeCts();
        }

        private void Update()
        {
            if (!m_isActive)
            {
                return;
            }

            // 투사체가 무언가에 막혀 멈춰있는지 감지 (벽 등)
            UpdateStoppedDetection();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!m_isActive || !other.CompareTag("Mob"))
            {
                return;
            }

            // 적중 처리
            if (other.TryGetComponent(out MobBase mob))
            {
                mob.TakeDamage(m_attackPower, m_stunTime);
            }

            // 진화 여부에 따른 유도 폭발 또는 즉시 제거
            if (m_isEvolved)
            {
                BulletExplosion();
            }
            else
            {
                ReleaseToPool();
            }
        }

        #endregion

        #region 초기화 및 상태 관리

        /// <summary>
        /// 투사체의 전투 파라미터를 초기화합니다. (Initialize -> Init)
        /// </summary>
        public void Init(float damage, float stunTime, float speed, bool isEvolved)
        {
            m_attackPower = damage;
            m_stunTime = stunTime;
            BulletSpeed = speed;
            m_isEvolved = isEvolved;
        }

        /// <summary>
        /// 투사체를 지정된 방향으로 발사합니다.
        /// </summary>
        public void ThrowBullet(Vector3 direction)
        {
            ThrowAndTrackLifecycleAsync(direction).Forget();
        }

        /// <summary>
        /// 풀에서 재사용될 때 상태를 초기화합니다.
        /// </summary>
        public void ResetState()
        {
            m_isActive = true;
            m_transform.DOKill();
            m_transform.rotation = Quaternion.identity;
            
            CancelLifetimeCts();
            
            m_lastPosition = m_transform.position;
            m_stoppedTime = 0f;
        }

        #endregion

        #region 비동기 연출 및 수명 주기

        /// <summary>
        /// 투사체 이동 및 비행 수명 주기를 처리하는 비동기 메서드입니다.
        /// </summary>
        private async UniTaskVoid ThrowAndTrackLifecycleAsync(Vector3 direction)
        {
            m_attackDirection = direction.normalized;
            Vector3 targetPosition = m_transform.position + m_attackDirection * m_travelDistance;
            float duration = m_travelDistance / BulletSpeed;

            // 새로운 수명 토큰 생성
            CancelLifetimeCts();
            m_lifetimeCts = new CancellationTokenSource();
            
            float maxLifetime = duration + k_LifetimeBuffer;
            LifetimeGuardAsync(maxLifetime, m_lifetimeCts.Token).Forget();

            var onDestroyToken = this.GetCancellationTokenOnDestroy();

            // 이전 트윈 정리
            m_transform.DOKill();
            
            // 1. 무한 회전
            _ = m_transform.DORotate(new Vector3(0, 0, m_rotateSpeed), 1f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental);

            // 2. 목적지까지 직선 이동
            m_moveTween = m_transform.DOMove(targetPosition, duration).SetEase(Ease.Linear);

            // 이동 완료 또는 취소 대기
            bool cancelled = await m_moveTween.ToUniTask(cancellationToken: onDestroyToken).SuppressCancellationThrow();

            if (!cancelled && m_isActive)
            {
                ReleaseToPool();
            }
        }

        /// <summary>
        /// 진화된 상태일 때 적중 시 발생하는 폭발 효과를 처리합니다.
        /// </summary>
        private void BulletExplosion()
        {
            Vector3 currentPosition = m_transform.position;
            
            // 시각 효과 재생
            EffectManager.Instance.PlayEffect(EffectType.BoneExplosion, currentPosition);

            // 반경 내 적들에게 광역 데미지
            int numColliders = Physics2D.OverlapCircle(currentPosition, m_explosionRadius, m_contactFilter, m_overlapResults);

            for (int i = 0; i < numColliders; i++)
            {
                if (m_overlapResults[i].TryGetComponent(out MobBase mob))
                {
                    mob.TakeDamage(m_explosionDamage);
                }
            }

            ReleaseToPool();
        }

        #endregion

        #region 유틸리티 및 풀링

        /// <summary>
        /// 투사체가 특정 지점에서 멈춰있는지 확인하여 비정상 상태 시 회수합니다.
        /// </summary>
        private void UpdateStoppedDetection()
        {
            Vector3 currentPos = m_transform.position;
            float sqrDistanceMoved = (currentPos - m_lastPosition).sqrMagnitude;
            
            if (sqrDistanceMoved < k_StopThresholdSqr)
            {
                m_stoppedTime += Time.deltaTime;
                if (m_stoppedTime >= k_MaxStoppedDuration)
                {
                    ReleaseToPool();
                }
            }
            else
            {
                m_stoppedTime = 0f;
                m_lastPosition = currentPos;
            }
        }

        /// <summary>
        /// 투사체가 미적중 상태로 비행 환경에 남겨지는 것을 방지하는 타임아웃 가드입니다.
        /// </summary>
        private async UniTaskVoid LifetimeGuardAsync(float maxLifetime, CancellationToken token)
        {
            try
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(maxLifetime), cancellationToken: token);
                
                if (m_isActive)
                {
                    ReleaseToPool();
                }
            }
            catch (System.OperationCanceledException)
            {
                // 취소 시 무시
            }
        }

        /// <summary>
        /// 현재 수명 주기의 CancellationTokenSource를 취소하고 정리합니다.
        /// </summary>
        private void CancelLifetimeCts()
        {
            if (m_lifetimeCts != null)
            {
                m_lifetimeCts.Cancel();
                m_lifetimeCts.Dispose();
                m_lifetimeCts = null;
            }
        }

        /// <summary>
        /// 투사체를 풀로 반환합니다.
        /// </summary>
        private void ReleaseToPool()
        {
            if (!m_isActive)
            {
                return;
            }
            
            m_isActive = false;
            m_transform.DOKill();
            CancelLifetimeCts();
            
            if (WeaponPoolManager.Instance != null)
            {
                WeaponPoolManager.Instance.Release(this);
            }
        }

        #endregion
    }
}
