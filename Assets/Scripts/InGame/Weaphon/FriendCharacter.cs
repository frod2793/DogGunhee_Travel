using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using Vamser_like.Mob.MobBase; // 몬스터에게 피해를 주기 위함

namespace Vamser_like.Weaphon
{
    /// <summary>
    /// 친구 캐릭터의 애니메이션 타입을 정의합니다.
    /// Animator Controller에 "FriendType" (Int) 파라미터와 "FriendDrop" (Trigger) 파라미터가 필요합니다.
    /// "FriendType" 값에 따라 다른 애니메이션 상태로 전이되도록 설정해야 합니다.
    /// </summary>
    public enum FriendAnimationType
    {
        TypeA = 0,
        TypeB = 1,
        TypeC = 2,
        TypeD = 3
    }

    /// <summary>
    /// WeaPhon_Friends에서 소환되는 친구 캐릭터의 동작을 관리합니다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Collider2D))] // 충돌 감지를 위해 Collider2D 필요
    public class FriendCharacter : MonoBehaviour
    {
        [SerializeField]
        private Animator m_animator;
        [SerializeField]
        private Collider2D m_collider;

        private float m_attackPower;
        private float m_mobStunTime;

        // Animator Parameters (캐싱하여 성능 최적화)
        private static readonly int k_AnimHashFriendType = Animator.StringToHash("FriendType");
        private static readonly int k_AnimHashFriendDrop = Animator.StringToHash("FriendDrop");

        private void Awake()
        {
            if (m_animator == null)
            {
                m_animator = GetComponent<Animator>();
            }
            if (m_collider == null)
            {
                m_collider = GetComponent<Collider2D>();
            }
            m_collider.isTrigger = true; // 물리적 충돌 없이 이벤트만 발생
            m_collider.enabled = false; // 기본적으로 비활성화
        }

        /// <summary>
        /// 친구 캐릭터를 초기화하고 애니메이션을 시작합니다.
        /// </summary>
        /// <param name="position">캐릭터가 소환될 위치</param>
        /// <param name="animType">재생할 애니메이션 타입</param>
        /// <param name="attackPower">캐릭터의 공격력</param>
        /// <param name="mobStunTime">몬스터 기절 시간</param>
        public void Initialize(Vector3 position, FriendAnimationType animType, float attackPower, float mobStunTime)
        {
            transform.position = position;
            m_attackPower = attackPower;
            m_mobStunTime = mobStunTime;

            PlayFriendAnimationAsync(animType).Forget();
        }

        private async UniTaskVoid PlayFriendAnimationAsync(FriendAnimationType animType)
        {
            var token = this.GetCancellationTokenOnDestroy();
            m_collider.enabled = true; // 활성화 시 콜라이더 활성화

            try
            {
                if (m_animator != null)
                {
                    // 애니메이터 파라미터를 설정하여 특정 애니메이션 타입 재생
                    m_animator.SetInteger(k_AnimHashFriendType, (int)animType);
                    m_animator.SetTrigger(k_AnimHashFriendDrop);

                    // 상태 전이를 위해 한 프레임 대기
                    await UniTask.Yield(PlayerLoopTiming.Update, token);

                    // 현재 애니메이션 상태 정보를 가져와 재생 시간 계산
                    AnimatorStateInfo stateInfo = m_animator.GetCurrentAnimatorStateInfo(0);
                    float animLength = stateInfo.length / (stateInfo.speed > 0 ? stateInfo.speed : 1f);

                    // 애니메이션이 완료될 때까지 대기
                    await UniTask.Delay(System.TimeSpan.FromSeconds(animLength), ignoreTimeScale: true, cancellationToken: token);
                }
            }
            finally
            {
                // 애니메이션 완료 또는 취소 시 콜라이더 비활성화 및 풀로 반환
                m_collider.enabled = false;
                WeaponPoolManager.Instance.Release(this);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Mob") && other.TryGetComponent<MobBase>(out var mob))
            {
                if (!mob.IsDead)
                {
                    mob.TakeDamage(m_attackPower, m_mobStunTime);
                }
            }
        }
    }
}