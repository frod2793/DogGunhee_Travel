using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// [설명]: 플레이어의 전투 및 생존 관련 수치 데이터를 관리하는 직렬화 가능한 클래스입니다.
    /// 데이터의 무결성을 위해 주요 상태 값(체력 등)은 메서드를 통해서만 변경하도록 설계되었습니다.
    /// </summary>
    [Serializable]
    public class PlayerStats
    {
        #region 에디터 설정

        [Header("1. 공격 능력치")]
        /// <summary> [설명]: 기본 공격력 </summary>
        [Tooltip("기본 공격력")]
        [FormerlySerializedAs("AttackPower")]
        [SerializeField]
        private float m_attackPower;

        /// <summary> [설명]: 공격 쿨타임 (초) </summary>
        [Tooltip("공격 쿨타임 (초)")]
        [FormerlySerializedAs("CoolTime")]
        [SerializeField]
        private float m_coolTime;

        /// <summary> [설명]: 공격 속도 배율 (기본 1.0) </summary>
        [Tooltip("공격 속도 배율 (기본 1.0)")]
        [FormerlySerializedAs("AttackSpeed")]
        [SerializeField]
        private float m_attackSpeed;

        /// <summary> [설명]: 무기 크기 배율 (기본 1.0) </summary>
        [Tooltip("무기 크기 배율 (기본 1.0)")]
        [FormerlySerializedAs("WeaponSize")]
        [SerializeField]
        private float m_weaponSize;

        /// <summary> [설명]: 발사체 개수 </summary>
        [Tooltip("발사체 개수")]
        [FormerlySerializedAs("ProjectileCount")]
        [SerializeField]
        private float m_projectileCount;

        [Header("2. 생존 및 이동 능력치")]
        /// <summary> [설명]: 최대 체력 </summary>
        [Tooltip("최대 체력")]
        [FormerlySerializedAs("MaxHealth")]
        [SerializeField]
        private float m_maxHealth;

        /// <summary> [설명]: 현재 체력 (인스펙터 디버깅용) </summary>
        [Tooltip("현재 체력 (인스펙터 디버깅용)")]
        [FormerlySerializedAs("CurrentHealth")]
        [SerializeField]
        private float m_currentHealth;

        /// <summary> [설명]: 방어력 (데미지 감소율에 영향) </summary>
        [Tooltip("방어력 (데미지 감소율에 영향)")]
        [FormerlySerializedAs("Defense")]
        [SerializeField]
        private float m_defense;

        /// <summary> [설명]: 이동 속도 </summary>
        [Tooltip("이동 속도")]
        [FormerlySerializedAs("MoveSpeed")]
        [SerializeField]
        private float m_moveSpeed;

        #endregion

        #region 공개 프로퍼티

        #region 공격 관련

        /// <summary> [설명]: 공격력 프로퍼티 </summary>
        public float AttackPower
        {
            get => m_attackPower;
            set => m_attackPower = Mathf.Max(0, value);
        }

        /// <summary> [설명]: 공격 쿨타임 프로퍼티 (0 나누기 방지) </summary>
        public float CoolTime
        {
            get => m_coolTime;
            set => m_coolTime = Mathf.Max(0.01f, value);
        }

        /// <summary> [설명]: 공격 속도 프로퍼티 </summary>
        public float AttackSpeed
        {
            get => m_attackSpeed;
            set => m_attackSpeed = Mathf.Max(0.1f, value);
        }

        /// <summary> [설명]: 무기 크기 프로퍼티 </summary>
        public float WeaponSize
        {
            get => m_weaponSize;
            set => m_weaponSize = Mathf.Max(0.1f, value);
        }

        /// <summary> [설명]: 발사체 개수 프로퍼티 </summary>
        public float ProjectileCount
        {
            get => m_projectileCount;
            set => m_projectileCount = Mathf.Max(1, value);
        }

        #endregion

        #region 생존 및 이동 관련

        /// <summary> [설명]: 최대 체력 프로퍼티 </summary>
        public float MaxHealth
        {
            get => m_maxHealth;
            set
            {
                m_maxHealth = Mathf.Max(1, value);
                if (m_currentHealth > m_maxHealth)
                {
                    m_currentHealth = m_maxHealth;
                }
            }
        }

        /// <summary>
        /// [설명]: 현재 체력 프로퍼티입니다. 값 설정은 ApplyDamage나 Heal 메서드를 사용하세요.
        /// </summary>
        public float CurrentHealth
        {
            get => m_currentHealth;
            private set => m_currentHealth = Mathf.Clamp(value, 0, m_maxHealth);
        }

        /// <summary> [설명]: 방어력 프로퍼티 </summary>
        public float Defense
        {
            get => m_defense;
            set => m_defense = value;
        }

        /// <summary> [설명]: 이동 속도 프로퍼티 </summary>
        public float MoveSpeed
        {
            get => m_moveSpeed;
            set => m_moveSpeed = Mathf.Max(0, value);
        }

        /// <summary> [설명]: 사망 여부 확인 프로퍼티 </summary>
        public bool IsDead => m_currentHealth <= 0;

        #endregion

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 캐릭터 생성 시 기본 스탯을 초기 데이터로 설정합니다.
        /// </summary>
        public void Init(float maxHp, float speed, float attack)
        {
            m_maxHealth = maxHp;
            m_currentHealth = maxHp;
            m_moveSpeed = speed;
            m_attackPower = attack;

            // 기본값 설정
            m_coolTime = 1f;
            m_attackSpeed = 1f;
            m_weaponSize = 1f;
            m_projectileCount = 1f;
            m_defense = 0f;
        }

        #endregion

        #region 핵심 전투 로직

        /// <summary>
        /// [설명]: 방어력을 적용하여 실제 피해를 입힙니다.
        /// 공식: ActualDamage = Damage * (100 / (100 + Defense))
        /// </summary>
        /// <param name="rawDamage">방어력 적용 전 원시 데미지</param>
        public void ApplyDamage(float rawDamage)
        {
            if (IsDead || rawDamage <= 0)
            {
                return;
            }

            // 방어력 공식 적용
            float reductionFactor = 100f / (100f + Mathf.Max(0, m_defense));
            float actualDamage = rawDamage * reductionFactor;

            // 최소 데미지 1 보장
            actualDamage = Mathf.Max(1f, actualDamage);

            CurrentHealth -= actualDamage;
        }

        /// <summary>
        /// [설명]: 지정된 양만큼 체력을 회복시킵니다.
        /// </summary>
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0)
            {
                return;
            }
            CurrentHealth += amount;
        }

        /// <summary>
        /// [설명]: 체력을 최대 체력 대비 퍼센트 단위로 회복시킵니다. (예: 0.1 = 10%)
        /// </summary>
        public void HealPercent(float percentage)
        {
            float amount = m_maxHealth * Mathf.Clamp01(percentage);
            Heal(amount);
        }

        #endregion
    }
}