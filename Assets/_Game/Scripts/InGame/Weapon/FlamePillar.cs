using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using InGame.Mob.MobBase;
using InGame.ObjectPool;
using UnityEngine.Rendering.Universal;
using DG.Tweening;
using InGame.Weapon.Logic;
using System.Threading;

namespace InGame.Weapon
{
    /// <summary>
    /// 불기둥(Flame Pillar)의 시각적 연출과 충돌 감지를 담당하는 View 컴포넌트입니다.
    /// </summary>
    public class FlamePillar : MonoBehaviour
    {
        #region 설정 데이터

        [Header("애니메이터 설정")]
        [Tooltip("공격 전 경고 애니메이션을 담당하는 애니메이터")]
        [SerializeField] private Animator m_warningAnimator;

        [Tooltip("실제 불기둥 애니메이터 리스트 (랜덤 재생)")]
        [SerializeField] private List<Animator> m_flameAnimators;

        [Tooltip("불기둥과 함께 활성화될 조명 (Light2D)")]
        [SerializeField] private Light2D m_flameLight;

        [Tooltip("조명 최소 강도 (Falloff)")]
        [SerializeField] private float m_minFalloffStrength = 0.35f;

        [Tooltip("조명 최대 강도 (Falloff)")]
        [SerializeField] private float m_maxFalloffStrength = 0.5f;

        [Header("공격 판정 설정")]
        [Tooltip("데미지를 입힐 콜라이더")]
        [SerializeField] private Collider2D m_damageCollider;

        [Tooltip("공격 대상 레이어 마스크")]
        [SerializeField] private LayerMask m_targetLayer;

        #endregion

        #region 내부 상태 및 캐시

        private FlamePillarLogic m_logic;
        private ContactFilter2D m_contactFilter;
        private readonly List<Collider2D> m_hitResults = new List<Collider2D>(20);

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_contactFilter = new ContactFilter2D();
            m_contactFilter.SetLayerMask(m_targetLayer);
            m_contactFilter.useTriggers = true;
            
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            if (m_damageCollider != null) m_damageCollider.enabled = false;
            if (m_warningAnimator != null) m_warningAnimator.gameObject.SetActive(false);
            if (m_flameLight != null) m_flameLight.gameObject.SetActive(false);
            m_flameAnimators?.ForEach(anim => anim.gameObject.SetActive(false));
        }

        #endregion

        #region 초기화 및 제어

        /// <summary>
        /// 불기둥을 지정된 위치에 활성화하고 공격 시퀀스를 시작합니다.
        /// </summary>
        public void Activate(Vector3 position, FlamePillarLogic logic)
        {
            m_logic = logic;
            m_logic.Reset();
            
            ResetViewState();
            
            transform.position = position;
            gameObject.SetActive(true);
            
            AttackSequenceAsync().Forget();
        }

        private void ResetViewState()
        {
            if (m_flameLight != null)
            {
                DOTween.Kill(m_flameLight);
                m_flameLight.falloffIntensity = 0f;
                m_flameLight.gameObject.SetActive(false);
            }

            if (m_warningAnimator != null)
            {
                m_warningAnimator.gameObject.SetActive(false);
            }

            if (m_flameAnimators != null)
            {
                foreach (var anim in m_flameAnimators)
                {
                    if (anim == null) continue;
                    if (anim.TryGetComponent(out SpriteRenderer sr))
                    {
                        DOTween.Kill(sr);
                        sr.color = Color.white;
                    }
                    anim.gameObject.SetActive(false);
                }
            }
            if (m_damageCollider != null) m_damageCollider.enabled = false;
        }

        private void FinishAndRelease()
        {
            if (m_flameLight != null) m_flameLight.gameObject.SetActive(false);
            WeaponPoolManager.Instance.Release(this);
        }

        #endregion

        #region 제어 로직 (비동기 및 물리)

        private async UniTaskVoid AttackSequenceAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();

            try
            {
                // 1. 경고 단계
                await PlayWarningCycleAsync(token);

                // 2. 소환 단계
                (Animator animator, float length) = PlayIgniteEffect();
                if (animator == null) return;

                // 3. 유지 및 데미지 단계
                await PlayBurnCycleAsync(animator, length, token);
            }
            finally
            {
                FinishAndRelease();
            }
        }

        private async UniTask PlayWarningCycleAsync(CancellationToken token)
        {
            if (m_warningAnimator == null) return;

            m_warningAnimator.gameObject.SetActive(true);
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            
            AnimatorStateInfo stateInfo = m_warningAnimator.GetCurrentAnimatorStateInfo(0);
            float duration = stateInfo.length / Mathf.Max(0.1f, stateInfo.speed);
            
            await UniTask.Delay(System.TimeSpan.FromSeconds(duration), cancellationToken: token);
            m_warningAnimator.gameObject.SetActive(false);
        }

        private (Animator, float) PlayIgniteEffect()
        {
            if (m_flameAnimators == null || m_flameAnimators.Count == 0) return (null, 0f);

            int randomIndex = Random.Range(0, m_flameAnimators.Count);
            var animator = m_flameAnimators[randomIndex];
            animator.gameObject.SetActive(true);

            if (animator.TryGetComponent(out SpriteRenderer sr)) sr.color = Color.white;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            float length = stateInfo.length / Mathf.Max(0.1f, stateInfo.speed);
            
            return (animator, length);
        }

        private async UniTask PlayBurnCycleAsync(Animator animator, float length, CancellationToken token)
        {
            StartLightFadeSequence(animator, length);

            if (m_damageCollider != null) m_damageCollider.enabled = true;

            float elapsed = 0f;
            while (elapsed < length)
            {
                CheckCollisionsAndApplyDamage();
                SyncLightCookie(animator);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                elapsed += Time.deltaTime;
            }

            if (m_damageCollider != null) m_damageCollider.enabled = false;
        }

        private void StartLightFadeSequence(Animator animator, float length)
        {
            if (m_flameLight == null) return;

            m_flameLight.gameObject.SetActive(true);
            m_flameLight.falloffIntensity = 0f;

            float targetStrength = Random.Range(m_minFalloffStrength, m_maxFalloffStrength);
            float fadeOutTime = length * 0.3f;

            Sequence seq = DOTween.Sequence();
            seq.Append(DOTween.To(() => m_flameLight.falloffIntensity, x => m_flameLight.falloffIntensity = x, targetStrength, length * 0.2f))
               .AppendInterval(length * 0.5f)
               .Append(DOTween.To(() => m_flameLight.falloffIntensity, x => m_flameLight.falloffIntensity = x, 1f, fadeOutTime));

            if (animator.TryGetComponent(out SpriteRenderer sr)) seq.Join(sr.DOFade(0f, fadeOutTime));

            seq.SetTarget(this).SetLink(gameObject);
        }

        private void SyncLightCookie(Animator animator)
        {
            if (m_flameLight == null || animator == null) return;
            if (animator.TryGetComponent(out SpriteRenderer sr)) m_flameLight.lightCookieSprite = sr.sprite;
        }

        private void CheckCollisionsAndApplyDamage()
        {
            if (m_logic == null || m_damageCollider == null) return;

            int hitCount = m_damageCollider.Overlap(m_contactFilter, m_hitResults);
            for (int i = 0; i < hitCount; i++)
            {
                if (m_hitResults[i].TryGetComponent(out MobBase mob))
                {
                    if (m_logic.TryHit(mob)) ProcessDamage(mob);
                }
            }
        }

        private void ProcessDamage(MobBase mob)
        {
            mob.TakeDamage(m_logic.DirectDamage);
            mob.PlayDamageEffect(m_logic.HitFlashColor);

            mob.ApplyDamageOverTime(m_logic.DotDamage, m_logic.Duration, m_logic.TickCount, () => 
            {
                if (mob != null && !mob.IsDead) mob.PlayDamageEffect(m_logic.HitFlashColor);
            });
        }

        #endregion
    }
}