using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.Mob.MobBase;
using InGame.ObjectPool;
using UnityEngine;

namespace InGame.Weapon
{
    /// <summary>
    /// 히어로 랜딩(Shield) 무기에서 발사되는 부메랑 투사체입니다.
    /// 목표 지점까지 이동 후 플레이어에게 복귀하며 경로상의 적을 타격합니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ShieldProjectile : MonoBehaviour
    {
        #region 필드 및 프로퍼티
        [Header("참조")]
        private Transform m_playerTransform;
        private SpriteRenderer m_spriteRenderer;
        private TrailRenderer m_trailRenderer;

        [Header("상태 데이터")]
        private float m_attackPower;
        private float m_mobStunTime;

        // 중복 타격 방지를 위한 히트 기록 (왕복 시 초기화 필요)
        private readonly HashSet<int> m_hitHistory = new HashSet<int>();

        [Header("트윈 애니메이션")]
        private Tween m_rotateTween;
        private Tween m_moveTween;
        #endregion

        #region Unity 라이프사이클
        private void Awake()
        {
            // 트리거 충돌 설정 강제
            if (TryGetComponent<Collider2D>(out var col))
            {
                col.isTrigger = true;
            }
            
            m_spriteRenderer = GetComponent<SpriteRenderer>();
            m_trailRenderer = GetComponent<TrailRenderer>();
        }

        private void OnEnable()
        {
            m_hitHistory.Clear();
            
            // 트레일 및 시각 효과 초기화
            if (m_trailRenderer != null)
            {
                m_trailRenderer.Clear();
                m_trailRenderer.emitting = true;
            }
            
            if (m_spriteRenderer != null)
            {
                var color = m_spriteRenderer.color;
                color.a = 1f;
                m_spriteRenderer.color = color;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // 적 태그 확인 및 유효성 검사
            if (other.CompareTag("Mob") && other.TryGetComponent<MobBase>(out var mob))
            {
                int id = other.GetInstanceID();
                
                // 중복 타격 방지 및 생존 확인
                if (!m_hitHistory.Contains(id) && !mob.IsDead)
                {
                    m_hitHistory.Add(id);
                    mob.TakeDamage(m_attackPower, m_mobStunTime);
                    
                    // 피격 연출 (Flash 등) 실행
                    mob.PlayDamageEffect();
                }
            }
        }

        private void OnDisable()
        {
            // 객체 비활성화 시 진행 중인 모든 트윈 정리 (메모리 누수 방지)
            m_rotateTween?.Kill();
            m_moveTween?.Kill();
            transform.DOKill();
        }
        #endregion

        #region 초기화 및 애니메이션 로직
        /// <summary>
        /// 부메랑 투사체를 초기화하고 발사 시퀀스를 시작합니다.
        /// </summary>
        public void Initialize(
            float attackPower, 
            float mobStunTime, 
            Transform playerTransform, 
            Vector3 direction, 
            float speed, 
            float distance, 
            float returnDelay, 
            float rotationsPerSecond)
        {
            m_playerTransform = playerTransform;
            m_attackPower = attackPower;
            m_mobStunTime = mobStunTime;
            
            // 비동기 애니메이션 실행 (Fire and Forget)
            AnimateBoomerangAsync(direction, speed, distance, returnDelay, rotationsPerSecond).Forget();
        }

        /// <summary>
        /// 부메랑의 왕복 이동 및 회전 애니메이션을 제어합니다.
        /// </summary>
        private async UniTaskVoid AnimateBoomerangAsync(
            Vector3 direction, 
            float speed, 
            float distance, 
            float returnDelay, 
            float rotationsPerSecond)
        {
            var token = this.GetCancellationTokenOnDestroy();
            
            try
            {
                Vector3 originPosition = transform.position;

                // 1. 회전 트윈 시작 (무한 루프)
                m_rotateTween?.Kill();
                m_rotateTween = transform.DORotate(new Vector3(0, 0, 360f), 1f / rotationsPerSecond, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Incremental)
                    .SetLink(gameObject);

                // 2. 바깥 방향으로 발사
                m_moveTween?.Kill();
                await transform.DOMove(originPosition + (direction * distance), distance / speed)
                    .SetEase(Ease.OutQuad)
                    .ToUniTask(cancellationToken: token);

                // 3. 반환 전 대기 및 타격 기록 초기화 (돌아올 때 다시 때릴 수 있게 함)
                m_hitHistory.Clear();
                await UniTask.Delay(System.TimeSpan.FromSeconds(returnDelay), cancellationToken: token);

                // 4. 플레이어를 추적하며 복귀
                bool hasStartedFadeOut = false;
                while (gameObject.activeInHierarchy && m_playerTransform != null)
                {
                    Vector3 currentPos = transform.position;
                    Vector3 targetPos = m_playerTransform.position;
                    
                    // 플레이어 방향으로 이동
                    transform.position = Vector3.MoveTowards(currentPos, targetPos, speed * Time.deltaTime);
                    
                    float distToPlayer = Vector3.Distance(currentPos, targetPos);

                    // 도착 임계값 도달 시 페이드 아웃 연출
                    if (!hasStartedFadeOut && distToPlayer <= 1.5f)
                    {
                        hasStartedFadeOut = true;
                        if (m_spriteRenderer != null) _ = m_spriteRenderer.DOFade(0f, 0.2f);
                        if (m_trailRenderer != null) m_trailRenderer.emitting = false;
                    }

                    // 최종 회복 처리
                    if (distToPlayer < 0.2f) break;

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (System.OperationCanceledException)
            {
                // 취소 시 무시
            }
            finally
            {
                // 작업을 마치면 풀로 반환
                if (gameObject.activeInHierarchy)
                {
                    WeaponPoolManager.Instance.Release(this);
                }
            }
        }
        #endregion
    }
}
