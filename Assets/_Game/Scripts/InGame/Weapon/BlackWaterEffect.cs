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

        #region 내부 상태 및 변수

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
            if (m_collider2D == null)
            {
                m_collider2D = GetComponentInChildren<Collider2D>();
            }

            if (m_collider2D != null)
            {
                m_collider2D.enabled = false;
                m_collider2D.isTrigger = true;
            }

            if (m_animator == null)
            {
                m_animator = GetComponentInChildren<Animator>();
            }

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

        public void Init(WeaponRuntimeStats stats)
        {
            BlackWaterTuningData? tuningData = null;
            
            // 튜닝 데이터가 필요한 경우 캐스팅하여 참조
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
            if (m_logic == null)
            {
                return;
            }

            m_logic.UpdateStats(stats);
            SyncWeaponVisuals();
        }

        public void Deactivate()
        {
            m_cts?.Cancel();
            m_cts?.Dispose();
            m_cts = null;

            if (m_collider2D != null)
            {
                m_collider2D.enabled = false;
            }

            gameObject.SetActive(false);
        }

        #endregion

        #region 상세 효과 제어 로직

        /// <summary>
        /// 이펙트 활성화 연출 및 데미지 루프를 시작합니다.
        /// </summary>
        private void ActivateEffect()
        {
            m_cts?.Cancel();
            m_cts = new CancellationTokenSource();

            SyncWeaponVisuals();
            
            if (m_collider2D != null)
            {
                m_collider2D.enabled = true;
            }

            // 등장 애니메이션 (DOTween)
            transform.DOScale(m_originalScale, 0.3f).From(Vector3.zero).SetEase(Ease.OutBack);
            
            TickDamageLoopAsync(m_cts.Token).Forget();
        }

        /// <summary>
        /// 일정 주기마다 영역 내 적에게 데미지를 입히는 비동기 루프입니다.
        /// </summary>
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
            catch (System.OperationCanceledException)
            {
                // 취소 시 예외 처리
            }
            finally
            {
                if (m_collider2D != null)
                {
                    m_collider2D.enabled = false;
                }
            }
        }

        /// <summary>
        /// 무기 진화 여부에 따라 애니메이션과 비주얼 상태를 동기화합니다.
        /// </summary>
        private void SyncWeaponVisuals()
        {
            if (m_logic == null)
            {
                return;
            }

            transform.localScale = m_originalScale;

            if (m_animator != null)
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

        /// <summary>
        /// 콜라이더 겹침 검사를 통해 영역 내 적에게 데미지 및 슬로우 효과를 부여합니다.
        /// </summary>
        private void ProcessTickDamage()
        {
            if (m_collider2D == null || m_logic == null)
            {
                return;
            }

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
