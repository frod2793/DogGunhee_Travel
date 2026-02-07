using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using InGame.Mob.MobBase;
using InGame.Weapon.Base;
using InGame.Manager;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 근접 공격(펀치/슬래시) 전략입니다.
    /// 애니메이터 트리거, 콜라이더 판정, 방향 추적을 처리합니다.
    /// </summary>
    public class MeleeAttackStrategy : IWeaponStrategy
    {
        #region 상수

        private static readonly int k_AnimTriggerStab = Animator.StringToHash("Stab");
        private static readonly int k_AnimTriggerSlash = Animator.StringToHash("Slash");

        #endregion

        #region 내부 변수

        private WeaponDataSO m_data;
        private GameObject m_meleeInstance;
        private Animator m_animator;
        private PolygonCollider2D m_collider;
        private SpriteRenderer m_spriteRenderer;
        private Transform m_ownerTransform;
        private ContactFilter2D m_contactFilter;

        private readonly List<Collider2D> m_hitResults = new List<Collider2D>(10);
        private readonly HashSet<int> m_hitMobInstanceIDs = new HashSet<int>();

        private bool m_isAttacking;
        private CancellationTokenSource m_cts;

        #endregion

        #region IWeaponStrategy 구현

        public void Initialize(WeaponDataSO data)
        {
            m_data = data;

            // 콜라이더 필터 설정
            m_contactFilter = ContactFilter2D.noFilter;
            m_contactFilter.useTriggers = true;
            m_contactFilter.SetLayerMask(LayerMask.GetMask("Mob"));
            m_contactFilter.useLayerMask = true;
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            if (m_isAttacking) return;
            if (m_meleeInstance == null)
            {
                SpawnMeleeInstance(owner);
            }

            m_ownerTransform = owner;
            PerformAttackAsync(stats, direction).Forget();
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 공격 중이면 방향 업데이트
            if (m_isAttacking && m_meleeInstance != null)
            {
                UpdateWeaponDirection();
            }
        }

        #endregion

        #region 내부 메서드

        private void SpawnMeleeInstance(Transform owner)
        {
            if (m_data?.ModelPrefab == null)
            {
                Debug.LogWarning("[MeleeAttackStrategy] ModelPrefab이 없습니다.");
                return;
            }

            m_meleeInstance = Object.Instantiate(m_data.ModelPrefab, owner);
            m_meleeInstance.transform.localPosition = Vector3.zero;

            m_animator = m_meleeInstance.GetComponentInChildren<Animator>();
            m_collider = m_meleeInstance.GetComponentInChildren<PolygonCollider2D>();
            m_spriteRenderer = m_meleeInstance.GetComponentInChildren<SpriteRenderer>();

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
                RotateToDirection(direction);

                if (m_animator != null)
                {
                    int trigger = stats.IsEvolved ? k_AnimTriggerSlash : k_AnimTriggerStab;
                    m_animator.speed = stats.CurrentAttackSpeed > 0 ? stats.CurrentAttackSpeed : 1f;
                    m_animator.SetTrigger(trigger);
                }

                if (m_collider != null) m_collider.enabled = true;

                // 애니메이션 길이 계산
                float duration = 0.4f;
                if (m_animator != null)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                    var stateInfo = m_animator.GetCurrentAnimatorStateInfo(0);
                    duration = stateInfo.length;
                    if (stateInfo.speed > 0) duration /= stateInfo.speed;
                }

                // 공격 지속 시간 동안 판정
                float timer = 0f;
                while (timer < duration && !token.IsCancellationRequested)
                {
                    CheckCollision(stats);
                    timer += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                }

                if (m_collider != null) m_collider.enabled = false;
            }
            catch (System.OperationCanceledException)
            {
                // 정상적인 취소
            }
            finally
            {
                m_isAttacking = false;
            }
        }

        private void RotateToDirection(Vector3 direction)
        {
            if (m_meleeInstance == null || direction == Vector3.zero) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            m_meleeInstance.transform.rotation = Quaternion.Euler(0, 0, angle);

            // 좌우 반전
            if (Mathf.Abs(angle) > 90)
                m_meleeInstance.transform.localScale = new Vector3(1, -1, 1);
            else
                m_meleeInstance.transform.localScale = new Vector3(1, 1, 1);
        }

        private void UpdateWeaponDirection()
        {
            if (GameManager.Instance?.Joystick != null)
            {
                var joystick = GameManager.Instance.Joystick;
                Vector3 dir = new Vector3(joystick.Horizontal, joystick.Vertical, 0);
                if (dir.sqrMagnitude > 0.01f)
                {
                    RotateToDirection(dir.normalized);
                }
            }
        }

        private void CheckCollision(WeaponRuntimeStats stats)
        {
            if (m_collider == null || m_collider.pathCount == 0) return;

            int hitCount = m_collider.Overlap(m_contactFilter, m_hitResults);

            for (int i = 0; i < hitCount; i++)
            {
                var target = m_hitResults[i];
                int id = target.gameObject.GetInstanceID();
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
