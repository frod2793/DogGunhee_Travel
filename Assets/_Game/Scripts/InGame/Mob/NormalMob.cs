using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using InGame.Manager;
using InGame.Mob.BehaviorTree;
using InGame.Mob.MobBase;
using Random = UnityEngine.Random;

namespace InGame.Mob
{
    /// <summary>
    /// Behavior Tree 기반의 AI를 가진 일반 몬스터 클래스입니다.
    /// 배회(Wander)와 추적(Chase) 행동을 수행합니다.
    /// </summary>
    public class NormalMob : MobBase.MobBase
    {
        #region 인스펙터 필드

        [Header("몬스터 기본 스탯")]
        [Tooltip("몬스터의 초기 체력입니다.")]
        [SerializeField] private float m_initialHp = 100f;
        [Tooltip("몬스터의 이동 속도입니다.")]
        [SerializeField] private float m_initialSpeed = 1f;
        [Tooltip("몬스터의 초기 공격력입니다.")]
        [SerializeField] private float m_initialAttackDamage = 10f;
        [Tooltip("몬스터가 피격 시 경직되는 시간입니다.")]
        [SerializeField] private float m_initialStunTime = 0.1f;

        [Header("AI 설정")]
        [Tooltip("플레이어를 감지하는 범위입니다.")]
        [SerializeField] private float m_searchRange = 8f;
        [Tooltip("배회 시 대기 시간 범위입니다 (최소, 최대).")]
        [SerializeField] private Vector2 m_wanderWaitRange = new Vector2(1f, 3f);

        #endregion

        #region 내부 상태 및 컴포넌트

        private Bounds m_mapBounds;
        private Tween m_moveTween;
        private Tween m_slowTween;
        private SpriteRenderer m_spriteRenderer;
        private Transform m_cachedTransform;
        private Rigidbody2D m_rb; // 물리 이동 지원

        // AI 관련
        private INode m_btRoot;
        private bool m_isChasing; // 현재 추적 상태인지 여부 (애니메이션/로직용)
        private float m_lastActionTime;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_cachedTransform = transform;
            m_spriteRenderer = GetComponent<SpriteRenderer>();
            m_rb = GetComponent<Rigidbody2D>();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            InitializeMob();
            
            if (GameManager.Instance != null)
            {
                m_mapBounds = GameManager.Instance.MapBounds;
            }

            // BT 초기화 및 실행
            InitializeBehaviorTree();
            StartAILoopAsync().Forget();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            KillAllTweens();
        }
        
        #endregion

        #region 초기화

        /// <summary>
        /// 몬스터의 스탯 및 상태를 초기화합니다.
        /// </summary>
        private void InitializeMob()
        {
            CurrentHp = m_initialHp;
            MoveSpeed = m_initialSpeed;
            AttackDamage = m_initialAttackDamage;
            StunTime = m_initialStunTime;
            m_isChasing = false;
            
            if (m_spriteRenderer != null) 
                m_spriteRenderer.color = Color.white;
        }

        /// <summary>
        /// 몬스터의 행동 트리(Behavior Tree)를 구성합니다.
        /// </summary>
        private void InitializeBehaviorTree()
        {
            // BT 구조:
            // Selector (우선순위 선택)
            //  |-- Sequence (추적 및 공격 시도)
            //  |      |-- Condition: 플레이어 감지?
            //  |      |-- Action: 플레이어 추적
            //  |
            //  |-- Action: 배회 (기본 행동)

            m_btRoot = new Selector()
                .Add(new BehaviorTree.Sequence()
                    .Add(new ConditionNode(CheckPlayerDetection))
                    .Add(new ActionNode(ChasePlayerAsync)))
                .Add(new ActionNode(WanderAsync));
        }

        #endregion

        #region AI 로직 (Behavior Tree)

        /// <summary>
        /// 비동기 AI 루프를 시작합니다. 
        /// </summary>
        private async UniTaskVoid StartAILoopAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();

            // 플레이어 초기화 대기
            await UniTask.WaitUntil(() => m_player != null && m_player.transform.parent != null, cancellationToken: token);
            m_playerTransform = m_player.transform.parent;

            // 맵 이탈 시 복귀 처리
            if (!m_mapBounds.Contains(m_cachedTransform.position))
            {
                await ReturnToMapAsync(token);
            }

            // AI 루프 시작
            while (!IsDead && isActiveAndEnabled)
            {
                // 게임 일시정지, 스턴 등의 상태 확인
                if (!IsMoveEnabled)
                {
                    m_moveTween?.Pause();
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                    continue;
                }
                
                m_moveTween?.Play();

                // 트리 평가
                await m_btRoot.Evaluate();

                // 반응 속도 조절 (Polling Rate)
                await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: token);
            }
        }

        #endregion

        #region BT 액션 및 조건

        /// <summary>
        /// 플레이어가 감지 범위 내에 있는지 확인합니다.
        /// </summary>
        private bool CheckPlayerDetection()
        {
            if (m_playerTransform == null) return false;

            float distSqr = (m_cachedTransform.position - m_playerTransform.position).sqrMagnitude;
            return distSqr <= (m_searchRange * m_searchRange);
        }

        /// <summary>
        /// 플레이어를 추적합니다. (Behavior Tree Action)
        /// </summary>
        private UniTask<NodeStatus> ChasePlayerAsync()
        {
            // 1. 상태 전환 및 초기화
            if (!m_isChasing)
            {
                m_isChasing = true;
                m_moveTween?.Kill(); // 기존 이동(배회) 중단
            }
            
            SetState(MobState.Move);

            // 2. 이미 이동 중이라면 목적지 갱신 여부 판단
            if (m_moveTween != null && m_moveTween.IsActive() && m_moveTween.IsPlaying())
            {
                m_moveTween.Kill();
            }

            // 3. 추적 이동 시작
            Vector3 targetPos = m_playerTransform.position;
            Vector3 currentPos = m_cachedTransform.position;
            Vector3 dir = (targetPos - currentPos).normalized;
            
            FlipTowards(dir.x);

            float dist = Vector3.Distance(currentPos, targetPos);
            float duration = dist / MoveSpeed;
            
            if (m_rb != null)
            {
                m_moveTween = m_rb.DOMove(targetPos, duration)
                    .SetEase(Ease.Linear)
                    .SetLink(gameObject);
            }
            else
            {
                m_moveTween = m_cachedTransform.DOMove(targetPos, duration)
                    .SetEase(Ease.Linear)
                    .SetLink(gameObject);
            }

            return UniTask.FromResult(NodeStatus.Running);
        }

        /// <summary>
        /// 주변을 배회합니다. (Behavior Tree Action)
        /// </summary>
        private UniTask<NodeStatus> WanderAsync()
        {
            // 추적 상태였다면 배회로 전환
            if (m_isChasing)
            {
                m_isChasing = false;
                m_moveTween?.Kill();
            }

            // 이미 배회 중이거나 대기 중이라면 유지
            if ((m_moveTween != null && m_moveTween.IsActive()) || Time.time < m_lastActionTime)
            {
                if (Time.time < m_lastActionTime && m_currentState != MobState.Idle)
                {
                    SetState(MobState.Idle);
                }
                return UniTask.FromResult(NodeStatus.Running);
            }

            SetState(MobState.Move);

            // 새로운 배회 목표 설정
            Vector3 dest = GetRandomPositionInMap();
            Vector3 currentPos = m_cachedTransform.position;
            Vector3 dir = (dest - currentPos).normalized;
            
            FlipTowards(dir.x);

            float moveDuration = Vector3.Distance(currentPos, dest) / MoveSpeed;
            float waitDuration = Random.Range(m_wanderWaitRange.x, m_wanderWaitRange.y);

            // 이동 -> 대기 시퀀스 생성
            if (m_rb != null)
            {
                m_moveTween = DOTween.Sequence()
                    .Append(m_rb.DOMove(dest, moveDuration).SetEase(Ease.Linear))
                    .AppendCallback(() => 
                    { 
                        SetState(MobState.Idle);
                        m_lastActionTime = Time.time + waitDuration; 
                    })
                    .SetLink(gameObject);
            }
            else
            {
                m_moveTween = DOTween.Sequence()
                    .Append(m_cachedTransform.DOMove(dest, moveDuration).SetEase(Ease.Linear))
                    .AppendCallback(() => 
                    { 
                        SetState(MobState.Idle);
                        m_lastActionTime = Time.time + waitDuration; 
                    })
                    .SetLink(gameObject);
            }

            return UniTask.FromResult(NodeStatus.Running);
        }

        #endregion

        #region 이동 및 유틸리티

        /// <summary>
        /// 맵 밖으로 나갔을 때 가장 가까운 맵 내부 위치로 복귀합니다.
        /// </summary>
        private async UniTask ReturnToMapAsync(System.Threading.CancellationToken token)
        {
            Vector3 targetPos = m_mapBounds.ClosestPoint(m_cachedTransform.position);
            targetPos.z = 0;
            float duration = Vector3.Distance(m_cachedTransform.position, targetPos) / (MoveSpeed * 2f);
            
            m_moveTween = m_cachedTransform.DOMove(targetPos, duration)
                .SetEase(Ease.Linear)
                .SetLink(gameObject);
                
            await m_moveTween.ToUniTask(cancellationToken: token);
        }

        /// <summary>
        /// 맵 내부의 랜덤한 위치를 반환합니다.
        /// </summary>
        private Vector3 GetRandomPositionInMap()
        {
            float x = Random.Range(m_mapBounds.min.x, m_mapBounds.max.x);
            float y = Random.Range(m_mapBounds.min.y, m_mapBounds.max.y);
            return new Vector3(x, y, 0);
        }

        /// <summary>
        /// 이동 방향에 따라 스프라이트를 좌우 반전시킵니다.
        /// </summary>
        private void FlipTowards(float dirX)
        {
            if (Mathf.Abs(dirX) > 0.01f)
            {
                float yRotation = dirX > 0 ? 180f : 0f;
                m_cachedTransform.rotation = Quaternion.Euler(0, yRotation, 0);
            }
        }

        /// <summary>
        /// 실행 중인 모든 트윈을 중단합니다.
        /// </summary>
        private void KillAllTweens()
        {
            m_moveTween?.Kill();
            m_slowTween?.Kill();
            if (m_spriteRenderer != null) 
                m_spriteRenderer.DOKill();
        }

        #endregion

        #region 전투 및 피격 처리 (Override)

        public override void PlayDamageEffect(Color? color = null)
        {
            EffectManager.Instance.PlayQueuedFlashEffect(m_spriteRenderer, color).Forget();
        }

        public override void TakeDamage(float damage, float stunTime = 0f)
        {
            if (IsDead) return;

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
            
            KillAllTweens();
            SoundManager.PlaySound(Sound.SFX, SoundKeys.EnemyDeth);
            
            base.OnDie();
        }

        private async UniTaskVoid ResetHitFlagAsync()
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            IsHit = false;
        }

        private void ApplyStun(float duration)
        {
            SetState(MobState.Stun);
            m_moveTween?.Pause();
            
            DOVirtual.DelayedCall(duration, () =>
            {
                if (!IsDead)
                {
                    SetState(MobState.Idle);
                }
            }).SetLink(gameObject);
        }

        #endregion
        
        #region 디버그

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, m_searchRange);
        }
#endif

        #endregion
    }
}