using System;
using UnityEngine;

namespace InGame.Mob.MobBase
{
    /// <summary>
    /// 몬스터의 주요 전투 능력치를 정의하는 구조체입니다.
    /// </summary>
    [Serializable]
    public struct MobStats
    {
        #region 설정 데이터

        [Header("기본 스탯")]
        [Tooltip("현재 체력")]
        public float Hp;

        [Tooltip("이동 속도")]
        public float MoveSpeed;

        [Tooltip("공격력")]
        public float AttackDamage;

        [Tooltip("공격 속도")]
        public float AttackSpeed;

        [Tooltip("공격 사거리")]
        public float AttackRange;

        [Tooltip("피격 시 경직 시간")]
        public float StunTime;

        #endregion

        #region 생성자 및 유틸리티

        public MobStats(float hp, float speed, float damage, float atkSpeed, float range, float stun)
        {
            Hp = hp;
            MoveSpeed = speed;
            AttackDamage = damage;
            AttackSpeed = atkSpeed;
            AttackRange = range;
            StunTime = stun;
        }

        /// <summary>
        /// 스탯 데이터를 초기값으로 재설정합니다.
        /// </summary>
        public void Reset(float hp, float speed, float damage, float stun)
        {
            Hp = hp;
            MoveSpeed = speed;
            AttackDamage = damage;
            StunTime = stun;
        }

        #endregion
    }
}
