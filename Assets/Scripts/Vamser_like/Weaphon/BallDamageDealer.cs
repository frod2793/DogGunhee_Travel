using UnityEngine;
using System.Collections.Generic;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// WeaphonBallplay에 의해 생성된 공의 물리적 충돌과 데미지를 담당하는 클래스입니다.
    /// [수정됨] 궤적(Trail/Line) 기능이 삭제되었습니다. 오직 충돌 판정만 처리합니다.
    /// </summary>
    [RequireComponent(typeof(PolygonCollider2D))]
    public class BallDamageDealer : Weaphon_base
    {
        #region 내부 변수

        private PolygonCollider2D m_polygonCollider;
        
        // 피해 쿨타임 관리 (Key: InstanceID, Value: NextAttackTime)
        private readonly Dictionary<int, float> m_damageCooldowns = new Dictionary<int, float>();

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            // PolygonCollider2D 가져오기
            m_polygonCollider = GetComponent<PolygonCollider2D>();
            
            // 만약 없다면 추가 (안전장치)
            if (m_polygonCollider == null)
            {
                m_polygonCollider = gameObject.AddComponent<PolygonCollider2D>();
            }

            // Trigger 설정 강제
            if (!m_polygonCollider.isTrigger)
            {
                Debug.LogWarning($"[BallDamageDealer] '{name}'의 Collider가 Trigger가 아닙니다. 강제로 설정합니다.");
                m_polygonCollider.isTrigger = true;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            // 활성화 시 쿨타임 초기화가 필요하다면 여기서 처리 (현재는 OnDisable에서 처리 중)
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            // 데이터 정리
            m_damageCooldowns.Clear();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!other.CompareTag("Mob")) return;

            int enemyId = other.GetInstanceID();

            // 쿨타임 체크
            if (!m_damageCooldowns.TryGetValue(enemyId, out float nextTime) || Time.time >= nextTime)
            {
                if (other.TryGetComponent(out VamserMobBase mob))
                {
                    if (!mob.IsDead)
                    {
                        // 데미지 적용
                        mob.TakeDamage(attackPower, mobStunTime);

                        // 쿨타임 갱신
                        m_damageCooldowns[enemyId] = Time.time + coolTime;
                    }
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

        public void Initialize(Weaphon_base parentWeapon)
        {
            this.isUpgradelv2 = parentWeapon.isUpgradelv2;
            this.attackPower = parentWeapon.attackPower;
            this.mobStunTime = parentWeapon.mobStunTime;
            this.coolTime = parentWeapon.coolTime;
        }

        #endregion
    }
}