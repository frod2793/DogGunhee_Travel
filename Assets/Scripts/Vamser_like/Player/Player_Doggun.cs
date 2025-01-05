using UnityEngine;

namespace DogGuns_Games.vamsir
{


    public class Player_Doggun : Player_Base
    {
        void Init()
        {
            // 부모 클래스의 변수를 오버라이드하여 초기화
            AttackPower = 15f;
            CoolTime = 0.5f;
            AttackSpeed = 1.5f;
            WeaponSize = 1.2f;
            ProjectileCount = 3f;
            CriticalChance = 0.2f;
            CriticalDamage = 2f;
            Health = 150f;
            HealthRegen = 1.5f;
            Defense = 5f;
            MoveSpeed = 7f;
            ExpGain = 1.2f;
            GoldGain = 1.3f;
            ItemGainRange = 1.5f;
            Reroll = 2f;
        }

        public override void OnEnable()
        {
            base.OnEnable();
            Init();
        }

        public override void OnCollisionEnter2D(Collision2D other)
        {
            base.OnCollisionEnter2D(other);
        }

        public override void Player_attack(Vector3 attackAngle)
        {
            base.Player_attack( attackAngle);
            
            WeaphonBase.Weaphon_Attack(attackAngle);
         //   Debug.Log("Player_attack : " + AttackAngle);
        }

        public override void Player_Die()
        {
            base.Player_Die();
        }

        public override void Player_Hit()
        {
            base.Player_Hit();
        }

        public override void Player_Idle()
        {
            base.Player_Idle();
        }
    }
}