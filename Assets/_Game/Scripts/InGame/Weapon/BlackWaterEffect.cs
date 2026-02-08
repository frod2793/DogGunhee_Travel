using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;
using InGame.Mob.MobBase;
using InGame.Weapon.Base;
using InGame.Weapon.Strategies;
using System.Threading;
using InGame.ObjectPool;
using InGame.Weapon.Logic;
using InGame.Weapon.Controllers;

namespace InGame.Weapon
{
    /// <summary>
    /// 블랙워터 지속 효과 컴포넌트입니다.
    /// 활성화 시 영역 내 적에게 틱 데미지를 주고, 진화 시 슬로우를 적용합니다.
    /// </summary>
    public class BlackWaterEffect : MonoBehaviour, IAuraEffect
    {
        #region 내부 변수

        private BlackWaterLogic m_logic;
        private Vector3 m_originalScale;
        private CancellationTokenSource m_cts;

        [Header("감지 및 비주얼 설정")]
        [SerializeField] private LayerMask m_targetLayer;
        [SerializeField] private Animator m_animator;
        [SerializeField] private Collider2D m_collider2D;

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

            if (m_collider2D != null)
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

        private void OnDisable()
        {
            m_cts?.Cancel();
            m_cts?.Dispose();
            m_cts = null;
            transform.DOKill();
        }

        #endregion

        #region IAuraEffect 구현

        public void Initialize(WeaponRuntimeStats stats)
        {
            // 1. 비주얼 설정 추출 (프리팹 기반 튜닝)
            BlackWaterTuningData? tuningData = null;
            var view =  WeaponPoolManager.Instance.GetComponent<BlackWaterView>();
            if (view != null)
            {
                tuningData = new BlackWaterTuningData
                {
                    DamageTickInterval = view.DamageTickInterval,
                    SlowAmount = view.SlowAmount,
                    SlowDuration = view.SlowDuration
                };
            }

            // 2. 로직 클래스 생성 (POCO)
            m_logic = new BlackWaterLogic(stats, tuningData);

            // 초기화 시 상태 불일치를 유도하여 반드시 애니메이션이 실행되도록 함
            m_isEvolvedState = !m_logic.IsEvolved; 
            ActivateEffect();
        }

        /// <summary>
        /// 런타임 중 스탯이 변경되었을 때 호출됩니다.
        /// </summary>
        public void UpdateStats(WeaponRuntimeStats stats)
        {
            m_logic.UpdateStats(stats);
            UpdateWeaponState();
        }

        /// <summary>
        /// 무기가 해제되거나 비활성화될 때 호출됩니다.
        /// </summary>
        public void Deactivate()
        {
            m_cts?.Cancel();
            m_cts?.Dispose();
            m_cts = null;
            
            if (m_collider2D != null) m_collider2D.enabled = false;
            gameObject.SetActive(false);
        }

        #endregion

        #region 효과 로직

        private void ActivateEffect()
        {
            m_cts?.Cancel();
            m_cts = new CancellationTokenSource();

            UpdateWeaponState();
            if (m_collider2D != null) m_collider2D.enabled = true;

            // 등장 애니메이션
            transform.DOScale(m_originalScale, 0.3f).From(Vector3.zero).SetEase(Ease.OutBack);

            // 틱 데미지 루프 시작
            TickDamageLoopAsync(m_cts.Token).Forget();
        }

        private async UniTaskVoid TickDamageLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && m_logic != null)
                {
                    ProcessTickDamage();

                    float tickDelay = m_logic.GetAdjustedTickDelay();
                    await UniTask.Delay(System.TimeSpan.FromSeconds(tickDelay), cancellationToken: token);
                }
            }
            catch (System.OperationCanceledException) { }
            finally
            {
                if (m_collider2D != null) m_collider2D.enabled = false;
            }
        }

        private bool m_isEvolvedState = false;

        private void UpdateWeaponState()
        {
            transform.localScale = m_originalScale;

            if (m_animator != null && m_logic != null)
            {
                if (m_logic.IsEvolved != m_isEvolvedState)
                {
                    m_isEvolvedState = m_logic.IsEvolved;

                    if (m_isEvolvedState)
                    {
                        m_animator.SetTrigger(k_AnimTriggerLevel2);
                    }
                    else
                    {
                        m_animator.Play(k_AnimStateLevel1);
                    }
                }
            }
        }

        private void ProcessTickDamage()
        {
            if (m_collider2D == null || m_logic == null) return;

            int hitCount = m_collider2D.Overlap(m_contactFilter, m_hitResults);

            for (int i = 0; i < hitCount; i++)
            {
                var target = m_hitResults[i];

                if (target.TryGetComponent(out MobBase mob) && !mob.IsDead)
                {
                    mob.TakeDamage(m_logic.AttackPower);

                    if (m_logic.IsEvolved)
                    {
                        mob.ApplySlow(m_logic.SlowAmount, m_logic.SlowDuration);
                    }
                }
            }
        }

        #endregion
    }
}
