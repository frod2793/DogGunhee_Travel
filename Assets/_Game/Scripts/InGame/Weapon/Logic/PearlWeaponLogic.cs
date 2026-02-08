using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Logic
{
    /// <summary>
    /// 싸구려 진주 무기의 비즈니스 로직을 담당하는 POCO 클래스입니다.
    /// </summary>
    public class PearlWeaponLogic
    {
        private readonly WeaponRuntimeStats m_stats;
        private readonly PearlTuningData m_tuningData;

        public float AttackPower => m_stats.CurrentAttackPower;
        public float AttackSpeed => (m_stats.CurrentAttackSpeed > 0) ? m_stats.CurrentAttackSpeed : 1f;
        public float StunTime => m_stats.MobStunTime;
        public bool IsEvolved => m_stats.IsEvolved;

        public float HitCooldown => m_tuningData.HitCooldown;

        public PearlWeaponLogic(WeaponRuntimeStats stats, PearlTuningData tuningData)
        {
            m_stats = stats;
            m_tuningData = tuningData;
        }

        public void UpdateStats(WeaponRuntimeStats stats)
        {
            // 참조형이므로 자동 반영되지만, 명시적 업데이트가 필요할 경우 확장 가능
        }
    }

    /// <summary>
    /// PearlWeaponView에서 추출한 튜닝 데이터
    /// </summary>
    public struct PearlTuningData
    {
        public float HitCooldown;
    }
}
