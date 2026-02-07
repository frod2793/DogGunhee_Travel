using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.Mob.MobBase;
using InGame.Weapon.Base;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 독물 장판 공격을 담당하는 POCO 컨트롤러입니다.
    /// </summary>
    public class BlackWaterWeaponController : WeaponControllerBase
    {
        #region 설정 데이터

        private float m_damageTickInterval;
        private float m_slowAmount;
        private float m_slowDuration;
        private LayerMask m_targetLayer;
        private Animator m_animator;
        private Collider2D m_collider2D;

        #endregion

        #region 내부 상태

        private bool m_isAttacking;
        private Vector3 m_originalScale;
        private bool m_currentEvolveState;
        private ContactFilter2D m_contactFilter;
        private readonly List<Collider2D> m_hitResults = new List<Collider2D>(20);
        private CancellationTokenSource m_attackCts;

        private static readonly int k_AnimStateLevel1 = Animator.StringToHash("Level1");
        private static readonly int k_AnimTriggerLevel2 = Animator.StringToHash("Level2");

        #endregion

        #region 초기화

        /// <summary>
        /// BlackWaterWeaponController를 초기화합니다.
        /// </summary>
        public void Init(
            WeaponDataSO data,
            Transform ownerTransform,
            Func<Vector3> getTargetDirection,
            float damageTickInterval,
            float slowAmount,
            float slowDuration,
            LayerMask targetLayer,
            Animator animator,
            Collider2D collider2D)
        {
            base.Init(data, ownerTransform, getTargetDirection);

            m_damageTickInterval = damageTickInterval;
            m_slowAmount = slowAmount;
            m_slowDuration = slowDuration;
            m_targetLayer = targetLayer;
            m_animator = animator;
            m_collider2D = collider2D;

            if (m_collider2D != null)
            {
                m_collider2D.enabled = false;
                m_collider2D.isTrigger = true;
            }

            m_originalScale = ownerTransform.localScale;
            m_currentEvolveState = m_runtimeStats.IsEvolved;

            m_contactFilter = ContactFilter2D.noFilter;
            m_contactFilter.useTriggers = true;
            m_contactFilter.SetLayerMask(m_targetLayer);
            m_contactFilter.useLayerMask = true;

            // 공격 루프 시작
            StartAttackLoop();
        }

        #endregion

        #region 공격 루프

        private void StartAttackLoop()
        {
            m_attackCts?.Cancel();
            m_attackCts = new CancellationTokenSource();
            AttackRoutineAsync(m_attackCts.Token).Forget();
        }

        private async UniTaskVoid AttackRoutineAsync(CancellationToken token)
        {
            m_isAttacking = true;

            UpdateWeaponState();
            if (m_collider2D != null) m_collider2D.enabled = true;

            // [CS4014 Warning 억제] Fire-and-forget 트윈
            _ = m_ownerTransform.DOScale(m_originalScale, 0.3f).From(Vector3.zero).SetEase(Ease.OutBack);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    CheckEvolveState();
                    ProcessTickDamage();

                    float speed = m_runtimeStats.AttackSpeed > 0 ? m_runtimeStats.AttackSpeed : 1f;
                    float tickDelay = m_damageTickInterval / speed;

                    await UniTask.Delay(TimeSpan.FromSeconds(tickDelay), cancellationToken: token);
                }
            }
            finally
            {
                if (m_collider2D != null) m_collider2D.enabled = false;
                m_isAttacking = false;
            }
        }

        private void CheckEvolveState()
        {
            if (m_currentEvolveState != m_runtimeStats.IsEvolved)
            {
                m_currentEvolveState = m_runtimeStats.IsEvolved;
                UpdateWeaponState();
            }
        }

        private void UpdateWeaponState()
        {
            m_ownerTransform.localScale = m_originalScale;

            if (m_animator != null)
            {
                if (m_runtimeStats.IsEvolved)
                {
                    m_animator.SetTrigger(k_AnimTriggerLevel2);
                }
                else
                {
                    m_animator.Play(k_AnimStateLevel1);
                }
            }
        }

        private void ProcessTickDamage()
        {
            if (m_collider2D == null) return;

            int hitCount = m_collider2D.Overlap(m_contactFilter, m_hitResults);

            for (int i = 0; i < hitCount; i++)
            {
                var target = m_hitResults[i];

                if (target.TryGetComponent(out MobBase mob) && !mob.IsDead)
                {
                    mob.TakeDamage(m_runtimeStats.AttackPower);

                    if (m_runtimeStats.IsEvolved)
                    {
                        mob.ApplySlow(m_slowAmount, m_slowDuration);
                    }
                }
            }
        }

        #endregion

        #region IWeaponController 구현

        public override void OnUpdate(float deltaTime)
        {
            // BlackWater는 자체 AttackLoop를 사용하므로 별도 Update 로직 불필요
        }

        public override void Attack(Vector3 direction)
        {
            // 자동 공격 루프를 사용하므로 수동 Attack은 무시됩니다.
            if (!m_isAttacking)
            {
                StartAttackLoop();
            }
        }

        public override void Dispose()
        {
            m_ownerTransform.DOKill();
            m_attackCts?.Cancel();
            m_attackCts?.Dispose();
            m_attackCts = null;
        }

        #endregion
    }
}
