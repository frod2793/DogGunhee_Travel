using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// [설명]: 독 구름(Poison Trail) 생성 전략입니다.
    /// 플레이어를 따라다니는 Emitter 인스턴스를 생성하고 관리합니다.
    /// </summary>
    public class PoisonTrailStrategy : IWeaponStrategy
    {
        #region 내부 변수

        private PoisonTrailEmitter m_emitterInstance;
        private WeaponDataSO m_data;
        private WeaponPoolManager m_poolManager;

        #endregion

        #region 인터페이스 구현

        public void Init(WeaponDataSO data, WeaponPoolManager poolManager)
        {
            m_data = data;
            m_poolManager = poolManager;
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            // Emitter가 없으면 생성 (Lazy Initialization)
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
            }
            else
            {
                // 이미 존재하면 스탯 동기화
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

        private void UpdateEmitterStats(WeaponRuntimeStats stats)
        {
            if (m_emitterInstance == null) return;

            m_emitterInstance.UpdateStats(
                stats.CurrentAttackPower,
                stats.MobStunTime,
                stats.CurrentCoolTime
            );
        }

        #endregion
    }
}