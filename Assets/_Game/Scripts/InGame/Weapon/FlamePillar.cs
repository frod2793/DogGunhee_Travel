using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.Mob.MobBase;
using InGame.ObjectPool;
using InGame.Weapon.Logic;

namespace InGame.Weapon
{
    /// <summary>
    /// 불기둥(Flame Pillar)의 시각적 연출과 물리 충돌을 담당하는 View 컴포넌트입니다.
    /// <br/> 경고 이펙트 후 불기둥을 소환하며, Logic 데이터를 기반으로 데미지를 판정합니다.
    /// </summary>
    public class FlamePillar : MonoBehaviour
    {
        #region 1. 내부 변수 및 컴포넌트 (Components & State)

        [Header("1. 애니메이션 설정")]
        [Tooltip("공격 전 바닥에 표시될 경고 애니메이터")]
        [SerializeField] private Animator m_warningAnimator;

        [Tooltip("실제 불기둥 애니메이터 리스트 (랜덤 재생용)")]
        [SerializeField] private List<Animator> m_flameAnimators;

        [Header("2. 조명(Light) 설정")]
        [Tooltip("불기둥과 함께 활성화될 2D 광원")]
        [SerializeField] private Light2D m_flameLight;

        [Tooltip("조명 최소 강도 (Falloff)")]
        [SerializeField] private float m_minFalloffStrength = 0.35f;

        [Tooltip("조명 최대 강도 (Falloff)")]
        [SerializeField] private float m_maxFalloffStrength = 0.5f;

        [Header("3. 공격 판정 설정")]
        [Tooltip("데미지 판정을 위한 트리거 콜라이더")]
        [SerializeField] private Collider2D m_damageCollider;

        [Tooltip("공격 대상 레이어")]
        [SerializeField] private LayerMask m_targetLayer;

        // 로직 및 관리 객체
        private FlamePillarLogic m_logic;
        private WeaponPoolManager m_poolManager;
        
        // 물리 연산 최적화
        private ContactFilter2D m_contactFilter;
        private readonly List<Collider2D> m_hitResults = new List<Collider2D>(20);

        #endregion

        #region 2. Unity 라이프사이클 (Lifecycle)

        private void Awake()
        {
            // 물리 필터 설정 (GC Alloc 방지)
            m_contactFilter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = true,
                layerMask = m_targetLayer
            };

            // 초기 컴포넌트 상태 설정
            ResetComponents();
        }

        private void OnDisable()
        {
            // 트윈 및 상태 정리
            if (m_flameLight != null) DOTween.Kill(m_flameLight);
            
            // 활성화된 불기둥 스프라이트 트윈 정리
            if (m_flameAnimators != null)
            {
                foreach (var anim in m_flameAnimators)
                {
                    if (anim != null && anim.TryGetComponent(out SpriteRenderer sr))
                    {
                        DOTween.Kill(sr);
                    }
                }
            }
        }

        #endregion

        #region 3. 초기화 및 제어 (Init & Control)

        /// <summary>
        /// 불기둥을 지정된 위치에 초기화하고 공격 시퀀스를 시작합니다.
        /// </summary>
        /// <param name="position">소환 위치</param>
        /// <param name="logic">데미지 로직 객체</param>
        /// <param name="poolManager">오브젝트 풀 매니저</param>
        public void Init(Vector3 position, FlamePillarLogic logic, WeaponPoolManager poolManager)
        {
            m_logic = logic;
            m_poolManager = poolManager;
            
            // 로직 상태 리셋 (피격 리스트 초기화 등)
            m_logic.Reset();

            // 비주얼 상태 리셋
            ResetViewState();

            // 위치 설정 및 활성화
            transform.position = position;
            gameObject.SetActive(true);

            // 비동기 공격 시퀀스 시작
            AttackSequenceAsync().Forget();
        }

        private void ResetComponents()
        {
            if (m_damageCollider != null) m_damageCollider.enabled = false;
            if (m_warningAnimator != null) m_warningAnimator.gameObject.SetActive(false);
            if (m_flameLight != null) m_flameLight.gameObject.SetActive(false);

            if (m_flameAnimators != null)
            {
                foreach (var anim in m_flameAnimators)
                {
                    if (anim != null) anim.gameObject.SetActive(false);
                }
            }
        }

        private void ResetViewState()
        {
            // 조명 초기화
            if (m_flameLight != null)
            {
                DOTween.Kill(m_flameLight);
                m_flameLight.falloffIntensity = 0f;
                m_flameLight.gameObject.SetActive(false);
            }

            // 애니메이터 초기화
            if (m_warningAnimator != null) m_warningAnimator.gameObject.SetActive(false);

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

            // 콜라이더 비활성화
            if (m_damageCollider != null) m_damageCollider.enabled = false;
        }

        private void ReleaseToPool()
        {
            if (m_flameLight != null) m_flameLight.gameObject.SetActive(false);

            if (m_poolManager != null)
            {
                m_poolManager.Release(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region 4. 공격 시퀀스 (Attack Sequence)

        /// <summary>
        /// 경고 -> 점화 -> 연소(데미지) -> 종료 순서로 진행되는 메인 시퀀스입니다.
        /// </summary>
        private async UniTaskVoid AttackSequenceAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();

            try
            {
                // 1. 경고 단계 (Warning)
                await PlayWarningPhaseAsync(token);

                // 2. 점화 단계 (Ignite) - 랜덤 불기둥 선택
                (Animator activeAnimator, float animLength) = SelectAndPlayFlameAnim();
                
                if (activeAnimator == null) return;

                // 3. 연소 단계 (Burn) - 데미지 판정 및 조명 연출
                await PlayBurnPhaseAsync(activeAnimator, animLength, token);
            }
            finally
            {
                // 시퀀스 종료 후 풀 반환
                ReleaseToPool();
            }
        }

        /// <summary>
        /// 바닥에 경고 표시를 재생하고 대기합니다.
        /// </summary>
        private async UniTask PlayWarningPhaseAsync(CancellationToken token)
        {
            if (m_warningAnimator == null) return;

            m_warningAnimator.gameObject.SetActive(true);
            
            // 상태 갱신 대기
            await UniTask.Yield(PlayerLoopTiming.Update, token);

            AnimatorStateInfo stateInfo = m_warningAnimator.GetCurrentAnimatorStateInfo(0);
            float duration = stateInfo.length / Mathf.Max(0.1f, stateInfo.speed);

            await UniTask.Delay(System.TimeSpan.FromSeconds(duration), cancellationToken: token);
            
            m_warningAnimator.gameObject.SetActive(false);
        }

        /// <summary>
        /// 불기둥 애니메이터 중 하나를 랜덤으로 선택하여 재생합니다.
        /// </summary>
        /// <returns>선택된 애니메이터와 애니메이션 길이</returns>
        private (Animator, float) SelectAndPlayFlameAnim()
        {
            if (m_flameAnimators == null || m_flameAnimators.Count == 0)
            {
                return (null, 0f);
            }

            int randomIndex = Random.Range(0, m_flameAnimators.Count);
            Animator animator = m_flameAnimators[randomIndex];
            
            if (animator != null)
            {
                animator.gameObject.SetActive(true);
                if (animator.TryGetComponent(out SpriteRenderer sr))
                {
                    sr.color = Color.white;
                }

                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                float length = stateInfo.length / Mathf.Max(0.1f, stateInfo.speed);
                
                return (animator, length);
            }

            return (null, 0f);
        }

        /// <summary>
        /// 불기둥이 유지되는 동안 조명 효과와 충돌 검사를 수행합니다.
        /// </summary>
        private async UniTask PlayBurnPhaseAsync(Animator animator, float length, CancellationToken token)
        {
            // 조명 페이드 연출 시작
            StartLightEffectSequence(animator, length);

            // 콜라이더 활성화
            if (m_damageCollider != null) m_damageCollider.enabled = true;

            float elapsed = 0f;
            while (elapsed < length)
            {
                // 매 프레임 충돌 검사 및 데미지 적용
                CheckCollisions();
                
                // 스프라이트에 맞춰 조명 모양 갱신
                SyncLightCookie(animator);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
                elapsed += Time.deltaTime;
            }

            if (m_damageCollider != null) m_damageCollider.enabled = false;
        }

        #endregion

        #region 5. 시각 효과 및 물리 판정 (Visuals & Physics)

        /// <summary>
        /// 불기둥의 타오름과 사라짐에 맞춰 조명 강도와 투명도를 조절합니다.
        /// </summary>
        private void StartLightEffectSequence(Animator animator, float length)
        {
            if (m_flameLight == null) return;

            m_flameLight.gameObject.SetActive(true);
            m_flameLight.falloffIntensity = 0f;

            float targetStrength = Random.Range(m_minFalloffStrength, m_maxFalloffStrength);
            float fadeOutTime = length * 0.3f;

            // DOTween 시퀀스 구성
            Sequence seq = DOTween.Sequence();
            
            // 1. 밝아짐
            seq.Append(DOTween.To(
                () => m_flameLight.falloffIntensity, 
                x => m_flameLight.falloffIntensity = x,
                targetStrength, length * 0.2f));
            
            // 2. 유지
            seq.AppendInterval(length * 0.5f);
            
            // 3. 어두워짐 (사라짐)
            seq.Append(DOTween.To(
                () => m_flameLight.falloffIntensity, 
                x => m_flameLight.falloffIntensity = x, 
                1f, fadeOutTime));

            // 스프라이트도 같이 페이드아웃
            if (animator.TryGetComponent(out SpriteRenderer sr))
            {
                seq.Join(sr.DOFade(0f, fadeOutTime));
            }

            seq.SetTarget(this).SetLink(gameObject);
        }

        /// <summary>
        /// 애니메이션의 현재 스프라이트 모양을 조명 쿠키로 설정하여 그림자를 동기화합니다.
        /// </summary>
        private void SyncLightCookie(Animator animator)
        {
            if (m_flameLight == null || animator == null) return;

            if (animator.TryGetComponent(out SpriteRenderer sr))
            {
                m_flameLight.lightCookieSprite = sr.sprite;
            }
        }

        /// <summary>
        /// 범위 내의 적을 감지하고 로직을 통해 데미지를 입힙니다.
        /// </summary>
        private void CheckCollisions()
        {
            if (m_logic == null || m_damageCollider == null) return;

            int hitCount = m_damageCollider.Overlap(m_contactFilter, m_hitResults);
            for (int i = 0; i < hitCount; i++)
            {
                var target = m_hitResults[i];
                if (target == null) continue;

                if (target.TryGetComponent(out MobBase mob))
                {
                    // 로직에서 중복 피격 여부 확인 (TryHit)
                    if (m_logic.TryHit(mob))
                    {
                        ApplyDamage(mob);
                    }
                }
            }
        }

        /// <summary>
        /// 대상에게 직접 데미지와 지속 데미지(DoT)를 적용합니다.
        /// </summary>
        private void ApplyDamage(MobBase mob)
        {
            // 직접 데미지
            mob.TakeDamage(m_logic.DirectDamage);
            mob.PlayDamageEffect(m_logic.HitFlashColor);

            // [수정됨] 지속 데미지 예약 (명명된 인수 'onTick:' 제거)
            mob.ApplyDamageOverTime(
                m_logic.DotDamage, 
                m_logic.Duration, 
                m_logic.TickCount, 
                () => 
                {
                    if (mob != null && !mob.IsDead)
                    {
                        mob.PlayDamageEffect(m_logic.HitFlashColor);
                    }
                }
            );
        }

        #endregion
    }
}