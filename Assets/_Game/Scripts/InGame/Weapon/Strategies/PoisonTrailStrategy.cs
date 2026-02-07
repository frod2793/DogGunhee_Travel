using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    public class PoisonTrailStrategy : IWeaponStrategy
    {
        private PoisonTrailEmitter m_emitterInstance;
        private WeaponDataSO m_data;

        public void Initialize(WeaponDataSO data)
        {
            m_data = data;
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            // Emitter가 없으면 생성 (Lazy Init)
            if (m_emitterInstance == null)
            {
                if (m_data.ModelPrefab != null)
                {
                    var obj = Object.Instantiate(m_data.ModelPrefab, owner.position, Quaternion.identity, owner);
                    m_emitterInstance = obj.GetComponent<PoisonTrailEmitter>();
                    
                    if (m_emitterInstance != null)
                    {
                        m_emitterInstance.Initialize(
                            stats.CurrentAttackPower,
                            stats.MobStunTime,
                            stats.CurrentCoolTime // 쿨타임은 데미지 주기로 사용
                        );
                    }
                }
                else
                {
                    // ModelPrefab이 없을 경우 경고
                    // InGame.LogManager.LogWarning("PoisonTrailStrategy: ModelPrefab is null in WeaponDataSO");
                }
            }
            else
            {
                // 이미 존재하면 스탯 갱신
                m_emitterInstance.UpdateStats(
                    stats.CurrentAttackPower,
                    stats.MobStunTime,
                    stats.CurrentCoolTime
                );
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            if (m_emitterInstance != null)
            {
                m_emitterInstance.UpdateStats(
                    stats.CurrentAttackPower,
                    stats.MobStunTime,
                    stats.CurrentCoolTime
                );
            }
        }
    }
}
