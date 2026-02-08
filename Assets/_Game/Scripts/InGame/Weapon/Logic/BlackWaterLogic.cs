using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Logic
{
    /// <summary>
    /// 블랙워터(Ink) 무기의 에디터 조정 데이터를 담는 구조체입니다.
    /// </summary>
    public struct BlackWaterTuningData
    {
        public float DamageTickInterval;
        public float SlowAmount;
        public float SlowDuration;
    }

    /// <summary>
    /// 블랙워터 무기의 비즈니스 로직을 담당하는 POCO 클래스입니다.
    /// </summary>
    public class BlackWaterLogic
    {
        private WeaponRuntimeStats m_stats;
        
        public float DamageTickInterval { get; private set; } = 0.5f;
        public float SlowAmount { get; private set; } = 0.3f;
        public float SlowDuration { get; private set; } = 1.0f;

        public float AttackPower => m_stats.CurrentAttackPower;
        public bool IsEvolved => m_stats.IsEvolved;
        public float AttackSpeed => m_stats.CurrentAttackSpeed > 0 ? m_stats.CurrentAttackSpeed : 1f;

        public BlackWaterLogic(WeaponRuntimeStats stats, BlackWaterTuningData? tuningData = null)
        {
            UpdateStats(stats, tuningData);
        }

        public void UpdateStats(WeaponRuntimeStats stats, BlackWaterTuningData? tuningData = null)
        {
            m_stats = stats;

            if (tuningData.HasValue)
            {
                var data = tuningData.Value;
                DamageTickInterval = data.DamageTickInterval;
                SlowAmount = data.SlowAmount;
                SlowDuration = data.SlowDuration;
            }
        }

        public float GetAdjustedTickDelay()
        {
            return DamageTickInterval / AttackSpeed;
        }
    }
}
