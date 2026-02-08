using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Logic
{
    /// <summary>
    /// 부메랑(Boomerang) 무기의 에디터 조정 데이터를 담는 구조체입니다.
    /// </summary>
    public struct BoomerangWeaponTuningData
    {
        public float StartAngle;
        public float AngleStep;
        public int BurstDelayMs;
    }

    /// <summary>
    /// 부메랑 무기의 비즈니스 로직을 담당하는 POCO 클래스입니다.
    /// </summary>
    public class BoomerangWeaponLogic
    {
        private WeaponRuntimeStats m_stats;
        
        public float StartAngle { get; private set; } = -15f;
        public float AngleStep { get; private set; } = 30f;
        public int BurstDelayMs { get; private set; } = 50;

        public float AttackPower => m_stats.CurrentAttackPower;
        public float StunTime => m_stats.MobStunTime;
        public float Speed => m_stats.CurrentAttackSpeed > 0 ? m_stats.CurrentAttackSpeed : 1f;
        public float Range => m_stats.CurrentAttackRange;

        public int MaxProjectiles => Mathf.Max(1, m_stats.CurrentProjectileCount);
        public int BurstCount => m_stats.IsEvolved ? 3 : 1; // 기본 1개, 진화 시 3개 (예시)

        public BoomerangWeaponLogic(WeaponRuntimeStats stats, BoomerangWeaponTuningData? tuningData = null)
        {
            UpdateStats(stats, tuningData);
        }

        public void UpdateStats(WeaponRuntimeStats stats, BoomerangWeaponTuningData? tuningData = null)
        {
            m_stats = stats;

            if (tuningData.HasValue)
            {
                var data = tuningData.Value;
                StartAngle = data.StartAngle;
                AngleStep = data.AngleStep;
                BurstDelayMs = data.BurstDelayMs;
            }
        }

        public float CalculateAngle(int index, int totalCount, float baseAngle)
        {
            float startAngleOffset = StartAngle * (totalCount - 1);
            float step = (totalCount > 1) ? AngleStep : 0f;
            return baseAngle + startAngleOffset + (step * index);
        }
    }
}
