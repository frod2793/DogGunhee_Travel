using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.Pool;
using System;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// WeaphonFlame에 의해 소환되어, 경고 표시 후 불기둥 공격을 수행하는 효과 컨트롤러입니다.
    /// </summary>
    public class FlamePillar : MonoBehaviour
    {
        [Header("애니메이터")]
        [SerializeField] private Animator m_warningAnimator;
        [SerializeField] private List<Animator> m_flameAnimators;
        
        [Header("공격 판정")]
        [SerializeField] private Collider2D m_damageCollider;
        [SerializeField] private LayerMask m_targetLayer;
        
        [Header("타이밍 설정")]
        [Tooltip("불기둥이 활성화되어 피해를 주는 시간입니다.")]
        [SerializeField] private float m_flameDuration = 1.0f;

        private float m_directDamage;
        private float m_dotDamage;
        private float m_dotDuration;
        private int m_dotTicks;

        private IObjectPool<FlamePillar> m_pool;
        private ContactFilter2D m_contactFilter;
        private readonly List<Collider2D> m_hitResults = new List<Collider2D>(20);
        private readonly HashSet<MobBase> m_hitMobs = new HashSet<MobBase>();

        private void Awake()
        {
            m_contactFilter.SetLayerMask(m_targetLayer);
            m_contactFilter.useTriggers = true;
            
            if (m_damageCollider != null) m_damageCollider.enabled = false;
            if (m_warningAnimator != null) m_warningAnimator.gameObject.SetActive(false);
            m_flameAnimators?.ForEach(anim => anim.gameObject.SetActive(false));
        }

        public void Activate(IObjectPool<FlamePillar> pool, Vector3 position, float directDamage, float dotDamage, float dotDuration, int dotTicks)
        {
            m_pool = pool;
            transform.position = position;
            
            m_directDamage = directDamage;
            m_dotDamage = dotDamage;
            m_dotDuration = dotDuration;
            m_dotTicks = dotTicks;

            gameObject.SetActive(true);
            AttackSequenceAsync().Forget();
        }
        
        private async UniTaskVoid AttackSequenceAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();
            m_hitMobs.Clear();

            // 1. 경고 애니메이션
            if (m_warningAnimator != null)
            {
                m_warningAnimator.gameObject.SetActive(true);

                // 애니메이션이 끝날 때까지 대기합니다.
                // 애니메이터가 다음 프레임에 상태를 업데이트할 시간을 줍니다.
                await UniTask.Yield(cancellationToken: token);
                
                var animatorStateInfo = m_warningAnimator.GetCurrentAnimatorStateInfo(0);
                float warningAnimationLength = animatorStateInfo.length;
                
                await UniTask.Delay(TimeSpan.FromSeconds(warningAnimationLength), ignoreTimeScale: true, cancellationToken: token);
                m_warningAnimator.gameObject.SetActive(false);
            }
            
            // 2. 랜덤 불기둥 선택 및 활성화
            Animator selectedFlameAnimator = null;
            if (m_flameAnimators != null && m_flameAnimators.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, m_flameAnimators.Count);
                selectedFlameAnimator = m_flameAnimators[randomIndex];
                selectedFlameAnimator.gameObject.SetActive(true);
            }

            // 3. 피해 판정 시작
            if (m_damageCollider != null) m_damageCollider.enabled = true;

            float timer = 0f;
            while (timer < m_flameDuration)
            {
                CheckForDamage();
                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, token);
                timer += Time.fixedDeltaTime;
            }

            // 4. 비활성화 및 오브젝트 풀 반환
            if (m_damageCollider != null) m_damageCollider.enabled = false;
            if (selectedFlameAnimator != null) selectedFlameAnimator.gameObject.SetActive(false);

            m_pool.Release(this);
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