using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using InGame.Mob.MobBase;
using InGame.Weapon.Base;
using InGame.Managers;
using InGame.Core.Interfaces;
using InGame.ObjectPool;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// [설명]: 근접 공격(Melee) 전략입니다.
    /// 애니메이션 재생, 콜라이더 판정 활성화, 조이스틱 방향 추적을 수행합니다.
    /// </summary>
    public class MeleeAttackStrategy : IWeaponStrategy
    {
        #region 상수 및 해시

        private static readonly int k_AnimTriggerStab = Animator.StringToHash("Stab");
        private static readonly int k_AnimTriggerSlash = Animator.StringToHash("Slash");

        #endregion

        #region 내부 변수

        private WeaponDataSO m_data;
        private WeaponPoolManager m_poolManager;
        
        // 인스턴스 컴포넌트
        private GameObject m_meleeInstance;
        private Animator m_animator;
        private PolygonCollider2D m_collider;
        private Transform m_ownerTransform;

        // 물리 판정용
        private ContactFilter2D m_contactFilter;
        private readonly List<Collider2D> m_hitResults = new List<Collider2D>(10);
        private readonly HashSet<int> m_hitMobInstanceIDs = new HashSet<int>();

        // 상태 제어
        private bool m_isAttacking;
        private CancellationTokenSource m_cts;

        // [추가]: 인터페이스 기반 의존성
        private IGameStateService m_gameState;
        private ICombatContext m_combatCtx;
        private IPlayerContext m_playerCtx;

        #endregion

        #region 인터페이스 구현

        public void Init(
            WeaponDataSO data, 
            WeaponPoolManager poolManager,
            IGameStateService gameState,
            ICombatContext combatContext,
            IPlayerContext playerContext)
        {
            m_data = data;
            m_poolManager = poolManager;
            
            m_gameState = gameState;
            m_combatCtx = combatContext;
            m_playerCtx = playerContext;

            // 충돌 필터 설정 (Mob 레이어만)
            m_contactFilter = ContactFilter2D.noFilter;
            m_contactFilter.useTriggers = true;
            m_contactFilter.SetLayerMask(LayerMask.GetMask("Mob"));
            m_contactFilter.useLayerMask = true;
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            if (m_isAttacking) return;

            // 인스턴스가 없으면 생성 (Lazy Init)
            if (m_meleeInstance == null)
            {
                SpawnMeleeInstance(owner);
            }

            m_ownerTransform = owner;
            PerformAttackAsync(stats, direction).Forget();
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 공격 중 플레이어 입력(조이스틱)에 따라 무기 방향 회전
            if (m_isAttacking && m_meleeInstance != null)
            {
                UpdateWeaponDirection();
            }
        }

        #endregion

        #region 상세 로직

        private void SpawnMeleeInstance(Transform owner)
        {
            if (m_data == null || m_data.ModelPrefab == null) return;

            m_meleeInstance = Object.Instantiate(m_data.ModelPrefab, owner);
            m_meleeInstance.transform.localPosition = Vector3.zero;

            m_animator = m_meleeInstance.GetComponentInChildren<Animator>();
            m_collider = m_meleeInstance.GetComponentInChildren<PolygonCollider2D>();

            if (m_collider != null)
            {
                m_collider.isTrigger = true;
                m_collider.enabled = false;
            }
        }

        private async UniTaskVoid PerformAttackAsync(WeaponRuntimeStats stats, Vector3 direction)
        {
            m_isAttacking = true;
            m_hitMobInstanceIDs.Clear();
            m_cts?.Cancel();
            m_cts = new CancellationTokenSource();

            var token = m_cts.Token;

            try
            {
                // 방향 설정 및 애니메이션 시작
                RotateToDirection(direction);

                if (m_animator != null)
                {
                    int trigger = stats.IsEvolved ? k_AnimTriggerSlash : k_AnimTriggerStab;
                    m_animator.speed = stats.CurrentAttackSpeed > 0 ? stats.CurrentAttackSpeed : 1f;
                    m_animator.SetTrigger(trigger);
                }

                if (m_collider != null) m_collider.enabled = true;

                // 애니메이션 길이 계산
                float duration = 0.4f; // 기본값
                if (m_animator != null)
                {
                    // 상태 정보 갱신 대기
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                    var stateInfo = m_animator.GetCurrentAnimatorStateInfo(0);
                    duration = stateInfo.length;
                    if (stateInfo.speed > 0) duration /= stateInfo.speed;
                }

                // 판정 루프
                float timer = 0f;
                while (timer < duration && !token.IsCancellationRequested)
                {
                    CheckCollision(stats);
                    timer += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                }
            }
            catch (System.OperationCanceledException)
            {
                // 공격 취소됨 - 정상 흐름
            }
            finally
            {
                if (m_collider != null) m_collider.enabled = false;
                m_isAttacking = false;
            }
        }

        private void RotateToDirection(Vector3 direction)
        {
            if (m_meleeInstance == null || direction == Vector3.zero) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            m_meleeInstance.transform.rotation = Quaternion.Euler(0, 0, angle);

            // 좌우 반전 처리
            if (Mathf.Abs(angle) > 90)
            {
                m_meleeInstance.transform.localScale = new Vector3(1, -1, 1);
            }
            else
            {
                m_meleeInstance.transform.localScale = new Vector3(1, 1, 1);
            }
        }

        private void UpdateWeaponDirection()
        {
            if (m_playerCtx != null && m_playerCtx.Joystick != null)
            {
                var joystick = m_playerCtx.Joystick;
                Vector3 dir = new Vector3(joystick.Horizontal, joystick.Vertical, 0);

                if (dir.sqrMagnitude > 0.01f)
                {
                    RotateToDirection(dir.normalized);
                }
            }
        }

        private void CheckCollision(WeaponRuntimeStats stats)
        {
            if (m_collider == null) return;

            int hitCount = m_collider.Overlap(m_contactFilter, m_hitResults);
            for (int i = 0; i < hitCount; i++)
            {
                var target = m_hitResults[i];
                if (target == null) continue;

                int id = target.gameObject.GetInstanceID();

                // 중복 타격 방지
                if (m_hitMobInstanceIDs.Contains(id)) continue;

                if (target.TryGetComponent(out MobBase mob))
                {
                    m_hitMobInstanceIDs.Add(id);
                    mob.TakeDamage(stats.CurrentAttackPower, stats.MobStunTime);
                }
            }
        }

        #endregion
    }
}