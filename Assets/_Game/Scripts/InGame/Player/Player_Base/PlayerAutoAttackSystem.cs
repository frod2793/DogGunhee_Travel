using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Mob.MobBase;
using InGame.Mob.Systems;
using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 자동 공격 및 타겟 탐색을 담당하는 시스템 클래스입니다.
    /// <br/> 지정된 반경 내의 가장 가까운 적을 감지하고 추적/공격 신호를 발생시킵니다.
    /// </summary>
    public class PlayerAutoAttackSystem : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("감지 및 공격 설정")] [SerializeField, Tooltip("적을 감지할 레이어 마스크")]
        private LayerMask m_enemyLayer;

        [SerializeField, Tooltip("적 감지 반경")] private float m_detectionRadius = 10f;

        [SerializeField, Tooltip("공격 가능 사거리")] private float m_attackRadius = 1.5f;

        #endregion

        #region 2. 내부 변수 및 캐시

        private const int k_MaxEnemyColliders = 20;

        private Transform m_playerTransform;
        private PlayerBase m_playerBase;
        private MobManager m_mobManager;

        private bool m_isActive = false;
        private bool m_isEnabledByToggle = false;

        private ContactFilter2D m_contactFilter;
        private readonly Collider2D[] m_enemyColliders = new Collider2D[k_MaxEnemyColliders];
        private CancellationTokenSource m_autoAttackCts;

        #endregion

        #region 3. 공개 프로퍼티 (Properties)

        public Vector3 AutoMoveDirection { get; private set; }
        public bool IsActive => m_isActive;
        public ITargetable CurrentTarget { get; private set; }
        public float AttackRadius => m_attackRadius;
        public float DetectionRadius => m_detectionRadius;

        public bool EnabledByToggle
        {
            get => m_isEnabledByToggle;
            set
            {
                if (m_isEnabledByToggle == value) return;
                m_isEnabledByToggle = value;

                if (!m_isEnabledByToggle)
                {
                    Disable(); // 토글 꺼짐 -> 시스템 중단
                }
            }
        }

        #endregion

        #region 4. 이벤트 (Events)

        public event Action<Vector3> OnAttackRequested;

        #endregion

        #region 5. 유니티 생명주기

        private void OnDisable()
        {
            Disable();
        }

        private void OnDestroy()
        {
            StopAutoAttackTask();
        }

        #endregion

        #region 6. 초기화 및 제어 (Public Methods)

        // PlayerController에서 호출: Init
        public void Init(Transform playerTransform, PlayerBase playerBase, MobManager mobManager, LayerMask enemyLayer, float detectionRadius,
            float attackRadius)
        {
            m_playerTransform = playerTransform;
            m_playerBase = playerBase;
            m_mobManager = mobManager;
            m_enemyLayer = enemyLayer;
            m_detectionRadius = detectionRadius;
            m_attackRadius = attackRadius;

            m_contactFilter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = true,
                layerMask = m_enemyLayer
            };
        }

        public void Enable()
        {
            if (m_isActive) return;
            if (!m_isEnabledByToggle) return;

            m_isActive = true;

            StopAutoAttackTask();
            m_autoAttackCts = new CancellationTokenSource();

            var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                m_autoAttackCts.Token,
                this.GetCancellationTokenOnDestroy()
            ).Token;

            AutoAttackLoopAsync(combinedToken).Forget();
        }

        public void Disable()
        {
            if (!m_isActive) return;

            m_isActive = false;
            AutoMoveDirection = Vector3.zero;
            CurrentTarget = null;

            StopAutoAttackTask();
        }

        private void StopAutoAttackTask()
        {
            if (m_autoAttackCts != null)
            {
                m_autoAttackCts.Cancel();
                m_autoAttackCts.Dispose();
                m_autoAttackCts = null;
            }
        }

        #endregion

        #region 7. 내부 로직

        private async UniTaskVoid AutoAttackLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (m_playerTransform == null || m_playerBase == null)
                {
                    continue;
                }
 
                ITargetable target = FindClosestEnemy();
                CurrentTarget = target;
 
                if (target != null)
                {
                    Vector3 targetPos = target.Position;
                    Vector3 myPos = m_playerTransform.position;

                    float dist = Vector3.Distance(myPos, targetPos);
                    Vector3 dirToTarget = (targetPos - myPos).normalized;

                    // 이동: 사거리의 90% 지점까지 접근
                    if (dist > m_attackRadius * 0.9f)
                    {
                        AutoMoveDirection = dirToTarget;
                    }
                    else
                    {
                        AutoMoveDirection = Vector3.zero;
                    }

                    // 공격: 사거리의 120% 이내면 시도
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

        public ITargetable FindClosestEnemy(float? customRange = null)
        {
            if (m_playerTransform == null || m_mobManager == null) return null;

            float range = customRange ?? m_detectionRadius;
            return m_mobManager.GetClosestTarget(m_playerTransform.position, range);
        }

        #endregion

        #region 8. 디버그

        private void OnDrawGizmos()
        {
            Vector3 center = m_playerTransform != null ? m_playerTransform.position : transform.position;

            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(center, m_detectionRadius);

            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(center, m_attackRadius);

            if (CurrentTarget != null && !CurrentTarget.IsDead)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(center, CurrentTarget.Position);
                Gizmos.DrawWireSphere(CurrentTarget.Position, 0.5f);
            }
        }

        #endregion
    }
}