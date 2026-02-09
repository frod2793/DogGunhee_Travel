using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 독 구름/자국(Poison Trail) 생성 전략입니다.
    /// </summary>
    public class PoisonTrailStrategy : IWeaponStrategy
    {
        #region 내부 상태 및 변수

        private PoisonTrailEmitter m_emitterInstance;
        private WeaponDataSO m_data;

        #endregion

        #region IWeaponStrategy 구현

        public void Init(WeaponDataSO data)
        {
            m_data = data;
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            // 이미터가 없으면 생성 (Lazy Initialization)
            if (m_emitterInstance == null)
            {
                if (m_data.ModelPrefab != null)
                {
                    var obj = Object.Instantiate(m_data.ModelPrefab, owner.position, Quaternion.identity, owner);
                    m_emitterInstance = obj.GetComponent<PoisonTrailEmitter>();
                    
                    if (m_emitterInstance != null)
                    {
                        m_emitterInstance.Init(
                            stats.CurrentAttackPower,
                            stats.MobStunTime,
                            stats.CurrentCoolTime
                        );
                    }
                }
                else
                {
                    Debug.LogWarning("[PoisonTrailStrategy] ModelPrefab이 설정되지 않았습니다.");
                }
            }
            else
            {
                // 이미 존재하면 스탯만 실시간 동기화
                UpdateEmitterStats(stats);
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            if (m_emitterInstance != null)
            {
                UpdateEmitterStats(stats);
            }
        }

        #endregion

        #region 상세 로직

        /// <summary>
        /// 이미터의 스탯 정보를 최신화합니다.
        /// </summary>
        private void UpdateEmitterStats(WeaponRuntimeStats stats)
        {
            if (m_emitterInstance == null)
            {
                return;
            }

            m_emitterInstance.UpdateStats(
                stats.CurrentAttackPower,
                stats.MobStunTime,
                stats.CurrentCoolTime
            );
        }

        #endregion
    }
}
