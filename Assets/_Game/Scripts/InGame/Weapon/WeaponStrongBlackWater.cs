using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.Mob.MobBase;
using InGame.Weapon.Base;
using UnityEngine;

namespace InGame.Weapon
{
    public class WeaponStrongBlackWater : WeaponBase
    {
        #region 인스펙터 필드

        [Header("기본 공격 설정")] [Tooltip("틱 데미지가 들어가는 간격입니다.")] [SerializeField]
        private float m_damageTickInterval = 0.5f;

        [Header("업그레이드 스탯 설정")] [Tooltip("적의 이동 속도를 감소시키는 비율 (0.3 = 30% 감소)")] [SerializeField] [Range(0f, 1f)]
        private float m_slowAmount = 0.3f;

        [SerializeField] private float m_slowDuration = 1.0f;

        [Header("감지 및 비주얼 설정")] [Tooltip("공격 대상 레이어 (Mob)")] [SerializeField]
        private LayerMask m_targetLayer;

        [Tooltip("자식 오브젝트에 있는 애니메이터 컴포넌트")] [SerializeField]
        private Animator m_animator;

        [Tooltip("자식 오브젝트에 있는 공격 판정용 콜라이더")] [SerializeField]
        private Collider2D m_collider2D;

        #endregion

        #region 내부 변수

        private bool m_isAttacking;
        private Vector3 m_originalScale;

        private bool m_currentEvolveState = false;

        private ContactFilter2D m_contactFilter;
        private readonly List<Collider2D> m_hitResults = new List<Collider2D>(20);

        private static readonly int k_AnimStateLevel1 = Animator.StringToHash("Level1");
        private static readonly int k_AnimTriggerLevel2 = Animator.StringToHash("Level2");

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            if (m_collider2D == null)
                m_collider2D = GetComponentInChildren<Collider2D>();

            if (m_collider2D == null)
            {
                Debug.LogError("[WeaponStrongBlackWater] Collider2D Missing!");
            }
            else
            {
                m_collider2D.enabled = false;
                m_collider2D.isTrigger = true;
            }

            if (m_animator == null)
                m_animator = GetComponentInChildren<Animator>();

            m_originalScale = transform.localScale;

            m_contactFilter = ContactFilter2D.noFilter;
            m_contactFilter.useTriggers = true;
            m_contactFilter.SetLayerMask(m_targetLayer);
            m_contactFilter.useLayerMask = true;
        }

        private new void OnEnable()
        {
            SetWeaponState(WeaponState.Idle);

            if (m_collider2D != null) m_collider2D.enabled = false;
            transform.localScale = m_originalScale;
            m_isAttacking = false;

            m_currentEvolveState = this.isEvolved;

            AttackRoutineAsync().Forget();
        }

        private new void OnDisable()
        {
            transform.DOKill();
            m_isAttacking = false;
        }

        #endregion

        #region 무기 동작 관리

        public override void Weapon_Attack(Vector3 attackAngle)
        {
            if (!m_isAttacking && gameObject.activeInHierarchy)
            {
                AttackRoutineAsync().Forget();
            }
        }

        #endregion

        #region 공격 로직 (UniTask)

        private async UniTaskVoid AttackRoutineAsync()
        {
            m_isAttacking = true;
            var token = this.GetCancellationTokenOnDestroy();

            UpdateWeaponState();
            if (m_collider2D != null) m_collider2D.enabled = true;

            // [CS4014 Warning 억제] Fire-and-forget 트윈
            _ = transform.DOScale(m_originalScale, 0.3f).From(Vector3.zero).SetEase(Ease.OutBack);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    CheckEvolveState();

                    ProcessTickDamage();

                    float speed = this.attackSpeed > 0 ? this.attackSpeed : 1f;
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
            if (m_currentEvolveState != this.isEvolved)
            {
                m_currentEvolveState = this.isEvolved;
                UpdateWeaponState();
            }
        }

        private void UpdateWeaponState()
        {
            transform.localScale = m_originalScale;

            if (m_animator != null)
            {
                if (this.isEvolved)
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
                    mob.TakeDamage(attackPower);

                    if (this.isEvolved)
                    {
                        mob.ApplySlow(m_slowAmount, m_slowDuration);
                    }
                }
            }
        }

        #endregion

        #region 디버그

        private void OnDrawGizmos()
        {
            if (m_collider2D != null && m_collider2D.enabled)
            {
                Gizmos.color = new Color(1, 0, 0, 0.3f);
                var bounds = m_collider2D.bounds;
                Gizmos.DrawCube(bounds.center, bounds.size);
            }
        }

        #endregion
    }
}