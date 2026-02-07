using UnityEngine;

namespace InGame.Weapon.Base
{
    /// <summary>
    /// 무기의 런타임 스탯(현재 레벨, 계산된 데미지 등)을 관리하는 POCO 클래스입니다.
    /// </summary>
    public class WeaponRuntimeStats
    {
        public WeaponDataSO Data { get; private set; }

        public int CurrentLevel { get; private set; } = 1;
        public float AttackPower { get; set; }
        public float CoolTime { get; set; }
        public float AttackSpeed { get; set; }
        public float AttackRange { get; set; }
        public float Duration { get; set; }
        public int ProjectileCount { get; set; }
        public float MobStunTime { get; set; }
        public bool IsEvolved { get; set; }

        // 하위 호환성을 위한 별칭 (필요 시)
        public float CurrentAttackPower => AttackPower;
        public float CurrentCoolTime => CoolTime;
        public float CurrentAttackSpeed => AttackSpeed;
        public int CurrentProjectileCount => ProjectileCount;
        public float CurrentDuration => Duration;

        public WeaponRuntimeStats(WeaponDataSO data)
        {
            Data = data;
            ResetStats();
        }

        public void ResetStats()
        {
            if (Data == null) return;

            CurrentLevel = 1;
            AttackPower = Data.BaseAttackPower;
            CoolTime = Data.BaseCoolTime;
            AttackSpeed = Data.BaseAttackSpeed;
            AttackRange = Data.BaseAttackRange;
            Duration = Data.BaseDuration;
            ProjectileCount = Data.BaseProjectileCount;
            MobStunTime = 0.5f; // 기본값
            IsEvolved = false;
        }

        public void LevelUp(int newLevel)
        {
            CurrentLevel = newLevel;
            if (CurrentLevel >= 6) IsEvolved = true;
            // TODO: WeaponDataSO의 Upgrades 리스트를 참조하여 스탯 재계산 로직 추가 필요
        }
    }
}
