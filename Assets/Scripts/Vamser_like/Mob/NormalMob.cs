using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 일반 몬스터의 AI 및 상태를 관리하는 클래스입니다.
    /// VamserMobBase(부모)의 최적화된 구조를 상속받습니다.
    /// </summary>
    public class NormalMob : VamserMobBase
    {
        #region 인스펙터 필드 (스탯 및 AI 설정)

        [Header("몬스터 기본 스탯")]
        [SerializeField] private float m_initialHp = 100f;
        [SerializeField] private float m_initialSpeed = 1f;
        [SerializeField] private float m_initialAttackDamage = 10f;
        [SerializeField] private float m_initialStunTime = 0.1f;

        [Header("AI 설정")]
        [SerializeField] private float m_searchRange = 8f;
        
        [Tooltip("배회 시 도착 후 대기 시간 범위 (최소, 최대)")]
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
        
        private Weaphon_base m_cachedPlayerWeapon;
        private bool m_isHitByProjectile; 

        private bool m_isAiPaused;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_cachedTransform = transform;
            m_spriteRenderer = GetComponent<SpriteRenderer>();

            // [삭제됨] 몬스터 콜라이더를 강제로 트리거로 만드는 코드를 제거했습니다.
            // 무기 콜라이더가 트리거라면, 몬스터가 일반 콜라이더여도 OnTriggerEnter2D가 정상 호출됩니다.
        }

        private void Start()
        {
            var mapObj = GameObject.FindGameObjectWithTag("Map");
            if (mapObj != null && mapObj.TryGetComponent(out SpriteRenderer mapRenderer))
            {
                m_mapBounds = mapRenderer.bounds;
            }
            else
            {
                m_mapBounds = new Bounds(Vector3.zero, Vector3.one * 100f);
            }
        }

        public override void OnEnable()
        {
            base.OnEnable();
            InitializeMob();
            StartAILoopAsync().Forget();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            KillAllTweens();
        }

        private void FixedUpdate()
        {
            if (!IsMoveEnabled || IsDead || m_aiState == AIState.Stunned || m_aiState == AIState.Dead || m_isAiPaused) 
                return;

            switch (m_aiState)
            {
                case AIState.Chasing:
                    HandleChasingState();
                    break;
                
                case AIState.Wandering:
                    CheckPlayerDetection();
                    break;
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
            m_cachedPlayerWeapon = null;
            m_isAiPaused = false;
            
            if (m_spriteRenderer != null) m_spriteRenderer.color = Color.white;
        }

        public override void SetTarget(PlayerBase target)
        {
            base.SetTarget(target);

            if (m_player != null)
            {
                m_cachedPlayerWeapon = m_player.WeaphonBase;
                m_isHitByProjectile = m_cachedPlayerWeapon != null && m_cachedPlayerWeapon.isShooting;
            }
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

            SetState(MobState.Move); 
            ChangeAIState(AIState.Wandering);
        }

        private void ChangeAIState(AIState newState)
        {
            if (m_aiState == newState) return;
            
            if (m_aiState == AIState.Wandering)
            {
                m_moveTween?.Kill();
            }

            m_aiState = newState;
            
            if (newState == AIState.Wandering)
            {
                StartWandering();
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
            
            await m_cachedTransform.DOMove(targetPos, duration)
                .SetEase(Ease.Linear)
                .SetLink(gameObject)
                .ToUniTask(cancellationToken: token);
        }

        private void HandleChasingState()
        {
            if (m_playerTransform == null) return;

            Vector3 dir = (m_playerTransform.position - m_cachedTransform.position).normalized;
            Vector3 newPos = m_cachedTransform.position + dir * (MoveSpeed * Time.fixedDeltaTime);
            newPos.z = 0;
            
            m_cachedTransform.position = newPos;
            FlipTowards(dir.x);
            
            CheckPlayerDetection();
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

        // 무기의 Collider가 Trigger라면, 몬스터가 일반 Collider여도 이 함수가 호출됩니다.
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (m_isHitByProjectile && !IsHit && other.CompareTag("Player_Attack"))
            {
                ProcessHit(other);
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!m_isHitByProjectile && !IsHit && other.CompareTag("Player_Attack"))
            {
                ProcessHit(other);
            }
        }

        private void ProcessHit(Collider2D other)
        {
            if (other.TryGetComponent(out Weaphon_base weapon))
            {
                TakeDamage(weapon.attackPower, weapon.mobStunTime);
            }
            else if (m_cachedPlayerWeapon != null)
            {
                TakeDamage(m_cachedPlayerWeapon.attackPower, m_cachedPlayerWeapon.mobStunTime);
            }
        }

        public override void TakeDamage(float damage, float stunTime = 0f)
        {
            if (IsDead || IsHit) return;

            IsHit = true;
            CurrentHp -= damage;

            EffectManager.Instance.PlayQueuedFlashEffect(m_spriteRenderer).Forget();
            SoundManager.PlaySound(Sound.SFX, SoundKeys.Enemyhit);

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
            await UniTask.Yield(PlayerLoopTiming.FixedUpdate);
            IsHit = false;
        }

        private void ApplyStun(float duration)
        {
            ChangeAIState(AIState.Stunned);
            SetState(MobState.Stun);
            StunTime = duration;

            DOVirtual.DelayedCall(duration, () =>
            {
                if (!IsDead)
                {
                    SetState(MobState.Move);
                    ChangeAIState(AIState.Wandering);
                }
            }).SetLink(gameObject);
        }

        public override void ApplySlow(float slowAmount, float duration)
        {
            m_slowTween?.Kill(true);

            float originalSpeed = m_initialSpeed;
            MoveSpeed = originalSpeed * (1f - slowAmount);

            m_slowTween = DOVirtual.DelayedCall(duration, () => 
            { 
                MoveSpeed = originalSpeed; 
            }).SetLink(gameObject);
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