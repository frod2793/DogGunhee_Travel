using UnityEngine;
using System.Collections.Generic;
using InGame.Mob.MobBase;

namespace InGame.Weapon
{
    /// <summary>
    /// WeaponBallplay에 의해 생성된 공의 물리적 충돌과 데미지를 담당하는 클래스입니다.
    /// </summary>
    [RequireComponent(typeof(PolygonCollider2D))]
    public class BallDamageDealer : MonoBehaviour
    {
        #region 스탯 필드

        private float m_attackPower;
        private float m_mobStunTime;
        private float m_coolTime;

        #endregion

        #region 내부 변수

        private PolygonCollider2D m_polygonCollider;
        
        private readonly Dictionary<int, float> m_damageCooldowns = new Dictionary<int, float>();

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_polygonCollider = GetComponent<PolygonCollider2D>();
            
            if (m_polygonCollider == null)
            {
                m_polygonCollider = gameObject.AddComponent<PolygonCollider2D>();
            }

            if (!m_polygonCollider.isTrigger)
            {
                Debug.LogWarning($"[BallDamageDealer] '{name}'의 Collider가 Trigger가 아닙니다. 강제로 설정합니다.");
                m_polygonCollider.isTrigger = true;
            }
        }

        private void OnEnable()
        {
            // 활성화 시 초기화 로직 (필요시)
        }

        private void OnDisable()
        {
            m_damageCooldowns.Clear();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!other.CompareTag("Mob")) return;

            int enemyId = other.GetInstanceID();

            if (!m_damageCooldowns.TryGetValue(enemyId, out float nextTime) || Time.time >= nextTime)
            {
                if (other.TryGetComponent(out MobBase mob) && !mob.IsDead)
                {
                    mob.TakeDamage(m_attackPower, m_mobStunTime);
                    m_damageCooldowns[enemyId] = Time.time + m_coolTime;
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Mob"))
            {
                m_damageCooldowns.Remove(other.GetInstanceID());
            }
        }

        #endregion

        #region 초기화

        /// <summary>
        /// BallDamageDealer를 초기화합니다.
        /// </summary>
        /// <param name="attackPower">공격력</param>
        /// <param name="mobStunTime">스턴 시간</param>
        /// <param name="coolTime">데미지 쿨타임</param>
        public void Initialize(float attackPower, float mobStunTime, float coolTime)
        {
            m_attackPower = attackPower;
            m_mobStunTime = mobStunTime;
            m_coolTime = coolTime;
        }

        #endregion
    }
}
