using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.Mob.MobBase;
using InGame.ObjectPool;

namespace InGame.Weapon
{
    /// <summary>
    /// [설명]: 히어로 랜딩(Shield) 무기에서 발사되는 방패 파편(부메랑) 투사체입니다.
    /// 목표 지점까지 포물선 혹은 직선으로 이동 후 플레이어에게 복귀하며 경로상의 적을 타격합니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
    public class ShieldProjectile : MonoBehaviour
    {
        #region 내부 변수 및 컴포넌트

        // 컴포넌트 참조
        private Transform m_playerTransform;
        private SpriteRenderer m_spriteRenderer;
        private TrailRenderer m_trailRenderer;
        private WeaponPoolManager m_poolManager;

        // 전투 데이터
        private float m_attackPower;
        private float m_mobStunTime;

        // 상태 및 기록
        private readonly HashSet<int> m_hitHistory = new HashSet<int>();
        private Tween m_rotateTween;
        private Tween m_moveTween;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            // 컴포넌트 캐싱
            m_spriteRenderer = GetComponent<SpriteRenderer>();
            m_trailRenderer = GetComponent<TrailRenderer>();

            if (TryGetComponent<Collider2D>(out var col))
            {
                col.isTrigger = true;
            }
        }

        private void OnEnable()
        {
            // 초기 상태 리셋
            m_hitHistory.Clear();

            if (m_trailRenderer != null)
            {
                m_trailRenderer.Clear();
                m_trailRenderer.emitting = true;
            }

            if (m_spriteRenderer != null)
            {
                Color color = m_spriteRenderer.color;
                color.a = 1f;
                m_spriteRenderer.color = color;
            }
        }

        private void OnDisable()
        {
            // 모든 연출 및 비동기 작업 정리
            Cleanup();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Mob")) return;

            if (other.TryGetComponent(out MobBase mob) && !mob.IsDead)
            {
                int id = other.GetInstanceID();

                // 중복 타격 방지 (왕복 시 1회씩 타격)
                if (!m_hitHistory.Contains(id))
                {
                    m_hitHistory.Add(id);
                    mob.TakeDamage(m_attackPower, m_mobStunTime);
                }
            }
        }

        #endregion

        #region 초기화 및 제어

        /// <summary>
        /// [설명]: 부메랑 투사체를 초기화하고 발사 시퀀스를 시작합니다.
        /// </summary>
        /// <param name="attackPower">공격력</param>
        /// <param name="mobStunTime">경직 시간</param>
        /// <param name="playerTransform">복귀 대상(플레이어)</param>
        /// <param name="poolManager">오브젝트 풀 매니저</param>
        /// <param name="direction">발사 방향</param>
        /// <param name="speed">이동 속도</param>
        /// <param name="distance">최대 사거리</param>
        /// <param name="returnDelay">정점 대기 시간</param>
        /// <param name="rotationsPerSecond">초당 회전수</param>
        public void Init(
            float attackPower,
            float mobStunTime,
            Transform playerTransform,
            WeaponPoolManager poolManager,
            Vector3 direction,
            float speed,
            float distance,
            float returnDelay,
            float rotationsPerSecond)
        {
            m_playerTransform = playerTransform;
            m_poolManager = poolManager;
            m_attackPower = attackPower;
            m_mobStunTime = mobStunTime;

            // 이동 및 회전 루틴 시작
            AnimateBoomerangAsync(direction, speed, distance, returnDelay, rotationsPerSecond).Forget();
        }

        private void Cleanup()
        {
            m_rotateTween?.Kill();
            m_moveTween?.Kill();
            transform.DOKill();
        }

        private void ReleaseToPool()
        {
            if (gameObject.activeInHierarchy && m_poolManager != null)
            {
                m_poolManager.Release(this);
            }
        }

        #endregion

        #region 비동기 이동 시퀀스

        /// <summary>
        /// [설명]: 부메랑의 왕복 이동 및 회전 애니메이션을 제어하는 메인 시퀀스입니다.
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

                // 1. 자전 회전 시작 (무한 루프)
                m_rotateTween?.Kill();
                float rotateDuration = rotationsPerSecond > 0 ? 1f / rotationsPerSecond : 0.5f;
                m_rotateTween = transform.DORotate(new Vector3(0, 0, 360f), rotateDuration, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Incremental)
                    .SetLink(gameObject);

                // 2. [Outward] 바깥 방향으로 발사
                m_moveTween?.Kill();
                float travelTime = speed > 0 ? distance / speed : 1f;
                await transform.DOMove(originPosition + (direction * distance), travelTime)
                    .SetEase(Ease.OutQuad) // 정점에서 자연스럽게 감속
                    .ToUniTask(cancellationToken: token);

                // 3. [Hold] 반환 전 짧은 대기 및 피격 기록 초기화
                m_hitHistory.Clear();
                await UniTask.Delay(System.TimeSpan.FromSeconds(returnDelay), cancellationToken: token);

                // 4. [Return] 플레이어를 실시간 추적하며 복귀
                await ReturnToPlayerAsync(speed, token);
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
        /// [설명]: 플레이어 위치를 향해 실시간으로 이동하며 복귀하는 루프입니다.
        /// </summary>
        private async UniTask ReturnToPlayerAsync(float speed, System.Threading.CancellationToken token)
        {
            bool hasStartedFadeOut = false;

            while (gameObject.activeInHierarchy && m_playerTransform != null)
            {
                Vector3 currentPos = transform.position;
                Vector3 targetPos = m_playerTransform.position;

                // 플레이어 방향으로 추적 이동
                transform.position = Vector3.MoveTowards(currentPos, targetPos, speed * Time.deltaTime);

                float distToPlayer = Vector3.Distance(currentPos, targetPos);

                // 소멸 전 페이드 아웃 연출
                if (!hasStartedFadeOut && distToPlayer <= 1.5f)
                {
                    hasStartedFadeOut = true;
                    if (m_spriteRenderer != null) _ = m_spriteRenderer.DOFade(0f, 0.2f);
                    if (m_trailRenderer != null) m_trailRenderer.emitting = false;
                }

                // 플레이어 도달 시 루프 탈출
                if (distToPlayer < 0.2f) break;

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        #endregion
    }
}