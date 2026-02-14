using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;
using InGame.Mob.MobBase;
using InGame.ObjectPool;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// [설명]: 부메랑 투사체(BoomerangProjectile) 클래스입니다.
    /// 일정 거리를 날아갔다가 플레이어 위치로 다시 돌아오는 왕복 로직을 수행합니다.
    /// TrailRenderer와 DOTween을 사용하여 시각적 연출을 처리합니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
    [RequireComponent(typeof(TrailRenderer))]
    public class BoomerangProjectile : MonoBehaviour
    {
        #region 내부 변수 및 설정

        [Header("시각 효과 설정")]
        [Tooltip("트레일(잔상)의 유지 시간")]
        [SerializeField] private float m_trailTime = 0.2f;

        [Tooltip("트레일의 시작 부분 두께")]
        [SerializeField] private float m_trailStartWidth = 0.5f;

        [Tooltip("트레일의 기본 색상")]
        [SerializeField] private Color m_trailColor = new Color(1, 1, 1, 0.5f);

        [Header("이동 설정")]
        [Tooltip("날아갈 때(Outward)의 속도 배율")]
        [SerializeField] private float m_outwardSpeedMultiplier = 1.5f;

        [Tooltip("돌아올 때(Return)의 속도 배율")]
        [SerializeField] private float m_returnSpeedMultiplier = 1.2f;

        // 컴포넌트 캐싱
        private Transform m_transform;
        private SpriteRenderer m_spriteRenderer;
        private TrailRenderer m_trailRenderer;

        // 런타임 데이터
        private Transform m_playerTransform;
        private float m_damage;
        private float m_stunTime;
        private float m_baseSpeed;
        private float m_maxDistance;
        
        // 상태 관리
        private System.Action m_onReturnCallback;
        private WeaponPoolManager m_poolManager;
        private readonly HashSet<int> m_hitHistory = new HashSet<int>();

        // 연출용 트윈
        private Tween m_rotateTween;
        private Tween m_fadeTween;
        private Tween m_trailFadeTween;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_transform = transform;
            m_spriteRenderer = GetComponent<SpriteRenderer>();
            m_trailRenderer = GetComponent<TrailRenderer>();
            
            // 트레일 초기 설정
            SetupTrail();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Mob")) return;

            // 중복 피격 방지 (한 번의 왕복 내에서)
            int id = other.gameObject.GetInstanceID();
            if (m_hitHistory.Contains(id)) return;

            if (other.TryGetComponent(out MobBase mob))
            {
                m_hitHistory.Add(id);
                mob.TakeDamage(m_damage, m_stunTime);
            }
        }

        private void OnDisable()
        {
            // 비활성화 시 트윈 정리
            KillAllTweens();
        }

        #endregion

        #region 초기화 및 제어

        /// <summary>
        /// [설명]: 투사체를 초기화하고 발사 시퀀스를 시작합니다.
        /// </summary>
        /// <param name="player">돌아올 대상(플레이어)</param>
        /// <param name="damage">공격력</param>
        /// <param name="stunTime">스턴 시간</param>
        /// <param name="speed">기본 이동 속도</param>
        /// <param name="distance">최대 사거리</param>
        /// <param name="poolManager">반환할 풀 매니저</param>
        /// <param name="onReturnComplete">복귀 완료 시 호출할 콜백</param>
        public void Init(
            Transform player,
            float damage,
            float stunTime,
            float speed,
            float distance,
            WeaponPoolManager poolManager,
            System.Action onReturnComplete = null)
        {
            m_playerTransform = player;
            m_damage = damage;
            m_stunTime = stunTime;
            m_baseSpeed = speed;
            m_maxDistance = distance;
            m_poolManager = poolManager;
            m_onReturnCallback = onReturnComplete;

            m_hitHistory.Clear();

            // 트레일 및 비주얼 리셋
            ResetVisuals();

            // 회전 애니메이션 시작 (무한 루프)
            m_rotateTween = m_transform
                .DORotate(new Vector3(0, 0, 720), 0.5f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental);

            // 비동기 이동 로직 시작
            LaunchSequenceAsync().Forget();
        }

        /// <summary>
        /// [설명]: 트레일 렌더러의 기본 속성을 설정합니다.
        /// </summary>
        private void SetupTrail()
        {
            if (m_trailRenderer == null) return;

            m_trailRenderer.time = m_trailTime;
            m_trailRenderer.startWidth = m_trailStartWidth;
            m_trailRenderer.endWidth = 0f;
            m_trailRenderer.autodestruct = false;

            // 머티리얼이 없거나 기본이면 스프라이트용 머티리얼 할당
            if (m_trailRenderer.material == null || m_trailRenderer.material.name.StartsWith("Default-Material"))
            {
                m_trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }

            // 그라디언트 설정 (투명해지도록)
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(m_trailColor, 0.0f), new GradientColorKey(m_trailColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(m_trailColor.a, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            m_trailRenderer.colorGradient = gradient;
            
            // 스프라이트 뒤로 가도록 정렬
            if (m_spriteRenderer != null)
            {
                m_trailRenderer.sortingOrder = m_spriteRenderer.sortingOrder - 1;
            }
        }

        private void ResetVisuals()
        {
            KillAllTweens();

            // 스프라이트 알파 초기화
            if (m_spriteRenderer != null)
            {
                Color c = m_spriteRenderer.color;
                c.a = 1f;
                m_spriteRenderer.color = c;
            }

            // 트레일 초기화
            if (m_trailRenderer != null)
            {
                m_trailRenderer.Clear();
                m_trailRenderer.emitting = true;
                if (m_trailRenderer.material != null)
                {
                    m_trailRenderer.material.color = Color.white;
                }
            }
        }

        private void KillAllTweens()
        {
            m_rotateTween?.Kill();
            m_fadeTween?.Kill();
            m_trailFadeTween?.Kill();
        }

        /// <summary>
        /// [설명]: 투사체를 풀로 반환하고 콜백을 호출합니다.
        /// </summary>
        private void ReleaseToPool()
        {
            // 콜백 호출
            m_onReturnCallback?.Invoke();
            m_onReturnCallback = null;

            KillAllTweens();

            // 트레일 정리
            if (m_trailRenderer != null)
            {
                m_trailRenderer.emitting = false;
            }

            // 풀 반환
            if (gameObject.activeSelf && m_poolManager != null)
            {
                m_poolManager.Release(this);
            }
            else if (m_poolManager == null) // 풀링 사용 안 하는 경우
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region 이동 시퀀스

        /// <summary>
        /// [설명]: 부메랑의 왕복 이동(전진 -> 대기 -> 복귀) 시퀀스를 처리합니다.
        /// </summary>
        private async UniTaskVoid LaunchSequenceAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();

            try
            {
                // 1. 발사 연출 (Fade In)
                if (m_spriteRenderer != null)
                {
                    Color c = m_spriteRenderer.color;
                    c.a = 0f;
                    m_spriteRenderer.color = c;
                    m_fadeTween = m_spriteRenderer.DOFade(1f, 0.15f).SetEase(Ease.OutQuad);
                }

                Vector3 startPos = m_transform.position;
                Vector3 targetPos = startPos + m_transform.up * m_maxDistance;

                // 2. [Outward] 전방으로 이동
                float outwardSpeed = m_baseSpeed * m_outwardSpeedMultiplier;
                float outDuration = (outwardSpeed > 0) ? m_maxDistance / outwardSpeed : 1f;

                await m_transform
                    .DOMove(targetPos, outDuration)
                    .SetEase(Ease.OutSine)
                    .ToUniTask(cancellationToken: token);

                // 3. [Hold] 정점에서 잠시 대기 및 피격 기록 초기화 (돌아올 때 다시 때릴 수 있게)
                m_hitHistory.Clear();
                await UniTask.Delay(100, cancellationToken: token);

                // 4. [Return] 플레이어 추적 복귀
                await ReturnToPlayerAsync(token);
            }
            catch (System.OperationCanceledException)
            {
                // 정상 취소
            }
            finally
            {
                ReleaseToPool();
            }
        }

        /// <summary>
        /// [설명]: 플레이어 위치를 추적하며 돌아오는 로직입니다.
        /// </summary>
        private async UniTask ReturnToPlayerAsync(System.Threading.CancellationToken token)
        {
            bool hasStartedFadeOut = false;
            float returnSpeed = m_baseSpeed * m_returnSpeedMultiplier;

            while (!token.IsCancellationRequested)
            {
                if (m_playerTransform == null) break;

                Vector3 myPos = m_transform.position;
                Vector3 playerPos = m_playerTransform.position;
                float distToPlayer = Vector3.Distance(myPos, playerPos);

                // 플레이어 쪽으로 이동
                float step = returnSpeed * Time.deltaTime;
                m_transform.position = Vector3.MoveTowards(myPos, playerPos, step);

                // 도착 직전 페이드 아웃 시작
                if (!hasStartedFadeOut && distToPlayer <= 1.5f)
                {
                    hasStartedFadeOut = true;
                    StartFadeOut();
                }

                // 플레이어 도달 확인
                if (distToPlayer < 0.5f)
                {
                    break;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
            }
        }

        private void StartFadeOut()
        {
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

        #endregion
    }
}