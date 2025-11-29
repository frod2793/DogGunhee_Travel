using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DogGuns_Games.vamsir
{
    public class FlamePillar : MonoBehaviour
    {
        [Header("애니메이터")]
        [SerializeField] private Animator m_warningAnimator;
        [SerializeField] private List<Animator> m_flameAnimators;

        [Header("공격 판정")]
        [SerializeField] private Collider2D m_damageCollider;
        [SerializeField] private LayerMask m_targetLayer;
        
        // [삭제] 고정된 시간 대신 애니메이션 길이를 동적으로 사용
        // [SerializeField] private float m_flameDuration = 1.0f;

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
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                AnimatorStateInfo warningStateInfo = m_warningAnimator.GetCurrentAnimatorStateInfo(0);
                float warningAnimLength = warningStateInfo.length / (warningStateInfo.speed > 0 ? warningStateInfo.speed : 1f);
                await UniTask.Delay(System.TimeSpan.FromSeconds(warningAnimLength), ignoreTimeScale: true, cancellationToken: token);
                m_warningAnimator.gameObject.SetActive(false);
            }

            // 2. 랜덤 불기둥 선택 및 재생
            Animator selectedFlameAnimator = null;
            float flameAnimLength = 0f;

            if (m_flameAnimators != null && m_flameAnimators.Count > 0)
            {
                int randomIndex = Random.Range(0, m_flameAnimators.Count);
                selectedFlameAnimator = m_flameAnimators[randomIndex];
                selectedFlameAnimator.gameObject.SetActive(true);

                // [수정] 불기둥 애니메이션의 실제 재생 시간 계산
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                AnimatorStateInfo flameStateInfo = selectedFlameAnimator.GetCurrentAnimatorStateInfo(0);
                flameAnimLength = flameStateInfo.length / (flameStateInfo.speed > 0 ? flameStateInfo.speed : 1f);
            }
            
            // 3. 불기둥 애니메이션 시간 동안 피해 판정
            if (m_damageCollider != null) m_damageCollider.enabled = true;

            float timer = 0f;
            while (timer < flameAnimLength)
            {
                CheckForDamage();
                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, token);
                timer += Time.fixedDeltaTime;
            }

            if (m_damageCollider != null) m_damageCollider.enabled = false;
            if (selectedFlameAnimator != null) selectedFlameAnimator.gameObject.SetActive(false);

            // 4. 오브젝트 풀로 반환
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