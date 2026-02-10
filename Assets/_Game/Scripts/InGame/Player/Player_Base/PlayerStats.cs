using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 전투 및 생존 관련 수치 데이터를 관리하는 직렬화 가능한 클래스입니다.
    /// <br/> 데이터의 무결성을 위해 주요 상태 값(체력 등)은 메서드를 통해서만 변경하도록 설계되었습니다.
    /// </summary>
    [Serializable]
    public class PlayerStats
    {
        #region 1. 설정 데이터 (Inspector Fields)

        [Header("1. 공격 능력치")]
        [Tooltip("기본 공격력")]
        [FormerlySerializedAs("AttackPower")] 
        [SerializeField] private float m_attackPower;

        [Tooltip("공격 쿨타임 (초)")]
        [FormerlySerializedAs("CoolTime")] 
        [SerializeField] private float m_coolTime;

        [Tooltip("공격 속도 배율 (기본 1.0)")]
        [FormerlySerializedAs("AttackSpeed")] 
        [SerializeField] private float m_attackSpeed;

        [Tooltip("무기 크기 배율 (기본 1.0)")]
        [FormerlySerializedAs("WeaponSize")] 
        [SerializeField] private float m_weaponSize;

        [Tooltip("발사체 개수")]
        [FormerlySerializedAs("ProjectileCount")] 
        [SerializeField] private float m_projectileCount;

        [Header("2. 생존 및 이동 능력치")]
        [Tooltip("최대 체력")]
        [FormerlySerializedAs("MaxHealth")] 
        [SerializeField] private float m_maxHealth;

        [Tooltip("현재 체력 (Inspector 디버깅용, 실제 수정은 메서드 권장)")]
        [FormerlySerializedAs("CurrentHealth")] 
        [SerializeField] private float m_currentHealth;

        [Tooltip("방어력 (데미지 감소율에 영향)")]
        [FormerlySerializedAs("Defense")] 
        [SerializeField] private float m_defense;

        [Tooltip("이동 속도")]
        [FormerlySerializedAs("MoveSpeed")] 
        [SerializeField] private float m_moveSpeed;

        #endregion

        #region 2. 공개 프로퍼티 (Properties)

        // --- 공격 관련 ---
        public float AttackPower { get => m_attackPower; set => m_attackPower = Mathf.Max(0, value); }
        public float CoolTime { get => m_coolTime; set => m_coolTime = Mathf.Max(0.01f, value); } // 0 나누기 방지
        public float AttackSpeed { get => m_attackSpeed; set => m_attackSpeed = Mathf.Max(0.1f, value); }
        public float WeaponSize { get => m_weaponSize; set => m_weaponSize = Mathf.Max(0.1f, value); }
        public float ProjectileCount { get => m_projectileCount; set => m_projectileCount = Mathf.Max(1, value); }

        // --- 생존 관련 ---
        public float MaxHealth 
        { 
            get => m_maxHealth; 
            set 
            {
                m_maxHealth = Mathf.Max(1, value);
                // 최대 체력이 변하면 현재 체력도 비율에 맞춰 조정하거나 클램핑 필요 (여기선 클램핑만 적용)
                if (m_currentHealth > m_maxHealth) m_currentHealth = m_maxHealth;
            } 
        }

        /// <summary>
        /// 현재 체력을 반환합니다. 값 설정은 ApplyDamage나 Heal 메서드를 사용하세요.
        /// </summary>
        public float CurrentHealth 
        { 
            get => m_currentHealth; 
            private set => m_currentHealth = Mathf.Clamp(value, 0, m_maxHealth); 
        }

        public float Defense { get => m_defense; set => m_defense = value; }
        public float MoveSpeed { get => m_moveSpeed; set => m_moveSpeed = Mathf.Max(0, value); }

        /// <summary>사망 여부 확인</summary>
        public bool IsDead => m_currentHealth <= 0;

        #endregion

        #region 3. 초기화 및 로직 (Logic)

        /// <summary>
        /// 캐릭터 생성 시 기본 스탯을 초기화합니다.
        /// </summary>
        public void Init(float maxHp, float speed, float attack)
        {
            m_maxHealth = maxHp;
            m_currentHealth = maxHp; // 체력 완충 상태로 시작
            m_moveSpeed = speed;
            m_attackPower = attack;
            
            // 기본값 설정
            m_coolTime = 1f;
            m_attackSpeed = 1f;
            m_weaponSize = 1f;
            m_projectileCount = 1f;
            m_defense = 0f;
        }

        /// <summary>
        /// 방어력을 적용하여 실제 피해를 입힙니다.
        /// <br/> 공식: $$ActualDamage = Damage \times \frac{100}{100 + Defense}$$
        /// </summary>
        /// <param name="rawDamage">방어력 적용 전 순수 데미지</param>
        public void ApplyDamage(float rawDamage)
        {
            if (IsDead || rawDamage <= 0) return;

            // 방어력 공식 적용 (음수 방어력 대비 분모 보정 필요 시 Mathf.Max 사용)
            float reductionFactor = 100f / (100f + Mathf.Max(0, m_defense));
            float actualDamage = rawDamage * reductionFactor;

            // 최소 데미지 1 보장
            actualDamage = Mathf.Max(1f, actualDamage);

            CurrentHealth -= actualDamage;
        }

        /// <summary>
        /// 체력을 회복시킵니다. (최대 체력 초과 불가)
        /// </summary>
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0) return;
            CurrentHealth += amount;
        }

        /// <summary>
        /// 체력을 퍼센트 단위로 회복시킵니다. (0.1 = 10%)
        /// </summary>
        public void HealPercent(float percentage)
        {
            float amount = m_maxHealth * Mathf.Clamp01(percentage);
            Heal(amount);
        }

        #endregion
    }
}