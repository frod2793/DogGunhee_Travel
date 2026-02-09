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
        #region 내부 상태 및 변수

        [Header("참조")]
        private Transform m_playerTransform;
        private SpriteRenderer m_spriteRenderer;
        private TrailRenderer m_trailRenderer;

        private float m_attackPower;
        private float m_mobStunTime;

        // 중복 타격 방지를 위한 히트 기록 (왕복 시 초기화)
        private readonly HashSet<int> m_hitHistory = new HashSet<int>();

        [Header("트윈 애니메이션")]
        private Tween m_rotateTween;
        private Tween m_moveTween;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
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
            
            // 시각 효과 리셋
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

        private void OnDisable()
        {
            // 트윈 및 비동기 작업 정리
            m_rotateTween?.Kill();
            m_moveTween?.Kill();
            transform.DOKill();
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
                    mob.PlayDamageEffect();
                }
            }
        }

        #endregion

        #region 초기화 및 루틴

        /// <summary>
        /// 부메랑 투사체를 초기화하고 발사 시퀀스를 시작합니다. (Initialize -> Init)
        /// </summary>
        public void Init(
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

                // 1. 회전 트윈 시작
                m_rotateTween?.Kill();
                m_rotateTween = transform.DORotate(new Vector3(0, 0, 360f), 1f / rotationsPerSecond, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Incremental)
                    .SetLink(gameObject);

                // 2. 바깥 방향으로 발사 (OutQuad로 서서히 멈추는 느낌 구현)
                m_moveTween?.Kill();
                await transform.DOMove(originPosition + (direction * distance), distance / speed)
                    .SetEase(Ease.OutQuad)
                    .ToUniTask(cancellationToken: token);

                // 3. 반환 전 대기 및 타격 기록 초기화
                m_hitHistory.Clear();
                await UniTask.Delay(System.TimeSpan.FromSeconds(returnDelay), cancellationToken: token);

                // 4. 플레이어를 추적하며 복귀
                bool hasStartedFadeOut = false;
                while (gameObject.activeInHierarchy && m_playerTransform != null)
                {
                    Vector3 currentPos = transform.position;
                    Vector3 targetPos = m_playerTransform.position;
                    
                    transform.position = Vector3.MoveTowards(currentPos, targetPos, speed * Time.deltaTime);
                    
                    float distToPlayer = Vector3.Distance(currentPos, targetPos);

                    // 도착 임팩트를 위해 가까워지면 페이드 아웃 연출
                    if (!hasStartedFadeOut && distToPlayer <= 1.5f)
                    {
                        hasStartedFadeOut = true;
                        if (m_spriteRenderer != null)
                        {
                            _ = m_spriteRenderer.DOFade(0f, 0.2f);
                        }

                        if (m_trailRenderer != null)
                        {
                            m_trailRenderer.emitting = false;
                        }
                    }

                    if (distToPlayer < 0.2f)
                    {
                        break;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (System.OperationCanceledException)
            {
                // 정상 종료
            }
            finally
            {
                if (gameObject.activeInHierarchy)
                {
                    WeaponPoolManager.Instance.Release(this);
                }
            }
        }

        #endregion
    }
}
