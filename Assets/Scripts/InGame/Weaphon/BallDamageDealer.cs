using UnityEngine;
using System.Collections.Generic;
using Vamser_like.Mob.MobBase;
using Vamser_like.Weaphon.Base;

namespace Vamser_like.Weaphon
{
    /// <summary>
    /// WeaphonBallplay에 의해 생성된 공의 물리적 충돌과 데미지를 담당하는 클래스입니다.
    /// </summary>
    [RequireComponent(typeof(PolygonCollider2D))]
    public class BallDamageDealer : WeaphonBase
    {
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

        private new void OnEnable()
        {
            SetWeaphonState(WeaphonState.Idle);
        }

        private new void OnDisable()
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
                    mob.TakeDamage(attackPower, mobStunTime);
                    m_damageCooldowns[enemyId] = Time.time + coolTime;
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

        public void Initialize(WeaphonBase parentWeapon)
        {
            this.isEvolved = parentWeapon.isEvolved;
            this.attackPower = parentWeapon.attackPower;
            this.mobStunTime = parentWeapon.mobStunTime;
            this.coolTime = parentWeapon.coolTime;
        }

        #endregion
    }
}