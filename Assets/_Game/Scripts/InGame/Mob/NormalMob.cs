using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.Manager;
using InGame.Mob.BehaviorTree;
using Random = UnityEngine.Random;

namespace InGame.Mob
{
    /// <summary>
    /// Behavior Tree 기반의 AI를 가진 일반 몬스터 클래스입니다.
    /// <br/> 플레이어 감지 시 추적(Chase)하고, 그렇지 않으면 배회(Wander)합니다.
    /// </summary>
    public class NormalMob : MobBase.MobBase
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("1. 초기 스탯 설정")]
        [SerializeField, Tooltip("초기 체력")] 
        private float m_initialHp = 100f;

        [SerializeField, Tooltip("이동 속도")] 
        private float m_initialSpeed = 1f;

        [SerializeField, Tooltip("공격력")] 
        private float m_initialAttackDamage = 10f;

        [SerializeField, Tooltip("피격 경직 시간")] 
        private float m_initialStunTime = 0.1f;

        [Header("2. AI 행동 설정")]
        [SerializeField, Tooltip("플레이어 감지 반경")] 
        private float m_searchRange = 8f;

        [SerializeField, Tooltip("배회 시 대기 시간 범위 (최소, 최대)")] 
        private Vector2 m_wanderWaitRange = new Vector2(1f, 3f);

        [SerializeField, Tooltip("추적 시 위치 갱신 주기 (초)")]
        private float m_chaseUpdateInterval = 0.2f;

        #endregion

        #region 2. 내부 변수 및 컴포넌트

        // 컴포넌트 캐시
        private Transform m_cachedTransform;
        private SpriteRenderer m_spriteRenderer;
        private Rigidbody2D m_rb;

        // 이동 및 연출
        private Tween m_moveTween;
        private Tween m_slowTween;
        private Bounds m_mapBounds;

        // AI (Behavior Tree)
        private INode m_btRoot;
        private bool m_isChasing; 
        private float m_lastChaseUpdateTime; // 추적 최적화용 타이머

        #endregion

        #region 3. 유니티 생명주기

        private void Awake()
        {
            m_cachedTransform = transform;
            m_spriteRenderer = GetComponent<SpriteRenderer>();
            m_rb = GetComponent<Rigidbody2D>();
        }

        public override void OnEnable()
        {
            base.OnEnable(); // MobBase 초기화

            InitializeStats();
            InitializeMapBounds();
            InitializeBehaviorTree();

            // AI 루프 시작 (Fire-and-Forget)
            StartAILoopAsync().Forget();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            KillAllTweens();
        }

        #endregion

        #region 4. 초기화 로직

        /// <summary>
        /// Inspector 값을 기반으로 MobBase의 런타임 스탯을 초기화합니다.
        /// </summary>
        private void InitializeStats()
        {
            // MobBase 프로퍼티 설정을 통해 m_stats 갱신
            // (구조체 직접 수정보다는 프로퍼티나 SetAll 메서드 사용 권장)
            // 여기서는 MobBase의 프로퍼티를 활용
            CurrentHp = m_initialHp;
            MoveSpeed = m_initialSpeed;
            AttackDamage = m_initialAttackDamage;
            StunTime = m_initialStunTime;
            
            m_isChasing = false;
            m_lastChaseUpdateTime = 0f;

            if (m_spriteRenderer != null)
            {
                m_spriteRenderer.color = Color.white;
            }
        }

        private void InitializeMapBounds()
        {
            if (GameManager.Instance != null)
            {
                m_mapBounds = GameManager.Instance.MapBounds;
            }
            else
            {
                // Fallback: 맵 정보가 없으면 임의의 큰 범위 설정
                m_mapBounds = new Bounds(Vector3.zero, Vector3.one * 50f);
            }
        }

        /// <summary>
        /// 행동 트리를 구성합니다.
        /// 구조: [Selector] -> (1. 추적 시퀀스) OR (2. 배회 액션)
        /// </summary>
        private void InitializeBehaviorTree()
        {
            m_btRoot = new Selector()
                .Add(new BehaviorTree.Sequence()
                    .Add(new ConditionNode(CheckPlayerDetected)) // 조건: 플레이어 발견?
                    .Add(new ActionNode(ChasePlayerAsync)))      // 행동: 추적
                .Add(new ActionNode(WanderAsync));               // 행동: 배회 (기본)
        }

        #endregion

        #region 5. AI 루프 (Behavior Tree Execution)

        private async UniTaskVoid StartAILoopAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();

            // 1. 플레이어 생성 대기
            await UniTask.WaitUntil(() => m_player != null, cancellationToken: token);
            
            // 타겟 트랜스폼 캐싱 (MobBase에서 SetTarget으로 설정됨)
            if (m_player != null)
            {
                m_playerTransform = m_player.transform.parent != null ? m_player.transform.parent : m_player.transform;
            }

            // 2. 맵 이탈 복귀 (스폰 위치 보정)
            if (!m_mapBounds.Contains(m_cachedTransform.position))
            {
                await ReturnToMapAsync(token);
            }

            // 3. 메인 루프
            while (!IsDead && isActiveAndEnabled)
            {
                // 이동 불가능 상태(스턴, 게임 일시정지 등) 처리
                if (!IsMoveEnabled)
                {
                    if (m_moveTween != null && m_moveTween.IsActive() && m_moveTween.IsPlaying())
                    {
                        m_moveTween.Pause();
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                    continue;
                }

                // 일시정지 해제 시 트윈 재개
                if (m_moveTween != null && m_moveTween.IsActive() && !m_moveTween.IsPlaying())
                {
                    m_moveTween.Play();
                }

                // BT 평가
                await m_btRoot.Evaluate();

                // 틱(Tick) 간격 대기 (성능 최적화)
                await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: token);
            }
        }

        #endregion

        #region 6. BT 조건 및 액션 (Actions & Conditions)

        /// <summary>
        /// [Condition] 플레이어가 감지 범위 내에 있는지 확인합니다.
        /// </summary>
        private bool CheckPlayerDetected()
        {
            if (m_playerTransform == null) return false;

            // 거리 제곱 비교 (최적화)
            float distSqr = (m_cachedTransform.position - m_playerTransform.position).sqrMagnitude;
            return distSqr <= (m_searchRange * m_searchRange);
        }

        /// <summary>
        /// [Action] 플레이어를 향해 이동합니다.
        /// </summary>
        private UniTask<NodeStatus> ChasePlayerAsync()
        {
            // 1. 상태 전환
            if (!m_isChasing)
            {
                m_isChasing = true;
                SetState(MobState.Move);
                m_moveTween?.Kill(); // 배회 중이던 트윈 제거
            }

            // 2. 쿨타임 체크 (매 프레임 경로 갱신 방지)
            if (Time.time < m_lastChaseUpdateTime + m_chaseUpdateInterval)
            {
                return UniTask.FromResult(NodeStatus.Running);
            }
            m_lastChaseUpdateTime = Time.time;

            // 3. 이동 로직
            if (m_playerTransform != null)
            {
                Vector3 targetPos = m_playerTransform.position;
                MoveToTarget(targetPos);
            }

            return UniTask.FromResult(NodeStatus.Running);
        }

        /// <summary>
        /// [Action] 랜덤한 위치로 배회합니다.
        /// </summary>
        private UniTask<NodeStatus> WanderAsync()
        {
            // 1. 추적 -> 배회 전환 시 초기화
            if (m_isChasing)
            {
                m_isChasing = false;
                m_moveTween?.Kill();
                SetState(MobState.Idle);
            }

            // 2. 이미 이동 중이거나 대기 중인 경우 상태 유지
            if (m_moveTween != null && m_moveTween.IsActive())
            {
                return UniTask.FromResult(NodeStatus.Running);
            }

            // 3. 새로운 배회 시작
            SetState(MobState.Move);
            Vector3 dest = GetRandomPositionInMap();
            float distance = Vector3.Distance(m_cachedTransform.position, dest);
            float duration = distance / MoveSpeed;

            // 이동 -> 대기 시퀀스
            FlipTowards((dest - m_cachedTransform.position).x);

            float waitTime = Random.Range(m_wanderWaitRange.x, m_wanderWaitRange.y);

            // Rigidbody가 있으면 Rigidbody 이동, 없으면 Transform 이동
            if (m_rb != null)
            {
                m_moveTween = DOTween.Sequence()
                    .Append(m_rb.DOMove(dest, duration).SetEase(Ease.Linear))
                    .AppendCallback(() => SetState(MobState.Idle))
                    .AppendInterval(waitTime)
                    .SetLink(gameObject);
            }
            else
            {
                m_moveTween = DOTween.Sequence()
                    .Append(m_cachedTransform.DOMove(dest, duration).SetEase(Ease.Linear))
                    .AppendCallback(() => SetState(MobState.Idle))
                    .AppendInterval(waitTime)
                    .SetLink(gameObject);
            }

            return UniTask.FromResult(NodeStatus.Running);
        }

        #endregion

        #region 7. 이동 및 유틸리티

        private void MoveToTarget(Vector3 targetPos)
        {
            // 방향 전환
            Vector3 dir = (targetPos - m_cachedTransform.position).normalized;
            FlipTowards(dir.x);

            // 이동 거리 및 시간 계산
            float dist = Vector3.Distance(m_cachedTransform.position, targetPos);
            float duration = dist / MoveSpeed;

            // 기존 트윈이 있으면 제거하고 갱신 (추적의 경우 목표가 계속 바뀌므로)
            m_moveTween?.Kill();

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
        }

        private async UniTask ReturnToMapAsync(System.Threading.CancellationToken token)
        {
            Vector3 safePos = m_mapBounds.ClosestPoint(m_cachedTransform.position);
            safePos.z = 0; // 2D 게임 가정

            float duration = Vector3.Distance(m_cachedTransform.position, safePos) / (MoveSpeed * 2f); // 빠르게 복귀

            m_moveTween?.Kill();
            m_moveTween = m_cachedTransform.DOMove(safePos, duration)
                .SetEase(Ease.Linear)
                .SetLink(gameObject);

            await m_moveTween.ToUniTask(cancellationToken: token);
        }

        private Vector3 GetRandomPositionInMap()
        {
            float x = Random.Range(m_mapBounds.min.x, m_mapBounds.max.x);
            float y = Random.Range(m_mapBounds.min.y, m_mapBounds.max.y);
            return new Vector3(x, y, 0);
        }

        private void FlipTowards(float dirX)
        {
            // 미세한 떨림 방지를 위한 임계값
            if (Mathf.Abs(dirX) > 0.01f)
            {
                // Y축 회전을 이용한 좌우 반전 (Scale 방식보다 자식 객체 관리에 유리할 수 있음)
                float yRotation = dirX > 0 ? 180f : 0f;
                m_cachedTransform.rotation = Quaternion.Euler(0, yRotation, 0);
            }
        }

        private void KillAllTweens()
        {
            m_moveTween?.Kill();
            m_slowTween?.Kill();
            
            if (m_spriteRenderer != null)
            {
                m_spriteRenderer.DOKill();
            }
        }

        #endregion

        #region 8. 전투 처리 (Override)

        public override void PlayDamageEffect(Color? color = null)
        {
            if (EffectManager.Instance != null && m_spriteRenderer != null)
            {
                EffectManager.Instance.PlayQueuedFlashEffect(m_spriteRenderer, color).Forget();
            }
        }

        public override void TakeDamage(float damage, float stunTime = 0f)
        {
            if (IsDead) return;

            // 1. 피격 연출
            PlayDamageEffect();

            // 2. 중복 피격 방지 (짧은 시간) - MobBase의 IsHit 로직 활용 가능하면 좋으나 여기선 재구현
            if (IsHit) return;
            
            IsHit = true;
            CurrentHp -= damage;

            // 3. 사운드
            if (CanPlayHitSound())
            {
                SoundManager.PlaySound(Sound.SFX, SoundKeys.Enemyhit);
            }

            // 4. 사망 또는 경직 처리
            if (CurrentHp <= 0)
            {
                OnDie();
            }
            else if (stunTime > 0)
            {
                ApplyStun(stunTime);
            }

            // 피격 상태 해제
            ResetHitFlagAsync().Forget();
        }

        public override void ApplySlow(float slowAmount, float duration)
        {
            // 기존 슬로우 갱신
            m_slowTween?.Kill(true);

            float originalSpeed = m_initialSpeed; // 주의: 기본 속도가 변하는 게임이면 CurrentSpeed 기반이어야 함
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
            
            // 이동 중지
            if (m_moveTween != null && m_moveTween.IsActive())
            {
                m_moveTween.Pause();
            }

            // 경직 해제 타이머
            DOVirtual.DelayedCall(duration, () =>
            {
                if (!IsDead)
                {
                    SetState(MobState.Idle);
                    // 트윈 재개는 AI 루프에서 IsMoveEnabled 체크 후 수행됨
                }
            }).SetLink(gameObject);
        }

        #endregion

        #region 9. 디버그 (Debug)

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