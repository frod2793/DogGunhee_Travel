using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using InGame.Mob.MobBase;
using InGame.ObjectPool;
using UnityEngine.Rendering.Universal; // Light2D 사용을 위해 추가
using DG.Tweening; // DOTween 사용을 위해 추가

namespace InGame.Weapon
{
    public class FlamePillar : MonoBehaviour
    {
        [Header("애니메이터")]
        [SerializeField] private Animator m_warningAnimator;
        [SerializeField] private List<Animator> m_flameAnimators;
        [SerializeField] private Light2D m_flameLight; // 불기둥 조명 오브젝트
        [SerializeField] private float m_minFalloffStrength = 0.35f; // 최소 Falloff 강도
        [SerializeField] private float m_maxFalloffStrength = 0.5f; // 최대 Falloff 강도
     

        [Header("공격 판정")]
        [SerializeField] private Collider2D m_damageCollider;
        [SerializeField] private LayerMask m_targetLayer;
        


        private float m_directDamage;
        private float m_dotDamage;
        private float m_dotDuration;
        private int m_dotTicks;

        private ContactFilter2D m_contactFilter;
        private readonly List<Collider2D> m_hitResults = new List<Collider2D>(20);
        private readonly HashSet<MobBase> m_hitMobs = new HashSet<MobBase>();

        private void Awake()
        {
            m_contactFilter.SetLayerMask(m_targetLayer);
            m_contactFilter.useTriggers = true;
            
            if (m_damageCollider != null) m_damageCollider.enabled = false;
            if (m_warningAnimator != null) m_warningAnimator.gameObject.SetActive(false);
            if (m_flameLight != null) m_flameLight.gameObject.SetActive(false);
            m_flameAnimators?.ForEach(anim => anim.gameObject.SetActive(false));
        }

        public void Activate(Vector3 position, float directDamage, float dotDamage, float dotDuration, int dotTicks)
        {
            ResetState();
            
            transform.position = position;
            
            m_directDamage = directDamage;
            m_dotDamage = dotDamage;
            m_dotDuration = dotDuration;
            m_dotTicks = dotTicks;

            gameObject.SetActive(true);
            AttackSequenceAsync().Forget();
        }

        private void ResetState()
        {
            // 1. 트윈 제거 (Kill Tweens)
            if (m_flameLight != null)
            {
                DOTween.Kill(m_flameLight);
                m_flameLight.falloffIntensity = 0f;
                m_flameLight.gameObject.SetActive(false);
            }

            // 2. 경고 애니메이션 비활성화 (Disable Warning)
            if (m_warningAnimator != null)
            {
                m_warningAnimator.gameObject.SetActive(false);
            }

            // 3. 애니메이터 및 스프라이트 초기화 (Reset Animators & Sprites)
            if (m_flameAnimators != null)
            {
                foreach (var anim in m_flameAnimators)
                {
                    if (anim != null)
                    {
                        // 스프라이트 페이드 트윈 제거 및 색상 복구
                        if (anim.TryGetComponent(out SpriteRenderer sr))
                        {
                            DOTween.Kill(sr);
                            sr.color = Color.white; // Alpha 1로 복구
                        }
                        anim.gameObject.SetActive(false);
                    }
                }
            }
            
            // 4. 콜라이더 비활성화 (Collider Disabled)
            if (m_damageCollider != null) m_damageCollider.enabled = false;
        }

        private async UniTaskVoid AttackSequenceAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();
            m_hitMobs.Clear();

            // 1. 경고 애니메이션
            if (m_warningAnimator != null)
            {
                m_warningAnimator.gameObject.SetActive(true);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                AnimatorStateInfo warningStateInfo = m_warningAnimator.GetCurrentAnimatorStateInfo(0);
                float warningAnimLength = warningStateInfo.length / (warningStateInfo.speed > 0 ? warningStateInfo.speed : 1f);
                
                // [수정] ignoreTimeScale: true -> false (애니메이터는 GameTime을 따르므로 딜레이도 맞춰야 함)
                if (warningAnimLength < 0.1f) warningAnimLength = 0.5f; // 안전 장치: 최소 지연 시간 보장
                await UniTask.Delay(System.TimeSpan.FromSeconds(warningAnimLength), cancellationToken: token);
                m_warningAnimator.gameObject.SetActive(false);
            }

            // 2. 랜덤 불기둥 선택 및 재생
            Animator selectedFlameAnimator = null;
            SpriteRenderer selectedSpriteRenderer = null;
            Light2D selectedLight2D = null;
            float flameAnimLength = 0f;

            if (m_flameAnimators != null && m_flameAnimators.Count > 0)
            {
                // [Logic] 중복 활성화 방지를 위해 먼저 모두 비활성화
                foreach (var anim in m_flameAnimators)
                {
                    if (anim != null) anim.gameObject.SetActive(false);
                }

                int randomIndex = Random.Range(0, m_flameAnimators.Count);
                selectedFlameAnimator = m_flameAnimators[randomIndex];
                selectedFlameAnimator.gameObject.SetActive(true);
                
                // SpriteRenderer와 Light2D 컴포넌트 가져오기
                selectedFlameAnimator.TryGetComponent(out selectedSpriteRenderer);
                selectedFlameAnimator.TryGetComponent(out selectedLight2D);

                // [수정] 알파 값 초기화 (이전 페이드 아웃으로 투명해졌을 수 있음)
                if (selectedSpriteRenderer != null)
                {
                    // 불필요한 구조체 복사 방지 (Color.white 등 활용 가능하지만, 기존 색상 유지 필요시 아래처럼)
                    // 여기서는 단순히 투명도만 복구하면 되므로 기존 R,G,B 유지
                    Color currentColor = selectedSpriteRenderer.color;
                    if (currentColor.a < 1f)
                    {
                        selectedSpriteRenderer.color = new Color(currentColor.r, currentColor.g, currentColor.b, 1f);
                    }
                }

                // [수정] 불기둥 애니메이션의 실제 재생 시간 계산 (먼저 계산해야 Tween에 사용 가능)
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                AnimatorStateInfo flameStateInfo = selectedFlameAnimator.GetCurrentAnimatorStateInfo(0);
                flameAnimLength = flameStateInfo.length / (flameStateInfo.speed > 0 ? flameStateInfo.speed : 1f);
                
                // 조명 활성화 및 애니메이션 + 스프라이트 페이드 아웃
                if (m_flameLight != null)
                {
                    m_flameLight.gameObject.SetActive(true);
                    
                    // 초기값 0에서 목표값까지 부드럽게 변화 (Fade In/Out)
                    m_flameLight.falloffIntensity = 0f;
                    float targetStrength = Random.Range(m_minFalloffStrength, m_maxFalloffStrength);
                    
                    // 시간 분배: 20% 등장, 50% 유지, 30% 소멸
                    float fadeInDuration = flameAnimLength * 0.2f;
                    float holdDuration = flameAnimLength * 0.5f;
                    float fadeOutDuration = flameAnimLength * 0.3f;

                    Sequence animSeq = DOTween.Sequence();
                    
                    // 1. Light Fade In -> 2. Hold -> 3. Light Fade Out
                    // [CS4014 Warning 억제] Fire-and-forget 트윈 체인
                    _ = animSeq.Append(DOTween.To(() => m_flameLight.falloffIntensity, x => m_flameLight.falloffIntensity = x, targetStrength, fadeInDuration).SetEase(Ease.OutQuad))
                           .AppendInterval(holdDuration)
                           .Append(DOTween.To(() => m_flameLight.falloffIntensity, x => m_flameLight.falloffIntensity = x, 1f, fadeOutDuration).SetEase(Ease.InQuad));
                    
                    if (selectedSpriteRenderer != null)
                    {
                        // [Fix] CS4014: Join 결과를 discard 하여 경고 무시
                        _ = animSeq.Join(selectedSpriteRenderer.DOFade(0f, fadeOutDuration).SetEase(Ease.InQuad));
                    }
                    
                    // 토큰 취소 시 안전하게 Kill
                    animSeq.ToUniTask(cancellationToken: token).Forget(); 
                }
            }
            
            // 3. 불기둥 애니메이션 시간 동안 피해 판정
            if (m_damageCollider != null) m_damageCollider.enabled = true;

            float timer = 0f;
            while (timer < flameAnimLength)
            {
                // [동기화] Light2D 스프라이트 동기화
                if (selectedLight2D != null && selectedSpriteRenderer != null)
                {
                    selectedLight2D.lightCookieSprite = selectedSpriteRenderer.sprite;
                }

                CheckForDamage();
                await UniTask.Yield(PlayerLoopTiming.Update, token); // Visual Sync를 위해 Update로 변경
                timer += Time.deltaTime;
            }

            if (m_damageCollider != null) m_damageCollider.enabled = false;
            if (m_flameLight != null) m_flameLight.gameObject.SetActive(false);

            // 4. 오브젝트 풀로 반환
            // m_pool.Release(this); // 제거
            WeaponPoolManager.Instance.Release(this); // WeaponPoolManager를 통해 반환
        }

        private void CheckForDamage()
        {
            int hitCount = m_damageCollider.Overlap(m_contactFilter, m_hitResults);
            for (int i = 0; i < hitCount; i++)
            {
                if (m_hitResults[i].TryGetComponent(out MobBase mob) && !m_hitMobs.Contains(mob))
                {
                    m_hitMobs.Add(mob);
                    mob.TakeDamage(m_directDamage);
                    mob.ApplyDamageOverTime(m_dotDamage, m_dotDuration, m_dotTicks);
                }
            }
        }
    }
}