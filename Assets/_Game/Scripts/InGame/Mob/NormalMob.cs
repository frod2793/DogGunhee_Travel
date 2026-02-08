using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using InGame.Manager; // GameManager 사용

namespace InGame.Mob
{
    public class NormalMob : MobBase.MobBase
    {
        #region 인스펙터 필드

        [Header("몬스터 기본 스탯")]
        [SerializeField] private float m_initialHp = 100f;
        [SerializeField] private float m_initialSpeed = 1f;
        [SerializeField] private float m_initialAttackDamage = 10f;
        [SerializeField] private float m_initialStunTime = 0.1f;

        [Header("AI 설정")]
        [SerializeField] private float m_searchRange = 8f;
        [SerializeField] private Vector2 m_wanderWaitRange = new Vector2(1f, 3f);

        #endregion

        #region 내부 변수

        private enum AIState { None, Wandering, Chasing, Stunned, Dead }
        private AIState m_aiState = AIState.None;

        private Bounds m_mapBounds;
        private Tween m_moveTween;
        private Tween m_slowTween;
        private SpriteRenderer m_spriteRenderer;
        private Transform m_cachedTransform;
        
        private bool m_isAiPaused;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_cachedTransform = transform;
            m_spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            InitializeMob();
            if (GameManager.Instance != null)
            {
                m_mapBounds = GameManager.Instance.MapBounds;
            }
            StartAILoopAsync().Forget();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            KillAllTweens();
        }

        private void Update()
        {
            if (!IsMoveEnabled || IsDead || m_aiState == AIState.Stunned || m_aiState == AIState.Dead || m_isAiPaused) 
                return;

            CheckPlayerDetection();

            if (m_aiState == AIState.Chasing)
            {
                HandleChasingState();
            }
        }

        #endregion

        #region 초기화

        private void InitializeMob()
        {
            CurrentHp = m_initialHp;
            MoveSpeed = m_initialSpeed;
            AttackDamage = m_initialAttackDamage;
            StunTime = m_initialStunTime;
            m_aiState = AIState.None;
            m_isAiPaused = false;
            if (m_spriteRenderer != null) m_spriteRenderer.color = Color.white;
        }

        #endregion

        #region AI 로직

        private async UniTaskVoid StartAILoopAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();
            await UniTask.WaitUntil(() => m_player != null && m_player.transform.parent != null, cancellationToken: token);
            
            m_playerTransform = m_player.transform.parent;

            if (!m_mapBounds.Contains(m_cachedTransform.position))
            {
                await ReturnToMapAsync(token);
            }
            
            ChangeAIState(AIState.Wandering);
        }

        private void ChangeAIState(AIState newState)
        {
            if (m_aiState == newState && newState != AIState.Wandering) return;

            m_moveTween?.Kill();
            m_aiState = newState;

            switch (newState)
            {
                case AIState.Wandering:
                    SetState(MobState.Move);
                    StartWandering();
                    break;
                case AIState.Chasing:
                    SetState(MobState.Move);
                    break;
                case AIState.Stunned:
                    SetState(MobState.Stun);
                    break;
                case AIState.Dead:
                    SetState(MobState.Die);
                    break;
            }
        }

        private void CheckPlayerDetection()
        {
            if (m_playerTransform == null) return;

            float distSqr = (m_cachedTransform.position - m_playerTransform.position).sqrMagnitude;
            float rangeSqr = m_searchRange * m_searchRange;

            if (m_aiState == AIState.Wandering && distSqr <= rangeSqr)
            {
                ChangeAIState(AIState.Chasing);
            }
            else if (m_aiState == AIState.Chasing && distSqr > rangeSqr)
            {
                ChangeAIState(AIState.Wandering);
            }
        }

        #endregion

        #region 이동 로직

        private async UniTask ReturnToMapAsync(System.Threading.CancellationToken token)
        {
            Vector3 targetPos = m_mapBounds.ClosestPoint(m_cachedTransform.position);
            targetPos.z = 0;
            float duration = Vector3.Distance(m_cachedTransform.position, targetPos) / (MoveSpeed * 2f);
            await m_cachedTransform.DOMove(targetPos, duration).SetEase(Ease.Linear).SetLink(gameObject).ToUniTask(cancellationToken: token);
        }

        private void HandleChasingState()
        {
            if (m_playerTransform == null || DOTween.IsTweening(m_cachedTransform)) return;

            Vector3 dir = (m_playerTransform.position - m_cachedTransform.position).normalized;
            Vector3 targetPos = m_cachedTransform.position + dir * 0.5f;
            
            FlipTowards(dir.x);
            
            m_moveTween = m_cachedTransform.DOMove(targetPos, 0.5f / MoveSpeed)
                .SetEase(Ease.Linear)
                .SetLink(gameObject);
        }

        private void StartWandering()
        {
            if (!IsMoveEnabled || m_aiState != AIState.Wandering || m_isAiPaused) return;

            Vector3 dest = GetRandomPositionInMap();
            Vector3 dir = (dest - m_cachedTransform.position).normalized;
            FlipTowards(dir.x);

            float duration = Vector3.Distance(m_cachedTransform.position, dest) / MoveSpeed;
            m_moveTween = m_cachedTransform.DOMove(dest, duration)
                .SetEase(Ease.Linear)
                .SetLink(gameObject)
                .OnComplete(() => WaitAndWanderNext().Forget());
        }

        private async UniTaskVoid WaitAndWanderNext()
        {
            float waitTime = UnityEngine.Random.Range(m_wanderWaitRange.x, m_wanderWaitRange.y);
            await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: this.GetCancellationTokenOnDestroy());
            if (m_aiState == AIState.Wandering && IsMoveEnabled && !m_isAiPaused)
            {
                StartWandering();
            }
        }

        private Vector3 GetRandomPositionInMap()
        {
            float x = UnityEngine.Random.Range(m_mapBounds.min.x, m_mapBounds.max.x);
            float y = UnityEngine.Random.Range(m_mapBounds.min.y, m_mapBounds.max.y);
            return new Vector3(x, y, 0);
        }

        private void FlipTowards(float dirX)
        {
            if (Mathf.Abs(dirX) > 0.01f)
            {
                float yRotation = dirX > 0 ? 180f : 0f;
                m_cachedTransform.rotation = Quaternion.Euler(0, yRotation, 0);
            }
        }

        #endregion

        #region 전투 및 피격 처리

        private void OnTriggerEnter2D(Collider2D other)
        {
            // [리팩토링] 신규 무기 시스템에서는 각 투사체/공격 이펙트가 직접 MobBase.TakeDamage를 호출합니다.
            // 따라서 몬스터 측에서의 별도 처리가 불필요합니다.
        }

        public override void PlayDamageEffect(Color? color = null)
        {
            EffectManager.Instance.PlayQueuedFlashEffect(m_spriteRenderer, color).Forget();
        }

        public override void TakeDamage(float damage, float stunTime = 0f)
        {
            if (IsDead) return;

            // 기본 피격 효과 (색상 지정 없음 -> Red)
            PlayDamageEffect();

            if (IsHit) return;

            IsHit = true;
            CurrentHp -= damage;

            if (CanPlayHitSound())
            {
                SoundManager.PlaySound(Sound.SFX, SoundKeys.Enemyhit);
            }

            if (CurrentHp <= 0)
            {
                OnDie();
            }
            else if (stunTime > 0)
            {
                ApplyStun(stunTime);
            }

            ResetHitFlagAsync().Forget();
        }

        private async UniTaskVoid ResetHitFlagAsync()
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            IsHit = false;
        }

        private void ApplyStun(float duration)
        {
            ChangeAIState(AIState.Stunned);
            DOVirtual.DelayedCall(duration, () =>
            {
                if (!IsDead)
                {
                    ChangeAIState(AIState.Wandering);
                }
            }).SetLink(gameObject);
        }

        public override void ApplySlow(float slowAmount, float duration)
        {
            m_slowTween?.Kill(true);
            float originalSpeed = m_initialSpeed;
            MoveSpeed = originalSpeed * (1f - slowAmount);
            m_slowTween = DOVirtual.DelayedCall(duration, () => { MoveSpeed = originalSpeed; }).SetLink(gameObject);
        }

        protected override void OnDie()
        {
            if (IsDead) return;
            ChangeAIState(AIState.Dead);
            KillAllTweens();
            SoundManager.PlaySound(Sound.SFX, SoundKeys.EnemyDeth);
            base.OnDie();
        }

        private void KillAllTweens()
        {
            m_moveTween?.Kill();
            m_slowTween?.Kill();
            if (m_spriteRenderer != null) m_spriteRenderer.DOKill();
        }

        #endregion

        #region 게임 상태 이벤트 핸들러

        protected override void OnPause()
        {
            base.OnPause();
            m_moveTween?.Pause();
            m_isAiPaused = true;
        }

        protected override void OnResume()
        {
            base.OnResume();
            if (!IsDead && m_aiState != AIState.Stunned)
            {
                m_moveTween?.Play();
                m_isAiPaused = false;
                if (m_aiState == AIState.Wandering && (m_moveTween == null || !m_moveTween.IsActive()))
                {
                    StartWandering();
                }
            }
        }

        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, m_searchRange);
        }
#endif
    }
}