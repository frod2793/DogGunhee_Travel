using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;

namespace DogGuns_Games.vamsir
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Collider2D))]
    public class ShieldShockwave : MonoBehaviour
    {
        #region 내부 변수
        private Animator m_animator;
        private Collider2D m_collider;
        private IObjectPool<ShieldShockwave> m_pool;
        
        private float m_damage;
        private float m_stunTime;
        
        // 애니메이션 해시 캐싱
        private static readonly int k_AnimHashImpact = Animator.StringToHash("Impact");
        #endregion

        private void Awake()
        {
            m_animator = GetComponent<Animator>();
            m_collider = GetComponent<Collider2D>();
            m_collider.enabled = false; 
        }

        /// <summary>
        /// 이펙트 초기화 (공격 속도 포함)
        /// </summary>
        /// <param name="attackSpeed">공격 속도 배율 (기본 1.0)</param>
        public void Initialize(IObjectPool<ShieldShockwave> pool, float damage, float stunTime, float attackSpeed)
        {
            m_pool = pool;
            m_damage = damage;
            m_stunTime = stunTime;

            // 0이 들어올 경우 방지 (기본 1배속)
            float speedMultiplier = (attackSpeed > 0) ? attackSpeed : 1.0f;

            PlayEffectAsync(speedMultiplier).Forget();
        }

        private async UniTaskVoid PlayEffectAsync(float speedMultiplier)
        {
            // [핵심] 애니메이터 속도 배속 적용
            if (m_animator != null)
            {
                m_animator.speed = speedMultiplier;
            }

            // 1. 트리거 발동 & 콜라이더 켜기
            m_animator.SetTrigger(k_AnimHashImpact);
            m_collider.enabled = true;

            // 2. 상태 전이 대기
            await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());

            // 3. 전이 중이라면 끝날 때까지 대기
            while (m_animator.IsInTransition(0))
            {
                await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
            }

            // 4. 애니메이션 진행률(NormalizedTime) 기반 대기
            // 속도(speedMultiplier)가 빨라지면 normalizedTime도 더 빨리 1.0에 도달합니다.
            // 안전장치: 최대 대기 시간 설정 (기본 길이 / 배속 + 여유분)
            var stateInfo = m_animator.GetCurrentAnimatorStateInfo(0);
            float estimatedDuration = (stateInfo.length / speedMultiplier) + 0.2f; 
            float timer = 0f;

            while (timer < estimatedDuration)
            {
                // 갱신된 상태 정보 가져오기
                stateInfo = m_animator.GetCurrentAnimatorStateInfo(0);
                
                if (stateInfo.normalizedTime >= 1.0f)
                {
                    break; // 재생 완료
                }

                timer += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
            }

            // 5. 종료: 콜라이더 끄고 반환
            m_collider.enabled = false;
            
            // [중요] 반환 전 애니메이터 속도 초기화 (풀링 재사용 시 문제 방지)
            if (m_animator != null) m_animator.speed = 1f;

            ReleaseToPool();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Mob"))
            {
                if (other.TryGetComponent(out VamserMobBase mob))
                {
                    mob.TakeDamage(m_damage, m_stunTime);
                }
            }
        }

        private void ReleaseToPool()
        {
            if (m_pool != null) m_pool.Release(this);
            else Destroy(gameObject);
        }
    }
}