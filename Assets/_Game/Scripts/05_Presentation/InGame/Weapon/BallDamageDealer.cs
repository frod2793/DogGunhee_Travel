using UnityEngine;
using InGame.Mob.MobBase;

namespace InGame.Weapon
{
    /// <summary>
    /// [설명]: 공놀이(Ball) 무기의 물리 충돌 및 데미지 판정을 담당하는 컴포넌트입니다.
    /// 투사체 프리팹에 부착되어 Trigger 충돌 시 적에게 데미지와 경직을 부여합니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BallDamageDealer : MonoBehaviour
    {
        #region 내부 변수

        private Collider2D m_collider;
        
        // 초기화 데이터
        private float m_attackPower;
        private float m_stunTime;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_collider = GetComponent<Collider2D>();
            
            // 트리거 설정 강제 (물리 충돌 방지)
            if (m_collider != null)
            {
                m_collider.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Mob 태그 확인
            if (!other.CompareTag("Mob")) return;

            // MobBase 컴포넌트 확인 및 데미지 적용
            if (other.TryGetComponent(out MobBase mob))
            {
                if (!mob.IsDead)
                {
                    mob.TakeDamage(m_attackPower, m_stunTime);
                }
            }
        }

        #endregion

        #region 초기화 및 제어

        /// <summary>
        /// [설명]: 데미지 딜러의 전투 수치를 초기화합니다.
        /// </summary>
        /// <param name="damage">적에게 입힐 데미지</param>
        /// <param name="stunTime">적에게 적용할 경직 시간</param>
        public void Init(float damage, float stunTime)
        {
            m_attackPower = damage;
            m_stunTime = stunTime;
        }

        #endregion
    }
}