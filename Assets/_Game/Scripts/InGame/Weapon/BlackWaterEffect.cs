using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;
using InGame.Mob.MobBase;
using InGame.Weapon.Base;
using InGame.Weapon.Strategies;
using System.Threading;

namespace InGame.Weapon
{
    /// <summary>
    /// 블랙워터 지속 효과 컴포넌트입니다.
    /// 활성화 시 영역 내 적에게 틱 데미지를 주고, 진화 시 슬로우를 적용합니다.
    /// </summary>
    public class BlackWaterEffect : MonoBehaviour, IAuraEffect
    {
        #region 인스펙터 필드

        [Header("틱 데미지 설정")]
        [Tooltip("틱 데미지가 들어가는 간격입니다.")]
        [SerializeField] private float m_damageTickInterval = 0.5f;

        [Header("슬로우 설정 (진화 시)")]
        [Tooltip("적의 이동 속도를 감소시키는 비율 (0.3 = 30% 감소)")]
        [SerializeField] [Range(0f, 1f)] private float m_slowAmount = 0.3f;
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

        private WeaponRuntimeStats m_stats;
        private Vector3 m_originalScale;
        private CancellationTokenSource m_cts;

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
            m_stats = stats;
            // 초기화 시 상태 불일치를 유도하여 반드시 애니메이션이 실행되도록 함
            m_isEvolvedState = !m_stats.IsEvolved; 
            ActivateEffect();
        }

        /// <summary>
        /// 런타임 중 스탯이 변경되었을 때 호출됩니다.
        /// </summary>
        public void UpdateStats(WeaponRuntimeStats stats)
        {
            m_stats = stats;
            // 스탯 변경에 따른 크기나 비주얼 업데이트 (필요 시)
            UpdateWeaponState();
        }

        /// <summary>
        /// 무기가 해제되거나 비활성화될 때 호출됩니다.
        /// </summary>
        public void Deactivate()
        {
            if (m_cts != null)
            {
                m_cts.Cancel();
                m_cts.Dispose();
                m_cts = null;
            }
            
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
                while (!token.IsCancellationRequested && m_stats != null)
                {
                    ProcessTickDamage();

                    float speed = m_stats.CurrentAttackSpeed > 0 ? m_stats.CurrentAttackSpeed : 1f;
                    float tickDelay = m_damageTickInterval / speed;

                    await UniTask.Delay(System.TimeSpan.FromSeconds(tickDelay), cancellationToken: token);
                }
            }
            catch (System.OperationCanceledException)
            {
                // 정상적인 취소
            }
            finally
            {
                if (m_collider2D != null) m_collider2D.enabled = false;
            }
        }

        private bool m_isEvolvedState = false;

        private void UpdateWeaponState()
        {
            transform.localScale = m_originalScale;

            if (m_animator != null && m_stats != null)
            {
                // 상태가 변경되었을 때만 트리거/재생 (중복 호출 방지)
                if (m_stats.IsEvolved != m_isEvolvedState)
                {
                    m_isEvolvedState = m_stats.IsEvolved;

                    if (m_isEvolvedState)
                    {
                        m_animator.SetTrigger(k_AnimTriggerLevel2);
                    }
                    else
                    {
                        m_animator.Play(k_AnimStateLevel1);
                    }
                }
                // 최초 실행 시에도 상태에 맞춰 초기화 필요 (State 초기값과 다를 경우)
                else if (m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash != k_AnimStateLevel1 && !m_isEvolvedState)
                {
                     // 비진화 상태인데 다른 애니메이션이 재생 중이라면 Level1로 강제 설정 (방어 코드)
                     // m_animator.Play(k_AnimStateLevel1);
                }
            }
        }

        private void ProcessTickDamage()
        {
            if (m_collider2D == null || m_stats == null) return;

            int hitCount = m_collider2D.Overlap(m_contactFilter, m_hitResults);

            for (int i = 0; i < hitCount; i++)
            {
                var target = m_hitResults[i];

                if (target.TryGetComponent(out MobBase mob) && !mob.IsDead)
                {
                    mob.TakeDamage(m_stats.CurrentAttackPower);

                    if (m_stats.IsEvolved)
                    {
                        mob.ApplySlow(m_slowAmount, m_slowDuration);
                    }
                }
            }
        }

        #endregion
    }
}
