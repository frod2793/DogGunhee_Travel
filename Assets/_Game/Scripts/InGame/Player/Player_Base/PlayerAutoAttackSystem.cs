using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Mob.MobBase;
using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 자동 공격 시스템을 전담하는 컴포넌트입니다.
    /// </summary>
    public class PlayerAutoAttackSystem : MonoBehaviour
    {
        #region 설정 변수
        [SerializeField] private LayerMask m_enemyLayer;
        [SerializeField] private float m_detectionRadius = 10f;
        [SerializeField] private float m_attackRadius = 1.5f;
        #endregion

        #region 내부 변수
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
        
        public bool EnabledByToggle
        {
            get => m_isEnabledByToggle;
            set
            {
                if (m_isEnabledByToggle == value) return;
                m_isEnabledByToggle = value;
                if (!m_isEnabledByToggle && m_isActive) Disable();
            }
        }
        #endregion

        #region 이벤트
        public event System.Action<Vector3> OnAttackRequested;
        #endregion

        #region 초기화
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
        #endregion

        #region 활성화/비활성화
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

        #region 자동 공격 루프
        private async UniTaskVoid AutoAttackLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (m_playerTransform == null || m_playerBase == null)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    continue;
                }

                MobBase target = FindClosestEnemy();
                
                if (target != null)
                {
                    Vector3 targetPos = target.transform.position;
                    Vector3 myPos = m_playerTransform.position;
                    Vector3 dirToTarget = (targetPos - myPos).normalized;
                    float dist = Vector3.Distance(myPos, targetPos);

                    AutoMoveDirection = dist > m_attackRadius * 0.9f ? dirToTarget : Vector3.zero;

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

        #region 적 탐지
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
    }
}
