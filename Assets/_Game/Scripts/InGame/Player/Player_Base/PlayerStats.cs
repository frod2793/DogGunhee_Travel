using System;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 모든 스텟 데이터를 관리하는 POCO 클래스입니다.
    /// </summary>
    [Serializable]
    public class PlayerStats
    {
        #region 공격 스텟
        public float AttackPower;
        public float CoolTime;
        public float AttackSpeed;
        public float WeaponSize;
        public float ProjectileCount;
        #endregion

        #region 방어 및 이동 스텟
        public float MaxHealth;
        public float CurrentHealth;
        public float Defense;
        public float MoveSpeed;
        #endregion

        #region 로직
        public void Initialize(float maxHp, float speed, float attack)
        {
            MaxHealth = maxHp;
            CurrentHealth = maxHp;
            MoveSpeed = speed;
            AttackPower = attack;
            
            // 기본값 설정
            CoolTime = 1f;
            AttackSpeed = 1f;
            WeaponSize = 1f;
            ProjectileCount = 1f;
            Defense = 0f;
        }

        public void ApplyDamage(float damage)
        {
            float actualDamage = Math.Max(1, damage * (100 / (100 + Defense)));
            CurrentHealth -= actualDamage;
        }

        public bool IsDead => CurrentHealth <= 0;

        public void Heal(float amount)
        {
            CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
        }
        #endregion
    }
}
