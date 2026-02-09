using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Mob.MobBase;
using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 자동 공격 로직과 타겟 탐색을 전담하는 컴포넌트입니다.
    /// 토글 상태에 따라 자동으로 가장 가까운 적을 추적하고 공격 신호를 보냅니다.
    /// </summary>
    public class PlayerAutoAttackSystem : MonoBehaviour
    {
        #region 설정 데이터

        [Header("시스템 설정")]
        [SerializeField] private LayerMask m_enemyLayer;
        [SerializeField] private float m_detectionRadius = 10f;
        [SerializeField] private float m_attackRadius = 1.5f;

        #endregion

        #region 내부 상태 및 캐시

        private const int k_MaxEnemyColliders = 20;
        
        private Transform m_playerTransform;
        private PlayerBase m_playerBase;
        private bool m_isActive = false;
        private bool m_isEnabledByToggle = false;
        
        private CancellationTokenSource m_autoAttackCts;
        private ContactFilter2D m_contactFilter;
        private readonly Collider2D[] m_enemyColliders = new Collider2D[k_MaxEnemyColliders];

        #endregion

        #region 프로퍼티

        public Vector3 AutoMoveDirection { get; private set; }
        public bool IsActive => m_isActive;
        
        /// <summary>
        /// 현재 시스템이 타겟팅하고 있는 가장 가까운 적입니다.
        /// </summary>
        public MobBase CurrentTarget { get; private set; }
        
        public bool EnabledByToggle
        {
            get => m_isEnabledByToggle;
            set
            {
                if (m_isEnabledByToggle == value) return;
                m_isEnabledByToggle = value;
                // 토글이 꺼지면 자동 공격 로직도 즉시 중단
                if (!m_isEnabledByToggle && m_isActive) Disable();
            }
        }

        #endregion

        #region 이벤트

        public event System.Action<Vector3> OnAttackRequested;

        #endregion

        #region 초기화 및 제어

        public void Init(Transform playerTransform, PlayerBase playerBase, LayerMask enemyLayer, float detectionRadius, float attackRadius)
        {
            m_playerTransform = playerTransform;
            m_playerBase = playerBase;
            m_enemyLayer = enemyLayer;
            m_detectionRadius = detectionRadius;
            m_attackRadius = attackRadius;
            
            m_contactFilter.useTriggers = true;
            m_contactFilter.SetLayerMask(m_enemyLayer);
            m_contactFilter.useLayerMask = true;
        }

        public void Enable()
        {
            if (m_isActive) return;
            m_isActive = true;
            m_autoAttackCts?.Cancel();
            m_autoAttackCts = new CancellationTokenSource();
            AutoAttackLoopAsync(m_autoAttackCts.Token).Forget();
        }

        public void Disable()
        {
            if (!m_isActive) return;
            m_isActive = false;
            AutoMoveDirection = Vector3.zero;
            m_autoAttackCts?.Cancel();
            m_autoAttackCts?.Dispose();
            m_autoAttackCts = null;
        }

        #endregion

        #region 자동 공격 메인 루프 (비동기)

        private async UniTaskVoid AutoAttackLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (m_playerTransform == null || m_playerBase == null)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    continue;
                }

                // 가장 가까운 적을 찾아 이동 및 공격 판단
                MobBase target = FindClosestEnemy();
                CurrentTarget = target; // [추가] 외부 참조용 타겟 캐싱
                
                if (target != null)
                {
                    Vector3 targetPos = target.transform.position;
                    Vector3 myPos = m_playerTransform.position;
                    Vector3 dirToTarget = (targetPos - myPos).normalized;
                    float dist = Vector3.Distance(myPos, targetPos);

                    // 공격 범위보다 멀면 해당 방향으로 이동, 가까우면 정지
                    AutoMoveDirection = dist > m_attackRadius * 0.9f ? dirToTarget : Vector3.zero;

                    // 공격 유효 범위 내에 있으면 공격 요청 이벤트 발생
                    if (dist <= m_attackRadius * 1.2f)
                    {
                        OnAttackRequested?.Invoke(dirToTarget);
                    }
                }
                else
                {
                    AutoMoveDirection = Vector3.zero;
                }
                
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        #endregion

        #region 서브 루틴 및 유틸리티

        /// <summary>
        /// 감지 반경 내에서 가장 가까운 살아있는 적을 찾아 반환합니다.
        /// </summary>
        public MobBase FindClosestEnemy()
        {
            if (m_playerTransform == null) return null;
            
            int count = Physics2D.OverlapCircle(m_playerTransform.position, m_detectionRadius, m_contactFilter, m_enemyColliders);
            MobBase closest = null;
            float minDstSqr = float.MaxValue;
            Vector3 myPos = m_playerTransform.position;

            for (int i = 0; i < count; i++)
            {
                var col = m_enemyColliders[i];
                if (col.TryGetComponent(out MobBase mob) && !mob.IsDead)
                {
                    float dstSqr = (mob.transform.position - myPos).sqrMagnitude;
                    if (dstSqr < minDstSqr)
                    {
                        minDstSqr = dstSqr;
                        closest = mob;
                    }
                }
            }
            return closest;
        }

        #endregion

        #region 기즈모 시각화

        private void OnDrawGizmos()
        {
            if (m_playerTransform == null)
            {
                // 에디터 모드 대응
                m_playerTransform = transform;
            }

            // 1. 감지 범위 (노란색)
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(m_playerTransform.position, m_detectionRadius);

            // 2. 이동/공격 범위 (빨간색)
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(m_playerTransform.position, m_attackRadius);

            // 3. 현재 타겟 연결선 (하늘색)
            if (CurrentTarget != null && !CurrentTarget.IsDead)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(m_playerTransform.position, CurrentTarget.transform.position);
                Gizmos.DrawWireSphere(CurrentTarget.transform.position, 0.5f);
            }
        }

        #endregion
    }
}
