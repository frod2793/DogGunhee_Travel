using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Mob.MobBase;
using InGame.Mob.Systems;
using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// [설명]: 플레이어의 자동 공격 및 타겟 탐색을 담당하는 시스템 클래스입니다.
    /// 지정된 반경 내의 가장 가까운 적을 감지하고 추적 또는 공격 신호를 발생시킵니다.
    /// </summary>
    public class PlayerAutoAttackSystem : MonoBehaviour
    {
        #region 에디터 설정

        [Header("감지 및 공격 설정")]
        [SerializeField, Tooltip("적을 감지할 레이어 마스크")]
        private LayerMask m_enemyLayer;

        [SerializeField, Tooltip("적 감지 반경")]
        private float m_detectionRadius = 10f;

        [SerializeField, Tooltip("공격 가능 사거리")]
        private float m_attackRadius = 1.5f;

        [SerializeField, Tooltip("적과 유지할 최소 안전 거리 (이보다 가까우면 후퇴)")]
        private float m_safeDistance = 0.8f;

        #endregion

        #region 내부 필드 및 캐시

        /// <summary> 최대 동시 감지 가능한 적 콜라이더 수 </summary>
        private const int k_MaxEnemyColliders = 20;

        /// <summary> 플레이어의 트랜스폼 캐시 </summary>
        private Transform m_playerTransform;

        /// <summary> 플레이어 기본 시스템 참조 </summary>
        private PlayerBase m_playerBase;

        /// <summary> 모든 몹을 관리하는 중앙 매니저 참조 </summary>
        private MobManager m_mobManager;

        /// <summary> 현재 자동 공격 시스템 활성화 상태 </summary>
        private bool m_isActive = false;

        /// <summary> 설정 토글에 의한 사용 가능 여부 </summary>
        private bool m_isEnabledByToggle = false;

        /// <summary> 2D 물리 쿼리용 필터 </summary>
        private ContactFilter2D m_contactFilter;

        /// <summary> 적 스캔 시 사용될 콜라이더 배열 </summary>
        private readonly Collider2D[] m_enemyColliders = new Collider2D[k_MaxEnemyColliders];

        /// <summary> 비동기 공격 루프 제어 토큰 </summary>
        private CancellationTokenSource m_autoAttackCts;

        #endregion

        #region 공개 프로퍼티

        /// <summary> [설명]: 자동 추적 시 결정된 이동 방향 벡터입니다. </summary>
        public Vector3 AutoMoveDirection { get; private set; }

        /// <summary> [설명]: 현재 시스템이 동작 중인지 여부입니다. </summary>
        public bool IsActive => m_isActive;

        /// <summary> [설명]: 현재 추적 중인 대상(타겟) 인터페이스입니다. </summary>
        public ITargetable CurrentTarget { get; private set; }

        /// <summary> [설명]: 설정된 공격 사거리 값입니다. </summary>
        public float AttackRadius => m_attackRadius;

        /// <summary> [설명]: 설정된 적 감지 반경 값입니다. </summary>
        public float DetectionRadius => m_detectionRadius;

        /// <summary> [설명]: UI나 설정을 통해 자동 공격 사용 여부를 제어하는 토글 프로퍼티입니다. </summary>
        public bool EnabledByToggle
        {
            get => m_isEnabledByToggle;
            set
            {
                if (m_isEnabledByToggle == value)
                {
                    return;
                }
                m_isEnabledByToggle = value;

                if (!m_isEnabledByToggle)
                {
                    Disable();
                }
            }
        }

        #endregion

        #region 이벤트

        /// <summary> [설명]: 공격 조건 충족 시 외부(무기 시스템 등)에 공격 실행을 요청하는 이벤트입니다. </summary>
        public event Action<Vector3> OnAttackRequested;

        #endregion

        #region 유니티 생명주기

        /// <summary> [설명]: 비활성화 시 시스템을 중지합니다. </summary>
        private void OnDisable()
        {
            Disable();
        }

        /// <summary> [설명]: 파괴 시 진행 중인 비동기 작업을 안전하게 종료합니다. </summary>
        private void OnDestroy()
        {
            StopAutoAttackTask();
        }

        #endregion

        #region 초기화 및 제어

        /// <summary>
        /// [설명]: 자동 공격 시스템에 필요한 필수 참조들을 주입하고 초기화합니다.
        /// </summary>
        public void Init(Transform playerTransform, PlayerBase playerBase, MobManager mobManager, LayerMask enemyLayer, float detectionRadius, float attackRadius)
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

        /// <summary>
        /// [설명]: 자동 공격 시스템을 가동하고 비동기 루프를 시작합니다.
        /// </summary>
        public void Enable()
        {
            if (m_isActive)
            {
                return;
            }
            if (!m_isEnabledByToggle)
            {
                return;
            }

            m_isActive = true;

            StopAutoAttackTask();
            m_autoAttackCts = new CancellationTokenSource();

            var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                m_autoAttackCts.Token,
                this.GetCancellationTokenOnDestroy()
            ).Token;

            AutoAttackLoopAsync(combinedToken).Forget();
        }

        /// <summary>
        /// [설명]: 자동 공격 시스템 가동을 중단하고 상태를 초기화합니다.
        /// </summary>
        public void Disable()
        {
            if (!m_isActive)
            {
                return;
            }

            m_isActive = false;
            AutoMoveDirection = Vector3.zero;
            CurrentTarget = null;

            StopAutoAttackTask();
        }

        /// <summary>
        /// [설명]: 진행 중인 비동기 공격 루틴을 중단하고 토큰 소스를 정리합니다.
        /// </summary>
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

        #region 내부 비즈니스 로직

        /// <summary>
        /// [설명]: 활성화 기간 동안 비동기로 프레임마다 타겟을 탐색하고 이동/공격 로직을 판단하는 메인 루프입니다.
        /// </summary>
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

                    // 이동 로직: 너무 가까우면 후퇴(카이팅), 멀면 접근
                    if (dist < m_safeDistance)
                    {
                        // 적과 너무 가까움 -> 반대 방향으로 이동하여 거리를 벌림
                        AutoMoveDirection = -dirToTarget;
                    }
                    else if (dist > m_attackRadius * 0.9f)
                    {
                        // 적이 사거리 밖에 있음 -> 타겟 방향으로 다가감
                        AutoMoveDirection = dirToTarget;
                    }
                    else
                    {
                        // 공격 적정 사거리 -> 제자리에 멈춰서 공격에 집중
                        AutoMoveDirection = Vector3.zero;
                    }

                    // 공격 로직: 사거리 여유분(120%) 이내 진입 시 공격 요청
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

        /// <summary>
        /// [설명]: MobManager를 통해 플레이어 위치에서 가장 가까운 유효 타겟을 검색합니다.
        /// </summary>
        /// <param name="customRange">선택적 커스텀 탐색 범위</param>
        /// <returns>탐색된 최적 타겟 인터페이스</returns>
        public ITargetable FindClosestEnemy(float? customRange = null)
        {
            if (m_playerTransform == null || m_mobManager == null)
            {
                return null;
            }

            float range = customRange ?? m_detectionRadius;
            return m_mobManager.GetClosestTarget(m_playerTransform.position, range);
        }

        #endregion

        #region 디버깅 도구

        /// <summary> [설명]: 에디터 기즈모를 통해 감지 및 공격 가시 범위를 시각화합니다. </summary>
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