using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;
using InGame.Mob.MobBase;
using InGame.Weapon.Base;
using InGame.Weapon.Strategies;
using System.Threading;
using InGame.ObjectPool;
using InGame.Weapon.Controllers;
using InGame.Weapon.Logic;

namespace InGame.Weapon
{
    /// <summary>
    /// 먹물(StrongBlackWater) 무기의 지속 효과 영역을 관리하는 컴포넌트입니다.
    /// 영역 내 적에게 지속적인 피해와 슬로우 상태이상을 부여합니다.
    /// </summary>
    public class BlackWaterEffect : MonoBehaviour, IAuraEffect
    {
        #region 설정 데이터

        [Header("감지 및 비주얼 설정")]
        [SerializeField] private LayerMask m_targetLayer;
        [SerializeField] private Animator m_animator;
        [SerializeField] private Collider2D m_collider2D;

        #endregion

        #region 내부 상태 및 캐시

        private BlackWaterLogic m_logic;
        private Vector3 m_originalScale;
        private CancellationTokenSource m_cts;
        private ContactFilter2D m_contactFilter;
        private readonly List<Collider2D> m_hitResults = new List<Collider2D>(20);
        private bool m_isEvolvedState = false;

        // 애니메이터 파라미터 해시
        private static readonly int k_AnimStateLevel1 = Animator.StringToHash("Level1");
        private static readonly int k_AnimTriggerLevel2 = Animator.StringToHash("Level2");

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            if (m_collider2D == null) m_collider2D = GetComponentInChildren<Collider2D>();
            if (m_collider2D != null) { m_collider2D.enabled = false; m_collider2D.isTrigger = true; }
            if (m_animator == null) m_animator = GetComponentInChildren<Animator>();

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

        #region 초기화 및 제어

        public void Initialize(WeaponRuntimeStats stats)
        {
            BlackWaterTuningData? tuningData = null;
            var view = WeaponPoolManager.Instance.GetComponent<BlackWaterView>();
            if (view != null)
            {
                tuningData = new BlackWaterTuningData
                {
                    DamageTickInterval = view.DamageTickInterval,
                    SlowAmount = view.SlowAmount,
                    SlowDuration = view.SlowDuration
                };
            }

            m_logic = new BlackWaterLogic(stats, tuningData);
            m_isEvolvedState = !m_logic.IsEvolved; 
            ActivateEffect();
        }

        public void UpdateStats(WeaponRuntimeStats stats)
        {
            m_logic.UpdateStats(stats);
            UpdateWeaponState();
        }

        public void Deactivate()
        {
            m_cts?.Cancel();
            m_cts?.Dispose();
            m_cts = null;
            if (m_collider2D != null) m_collider2D.enabled = false;
            gameObject.SetActive(false);
        }

        #endregion

        #region 제어 로직 (비동기 및 물리)

        private void ActivateEffect()
        {
            m_cts?.Cancel();
            m_cts = new CancellationTokenSource();

            UpdateWeaponState();
            if (m_collider2D != null) m_collider2D.enabled = true;

            transform.DOScale(m_originalScale, 0.3f).From(Vector3.zero).SetEase(Ease.OutBack);
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

        private void UpdateWeaponState()
        {
            transform.localScale = m_originalScale;
            if (m_animator != null && m_logic != null)
            {
                if (m_logic.IsEvolved != m_isEvolvedState)
                {
                    m_isEvolvedState = m_logic.IsEvolved;
                    if (m_isEvolvedState) m_animator.SetTrigger(k_AnimTriggerLevel2);
                    else m_animator.Play(k_AnimStateLevel1);
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
                    if (m_logic.IsEvolved) mob.ApplySlow(m_logic.SlowAmount, m_logic.SlowDuration);
                }
            }
        }

        #endregion
    }
}
