using UnityEngine;
using InGame.Mob.MobBase;
using InGame.ObjectPool;

namespace InGame.Weapon
{
    /// <summary>
    /// 공놀이(Ball) 무기의 데미지 판정을 담당하는 컴포넌트입니다.
    /// 구체 콜라이더를 통해 범위 내 적에게 데미지와 경직을 줍니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BallDamageDealer : MonoBehaviour
    {
        #region 내부 상태 및 캐시

        private float m_attackPower;
        private float m_stunTime;
        private Collider2D m_collider;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_collider = GetComponent<Collider2D>();
            m_collider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Mob") && other.TryGetComponent<MobBase>(out var mob))
            {
                if (!mob.IsDead)
                {
                    mob.TakeDamage(m_attackPower, m_stunTime);
                    mob.PlayDamageEffect();
                }
            }
        }

        #endregion

        #region 초기화 및 제어

        /// <summary>
        /// 데미지 딜러의 공격 수치를 설정합니다.
        /// </summary>
        public void Initialize(float damage, float stunTime)
        {
            m_attackPower = damage;
            m_stunTime = stunTime;
        }

        #endregion
    }
}
