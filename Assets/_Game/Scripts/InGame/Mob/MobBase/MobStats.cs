using System;
using UnityEngine;

namespace InGame.Mob.MobBase
{
    /// <summary>
    /// 몬스터의 기본 전투 능력치(체력, 공격력, 속도 등)를 정의하는 데이터 구조체입니다.
    /// <br/> Unity Inspector 노출을 위해 [Serializable]로 선언되었습니다.
    /// </summary>
    [Serializable]
    public struct MobStats
    {
        #region 1. 설정 데이터 (Inspector)

        [Header("1. 생존 및 이동")]
        [Tooltip("현재 체력 (MaxHp와 동일하게 시작하거나 로직에 따라 변동)")]
        public float Hp;

        [Tooltip("초당 이동 속도")]
        public float MoveSpeed;
        
        [Header("2. 전투 능력치")]
        [Tooltip("기본 공격력")]
        public float AttackDamage;

        [Tooltip("초당 공격 횟수 (또는 쿨타임 계산용)")]
        public float AttackSpeed;

        [Tooltip("공격 사거리 (추적 중지 및 공격 시작 거리)")]
        public float AttackRange;

        [Tooltip("경직 저항력 (0: 저항 없음, 1: 완전 면역)")]
        [Range(0f, 1f)]
        public float StunResistance;

        #endregion

        #region 2. 생성자 및 초기화

        /// <summary>
        /// 모든 스탯을 초기화하는 생성자입니다.
        /// </summary>
        public MobStats(float hp, float speed, float damage, float atkSpeed, float range, float resistance)
        {
            Hp = hp;
            MoveSpeed = speed;
            AttackDamage = damage;
            AttackSpeed = atkSpeed;
            AttackRange = range;
            StunResistance = resistance;
        }

        /// <summary>
        /// 주요 스탯을 재설정합니다. (공격 속도와 사거리는 유지됩니다)
        /// </summary>
        public void Reset(float hp, float speed, float damage, float resistance)
        {
            Hp = hp;
            MoveSpeed = speed;
            AttackDamage = damage;
            StunResistance = resistance;
        }

        /// <summary>
        /// 모든 스탯 값을 덮어씁니다.
        /// </summary>
        public void SetAll(float hp, float speed, float damage, float atkSpeed, float range, float resistance)
        {
            Hp = hp;
            MoveSpeed = speed;
            AttackDamage = damage;
            AttackSpeed = atkSpeed;
            AttackRange = range;
            StunResistance = resistance;
        }

        #endregion
    }
}