using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

namespace DogGuns_Games.vamsir
{
    public class WeaponStrongBlackWater : WeaphonBase
    {
        #region 인스펙터 필드

        [Header("기본 공격 설정")]
        [Tooltip("틱 데미지가 들어가는 간격입니다.")]
        [FormerlySerializedAs("damageTickInterval")]
        [SerializeField] private float m_damageTickInterval = 0.5f;

        [Header("업그레이드 스탯 설정")]
        // [삭제됨] 크기 배율 변수 제거
        // [SerializeField] private float m_rangeMultiplier = 1.5f;
        
        [Tooltip("적의 이동 속도를 감소시키는 비율 (0.3 = 30% 감소)")]
        [FormerlySerializedAs("slowAmount")]
        [SerializeField] [Range(0f, 1f)] private float m_slowAmount = 0.3f;
        
        [FormerlySerializedAs("slowDuration")]
        [SerializeField] private float m_slowDuration = 1.0f;

        [Header("감지 및 비주얼 설정")]
        [Tooltip("공격 대상 레이어 (Mob)")]
        [SerializeField] private LayerMask m_targetLayer;

        [Tooltip("자식 오브젝트에 있는 애니메이터 컴포넌트")]
        [SerializeField] private Animator m_animator;

        [Tooltip("자식 오브젝트에 있는 공격 판정용 콜라이더")]
        [SerializeField] private Collider2D m_collider2D;

        #endregion

        #region 내부 변수

        private bool m_isAttacking;
        private Vector3 m_originalScale;
        
        private bool m_currentLevelState = false; 

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

            m_contactFilter.NoFilter();
            m_contactFilter.useTriggers = true;
            m_contactFilter.SetLayerMask(m_targetLayer);
            m_contactFilter.useLayerMask = true;
        }

        protected override void OnEnable()
        {
            base.OnEnable(); 
            
            if (m_collider2D != null) m_collider2D.enabled = false;
            // 항상 초기 크기로 시작
            transform.localScale = m_originalScale;
            m_isAttacking = false;
            
            m_currentLevelState = this.isUpgradelv2;
            
            AttackRoutineAsync().Forget();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            transform.DOKill(); 
            m_isAttacking = false;
        }

        #endregion

        #region 무기 동작 관리

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            base.Weaphon_Attack(attackAngle);
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

            // 초기 상태 적용
            UpdateWeaponState(); 
            if (m_collider2D != null) m_collider2D.enabled = true;
            
            // 등장 이펙트 (0 -> 원래 크기)
            transform.DOScale(m_originalScale, 0.3f).From(Vector3.zero).SetEase(Ease.OutBack);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    CheckLevelUpState();

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

        private void CheckLevelUpState()
        {
            if (m_currentLevelState != this.isUpgradelv2)
            {
                m_currentLevelState = this.isUpgradelv2;
                UpdateWeaponState(); 
            }
        }

        /// <summary>
        /// 현재 레벨(isUpgradelv2)에 맞춰 무기 상태(애니메이션)를 설정합니다.
        /// [수정됨] 스케일 변경 로직 삭제됨.
        /// </summary>
        private void UpdateWeaponState()
        {
            // 1. 스케일: 항상 원본 크기 유지
            // (이전에 있던 if-else 분기 삭제)
            transform.localScale = m_originalScale;

            // 2. 애니메이션 전환
            if (m_animator != null)
            {
                if (this.isUpgradelv2)
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
                
                if (target.TryGetComponent(out VamserMobBase mob))
                {
                    if (!mob.IsDead) 
                    {
                        mob.TakeDamage(attackPower); 

                        if (this.isUpgradelv2)
                        {
                            mob.ApplySlow(m_slowAmount, m_slowDuration);
                        }
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