using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.Mob.MobBase;
using InGame.ObjectPool;
using System.Threading;

namespace InGame.Weapon
{
    /// <summary>
    /// 히어로 랜딩 무기(Shield)의 충격파 효과를 관리하는 컴포넌트입니다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Collider2D))]
    public class ShieldShockwave : MonoBehaviour
    {
        #region 설정 데이터

        [Header("위치 설정")]
        [Tooltip("캐릭터 기준 충격파 생성 오프셋")]
        [SerializeField] private Vector3 m_spawnOffset = new Vector3(0, -0.5f, 0);

        #endregion

        #region 내부 상태 및 캐시

        private Animator m_animator;
        private Collider2D m_collider;
        private float m_damage;
        private float m_stunTime;
        
        private static readonly int k_AnimHashImpact = Animator.StringToHash("Impact");

        #endregion

        #region 프로퍼티

        public Vector3 SpawnOffset => m_spawnOffset;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_animator = GetComponent<Animator>();
            m_collider = GetComponent<Collider2D>();
            m_collider.enabled = false; 
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Mob") && other.TryGetComponent(out MobBase mob))
            {
                mob.TakeDamage(m_damage, m_stunTime);
                mob.PlayDamageEffect();
            }
        }

        #endregion

        #region 초기화 및 제어

        public void Initialize(float damage, float stunTime, float attackSpeed)
        {
            m_damage = damage;
            m_stunTime = stunTime;
            PlayEffectSequenceAsync(attackSpeed > 0 ? attackSpeed : 1.0f).Forget();
        }

        private void ReleaseToPool()
        {
            if (WeaponPoolManager.Instance != null) WeaponPoolManager.Instance.Release(this); 
        }

        #endregion

        #region 제어 로직 (비동기)

        private async UniTaskVoid PlayEffectSequenceAsync(float speedMultiplier)
        {
            var token = this.GetCancellationTokenOnDestroy();
            if (m_animator != null) m_animator.speed = speedMultiplier;

            m_animator.SetTrigger(k_AnimHashImpact);
            m_collider.enabled = true;

            await UniTask.Yield(PlayerLoopTiming.Update, token);
            while (m_animator.IsInTransition(0)) await UniTask.Yield(PlayerLoopTiming.Update, token);

            var stateInfo = m_animator.GetCurrentAnimatorStateInfo(0);
            float estimatedDuration = (stateInfo.length / speedMultiplier) + 0.2f; 
            float timer = 0f;

            while (timer < estimatedDuration)
            {
                if (m_animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1.0f) break;
                timer += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            m_collider.enabled = false;
            if (m_animator != null) m_animator.speed = 1f;
            ReleaseToPool();
        }

        #endregion
    }
}