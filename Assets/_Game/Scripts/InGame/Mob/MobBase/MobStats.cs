using System;
using UnityEngine;

namespace InGame.Mob.MobBase
{
    [Serializable]
    public struct MobStats
    {
        [Header("기본 스탯")]
        [Tooltip("체력")]
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

        public MobStats(float hp, float speed, float damage, float atkSpeed, float range, float stun)
        {
            Hp = hp;
            MoveSpeed = speed;
            AttackDamage = damage;
            AttackSpeed = atkSpeed;
            AttackRange = range;
            StunTime = stun;
        }

        public void Reset(float hp, float speed, float damage, float stun)
        {
            Hp = hp;
            MoveSpeed = speed;
            AttackDamage = damage;
            StunTime = stun;
            // 필요한 경우 다른 스탯도 리셋
        }
    }
}
