using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.Mob.MobBase;
using InGame.ObjectPool;

namespace InGame.Weapon
{
    /// <summary>
    /// 히어로 랜딩(Shield) 무기 사용 시 발생하는 충격파 효과를 관리하는 컴포넌트입니다.
    /// <br/> 애니메이션 재생 타이밍에 맞춰 콜라이더를 활성화하고, 범위 내 적에게 데미지를 입힙니다.
    /// </summary>
    [RequireComponent(typeof(Animator), typeof(Collider2D))]
    public class ShieldShockwave : MonoBehaviour
    {
        #region 1. 내부 변수 및 컴포넌트 (Components & State)

        [Header("1. 위치 설정")]
        [Tooltip("캐릭터 발밑 기준 충격파 생성 오프셋")]
        [SerializeField] private Vector3 m_spawnOffset = new Vector3(0, -0.5f, 0);

        // 컴포넌트 참조
        private Animator m_animator;
        private Collider2D m_collider;
        private WeaponPoolManager m_poolManager;

        // 전투 데이터
        private float m_damage;
        private float m_stunTime;

        // 애니메이션 해시
        private static readonly int k_AnimHashImpact = Animator.StringToHash("Impact");

        #endregion

        #region 2. 프로퍼티 (Properties)

        /// <summary>
        /// 캐릭터 기준 생성 위치 오프셋
        /// </summary>
        public Vector3 SpawnOffset => m_spawnOffset;

        #endregion

        #region 3. Unity 라이프사이클 (Lifecycle)

        private void Awake()
        {
            m_animator = GetComponent<Animator>();
            m_collider = GetComponent<Collider2D>();
            
            // 초기 상태에서는 판정 비활성화
            if (m_collider != null)
            {
                m_collider.isTrigger = true;
                m_collider.enabled = false;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Mob")) return;

            if (other.TryGetComponent(out MobBase mob) && !mob.IsDead)
            {
                mob.TakeDamage(m_damage, m_stunTime);
                
                // 피격 효과 재생 (MobBase 인터페이스에 따라 호출)
                // mob.PlayDamageEffect();
            }
        }

        #endregion

        #region 4. 초기화 및 실행 (Init & Execution)

        /// <summary>
        /// 충격파 효과를 초기화하고 애니메이션 시퀀스를 시작합니다.
        /// </summary>
        /// <param name="damage">공격력</param>
        /// <param name="stunTime">경직 시간</param>
        /// <param name="attackSpeed">공격 속도 (애니메이션 배속)</param>
        /// <param name="poolManager">오브젝트 풀 매니저</param>
        public void Init(float damage, float stunTime, float attackSpeed, WeaponPoolManager poolManager)
        {
            m_damage = damage;
            m_stunTime = stunTime;
            m_poolManager = poolManager;

            // 비동기 시퀀스 시작
            PlayEffectSequenceAsync(attackSpeed > 0 ? attackSpeed : 1.0f).Forget();
        }

        private void ReleaseToPool()
        {
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

        #region 5. 비동기 연출 로직 (Async Logic)

        /// <summary>
        /// 애니메이션 재생 및 충돌체 활성화 주기를 제어하는 비동기 루틴입니다.
        /// </summary>
        /// <param name="speedMultiplier">애니메이션 속도 배율</param>
        private async UniTaskVoid PlayEffectSequenceAsync(float speedMultiplier)
        {
            var token = this.GetCancellationTokenOnDestroy();

            if (m_animator != null)
            {
                m_animator.speed = speedMultiplier;
                m_animator.SetTrigger(k_AnimHashImpact);
            }

            // 판정 활성화
            if (m_collider != null) m_collider.enabled = true;

            try
            {
                // 1. 애니메이션 상태 진입 대기 (한 프레임 딜레이)
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                
                if (m_animator != null)
                {
                    while (m_animator.IsInTransition(0))
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                    }

                    // 2. 애니메이션 완료 대기 (NormalizedTime 1.0 도달 시까지)
                    var stateInfo = m_animator.GetCurrentAnimatorStateInfo(0);
                    float estimatedDuration = (stateInfo.length / speedMultiplier) + 0.2f; // 안전 여유 시간
                    float timer = 0f;

                    while (timer < estimatedDuration)
                    {
                        if (m_animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1.0f)
                        {
                            break;
                        }

                        timer += Time.deltaTime;
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                // 정상 취소 처리
            }
            finally
            {
                // 3. 정리 및 풀 반환
                if (m_collider != null) m_collider.enabled = false;
                
                if (m_animator != null)
                {
                    m_animator.speed = 1f;
                }

                ReleaseToPool();
            }
        }

        #endregion
    }
}