using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 모든 능력치(공격, 방어, 이동 등) 데이터를 관리하는 POCO 클래스입니다.
    /// </summary>
    [Serializable]
    public class PlayerStats
    {
        #region 설정 데이터

        [Header("공격 능력치")]
        [FormerlySerializedAs("AttackPower")] [SerializeField] private float m_attackPower;
        [FormerlySerializedAs("CoolTime")] [SerializeField] private float m_coolTime;
        [FormerlySerializedAs("AttackSpeed")] [SerializeField] private float m_attackSpeed;
        [FormerlySerializedAs("WeaponSize")] [SerializeField] private float m_weaponSize;
        [FormerlySerializedAs("ProjectileCount")] [SerializeField] private float m_projectileCount;

        [Header("방어 및 이동 능력치")]
        [FormerlySerializedAs("MaxHealth")] [SerializeField] private float m_maxHealth;
        [FormerlySerializedAs("CurrentHealth")] [SerializeField] private float m_currentHealth;
        [FormerlySerializedAs("Defense")] [SerializeField] private float m_defense;
        [FormerlySerializedAs("MoveSpeed")] [SerializeField] private float m_moveSpeed;

        #endregion

        #region 프로퍼티

        public float AttackPower { get => m_attackPower; set => m_attackPower = value; }
        public float CoolTime { get => m_coolTime; set => m_coolTime = value; }
        public float AttackSpeed { get => m_attackSpeed; set => m_attackSpeed = value; }
        public float WeaponSize { get => m_weaponSize; set => m_weaponSize = value; }
        public float ProjectileCount { get => m_projectileCount; set => m_projectileCount = value; }

        public float MaxHealth { get => m_maxHealth; set => m_maxHealth = value; }
        public float CurrentHealth { get => m_currentHealth; set => m_currentHealth = value; }
        public float Defense { get => m_defense; set => m_defense = value; }
        public float MoveSpeed { get => m_moveSpeed; set => m_moveSpeed = value; }

        public bool IsDead => m_currentHealth <= 0;

        #endregion

        #region 초기화 및 제어 로직

        /// <summary>
        /// 기초 능력치를 설정하여 초기화합니다.
        /// </summary>
        public void Init(float maxHp, float speed, float attack)
        {
            m_maxHealth = maxHp;
            m_currentHealth = maxHp;
            m_moveSpeed = speed;
            m_attackPower = attack;
            
            // 시스템 기본값 설정
            m_coolTime = 1f;
            m_attackSpeed = 1f;
            m_weaponSize = 1f;
            m_projectileCount = 1f;
            m_defense = 0f;
        }

        /// <summary>
        /// 데미지를 적용합니다. 방어력 공식을 사용하여 실제 피해량을 계산합니다.
        /// </summary>
        public void ApplyDamage(float damage)
        {
            float actualDamage = Math.Max(1, damage * (100 / (100 + m_defense)));
            m_currentHealth -= actualDamage;
        }

        /// <summary>
        /// 체력을 회복시킵니다. 최대 체력을 초과할 수 없습니다.
        /// </summary>
        public void Heal(float amount)
        {
            m_currentHealth = Math.Min(m_maxHealth, m_currentHealth + amount);
        }

        #endregion
    }
}
