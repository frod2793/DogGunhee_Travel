using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.Managers;
using InGame.Mob.MobBase;
using InGame.ObjectPool;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// [설명]: 개다귀(Bone) 무기의 투사체 로직을 처리하는 컴포넌트입니다.
    /// 직선 이동과 회전 연출을 수행하며, 벽에 부딪히거나 일정 시간 후 소멸합니다.
    /// 진화 시(Evolution) 적중 후 폭발하여 광역 데미지를 입힙니다.
    /// </summary>
    public class BoneBullet : MonoBehaviour
    {
        #region 내부 변수 및 설정

        [Header("이동 설정")]
        [Tooltip("투사체가 날아가는 최대 거리")]
        [SerializeField] private float m_travelDistance = 20f;

        [Header("회전 설정")]
        [Tooltip("초당 회전 속도 (도/s)")]
        [SerializeField] private float m_rotateSpeed = 720f;

        [Header("진화(폭발) 설정")]
        [Tooltip("폭발 반경")]
        [SerializeField] private float m_explosionRadius = 1.5f;

        [Tooltip("폭발 시 발생하는 추가 고정 데미지")]
        [SerializeField] private float m_explosionDamage = 10f;

        [Header("감지 설정")]
        [Tooltip("적(Mob)으로 인식할 레이어")]
        [SerializeField] private LayerMask m_mobLayerMask;

        // 상수
        private const float k_StopThreshold = 0.01f;
        private const float k_StopThresholdSqr = k_StopThreshold * k_StopThreshold;
        private const float k_MaxStoppedDuration = 0.3f; // 벽에 끼임 판정 시간
        private const float k_LifetimeBuffer = 2f;       // 안전용 추가 수명
        private const int k_MaxOverlapColliders = 10;    // 폭발 감지 최대 개수

        // 컴포넌트 및 트랜스폼 캐싱
        private Transform m_transform;
        
        // 런타임 상태
        private Vector3 m_attackDirection;
        private Vector3 m_lastPosition;
        private float m_stoppedTime;
        private bool m_isActive;
        
        // 데이터
        private float m_attackPower;
        private float m_stunTime;
        private float m_bulletSpeed;
        private bool m_isEvolved;
        
        // 관리 객체
        private WeaponPoolManager m_poolManager;
        private InGame.Services.ISoundManager m_soundManager;
        private IEffectService m_effectService;
        private CancellationTokenSource m_lifetimeCts;
        private Tween m_moveTween;
        private Tween m_rotateTween;

        // 물리 연산 캐시
        private readonly Collider2D[] m_overlapResults = new Collider2D[k_MaxOverlapColliders];
        private ContactFilter2D m_contactFilter;

        #endregion

        #region 프로퍼티

        /// <summary>
        /// 투사체의 이동 속도
        /// </summary>
        public float BulletSpeed
        {
            get => m_bulletSpeed;
            set => m_bulletSpeed = value;
        }

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_transform = transform;

            // 물리 감지용 필터 설정 (GC Alloc 방지)
            m_contactFilter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = true,
                layerMask = m_mobLayerMask
            };
        }

        private void OnEnable()
        {
            m_isActive = true;
            m_lastPosition = m_transform.position;
            m_stoppedTime = 0f;

            m_isActive = true;
            m_lastPosition = m_transform.position;
            m_stoppedTime = 0f;
        }

        private void OnDisable()
        {
            m_isActive = false;
            Cleanup();
        }

        private void Update()
        {
            if (!m_isActive) return;

            // 투사체가 벽 등에 막혀 멈춰있는지 감지
            UpdateStoppedDetection();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!m_isActive || !other.CompareTag("Mob")) return;

            // 1. 단일 대상 적중 처리
            if (other.TryGetComponent(out MobBase mob))
            {
                mob.TakeDamage(m_attackPower, m_stunTime);
            }

            // 2. 진화 여부에 따른 처리
            if (m_isEvolved)
            {
                Explode();
            }
            else
            {
                ReleaseToPool();
            }
        }

        #endregion

        #region 초기화 및 제어

        /// <summary>
        /// [설명]: 투사체의 전투 파라미터를 초기화합니다.
        /// </summary>
        /// <param name="damage">기본 데미지</param>
        /// <param name="stunTime">스턴 시간</param>
        /// <param name="speed">이동 속도</param>
        /// <param name="isEvolved">진화 여부(폭발 효과)</param>
        /// <param name="poolManager">오브젝트 풀 매니저</param>
        /// <param name="soundManager">사운드 매니저 (DI)</param>
        /// <param name="effectService">이펙트 서비스 (DI)</param>
        public void Init(float damage, float stunTime, float speed, bool isEvolved, WeaponPoolManager poolManager, InGame.Services.ISoundManager soundManager, IEffectService effectService)
        {
            m_attackPower = damage;
            m_stunTime = stunTime;
            m_bulletSpeed = speed;
            m_isEvolved = isEvolved;
            m_poolManager = poolManager;
            m_soundManager = soundManager;
            m_effectService = effectService;
        }

        /// <summary>
        /// [설명]: 투사체를 지정된 방향으로 발사하고 수명 주기를 관리합니다.
        /// </summary>
        public void ThrowBullet(Vector3 direction)
        {
            // 방향 보정
            if (direction == Vector3.zero) direction = Random.insideUnitCircle.normalized;

            // 사운드 재생 (투사체가 날아갈 때 재생)
            if (m_soundManager != null)
            {
                m_soundManager.Play(SoundKeys.Throwbone.ToString(), Sound.SFX);
            }

            ThrowAndTrackLifecycleAsync(direction).Forget();
        }

        /// <summary>
        /// [설명]: 풀에서 재사용될 때 상태를 리셋합니다.
        /// </summary>
        public void ResetState()
        {
            m_isActive = true;
            Cleanup(); // 이전 트윈 및 토큰 정리

            m_transform.rotation = Quaternion.identity;
            m_lastPosition = m_transform.position;
            m_stoppedTime = 0f;
        }

        private void Cleanup()
        {
            // DOTween 정리
            if (m_moveTween != null && m_moveTween.IsActive()) m_moveTween.Kill();
            if (m_rotateTween != null && m_rotateTween.IsActive()) m_rotateTween.Kill();

            // CancellationToken 정리
            if (m_lifetimeCts != null)
            {
                m_lifetimeCts.Cancel();
                m_lifetimeCts.Dispose();
                m_lifetimeCts = null;
            }
        }

        #endregion

        #region 이동 및 연출

        /// <summary>
        /// [설명]: 투사체 이동 및 비행 수명 주기를 처리하는 비동기 메서드입니다.
        /// </summary>
        private async UniTaskVoid ThrowAndTrackLifecycleAsync(Vector3 direction)
        {
            m_attackDirection = direction.normalized;
            Vector3 targetPosition = m_transform.position + m_attackDirection * m_travelDistance;
            
            // 속도가 0이면 즉시 종료
            if (m_bulletSpeed <= 0)
            {
                ReleaseToPool();
                return;
            }

            float duration = m_travelDistance / m_bulletSpeed;

            // 수명 관리용 토큰 생성
            Cleanup();
            m_lifetimeCts = new CancellationTokenSource();
            
            // 안전장치: 예상 시간보다 훨씬 오래 살아있으면 강제 회수
            LifetimeGuardAsync(duration + k_LifetimeBuffer, m_lifetimeCts.Token).Forget();

            // 객체 파괴 시 취소 토큰
            var destroyToken = this.GetCancellationTokenOnDestroy();

            // 1. 무한 회전 연출
            m_rotateTween = m_transform
                .DORotate(new Vector3(0, 0, m_rotateSpeed), 1f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental);

            // 2. 목적지까지 직선 이동
            m_moveTween = m_transform
                .DOMove(targetPosition, duration)
                .SetEase(Ease.Linear);

            // 이동 완료 대기 (취소 예외는 무시하고 bool 반환)
            // SuppressCancellationThrow로 인해 취소 시 false가 아니라 true로 동작할 수 있으므로 주의 (ToUniTask 반환값: 취소여부 아님)
            // ToUniTask().SuppressCancellationThrow()는 (bool cancelled, TResult result)가 아니라 (bool isCanceled)를 리턴함. (UniTask v2 기준)
            bool cancelled = await m_moveTween.ToUniTask(cancellationToken: destroyToken).SuppressCancellationThrow();

            // 정상적으로 이동이 끝났고 아직 활성 상태라면 회수 (타겟에 맞지 않고 사거리 끝까지 감)
            if (!cancelled && m_isActive)
            {
                ReleaseToPool();
            }
        }

        /// <summary>
        /// [설명]: 진화 상태일 때 적중 시 발생하는 폭발 효과를 처리합니다.
        /// </summary>
        private void Explode()
        {
            Vector3 currentPosition = m_transform.position;

            // 1. 시각 효과 재생
            if (m_effectService != null)
            {
                m_effectService.PlayEffect(EffectType.BoneExplosion, currentPosition);
            }

            // 2. 반경 내 적들에게 광역 데미지
            int numColliders = Physics2D.OverlapCircle(currentPosition, m_explosionRadius, m_contactFilter, m_overlapResults);

            for (int i = 0; i < numColliders; i++)
            {
                var col = m_overlapResults[i];
                if (col == null) continue;

                if (col.TryGetComponent(out MobBase mob))
                {
                    mob.TakeDamage(m_explosionDamage);
                }
            }

            // 3. 투사체 소멸
            ReleaseToPool();
        }

        #endregion

        #region 유틸리티

        /// <summary>
        /// [설명]: 투사체가 벽 등에 막혀 제자리에서 멈춰있는지 감지하여 회수합니다.
        /// </summary>
        private void UpdateStoppedDetection()
        {
            Vector3 currentPos = m_transform.position;
            float sqrDistanceMoved = (currentPos - m_lastPosition).sqrMagnitude;

            // 거의 움직이지 않음
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
                // 움직이고 있음 -> 리셋
                m_stoppedTime = 0f;
                m_lastPosition = currentPos;
            }
        }

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
                // 정상 취소됨
            }
        }

        private void ReleaseToPool()
        {
            if (!m_isActive) return;

            m_isActive = false;
            Cleanup();

            if (m_poolManager != null)
            {
                m_poolManager.Release(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #endregion
    }
}