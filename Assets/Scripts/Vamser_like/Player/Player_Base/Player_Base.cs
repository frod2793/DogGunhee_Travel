
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace DogGuns_Games.vamsir
{
    public class Player_Base : MonoBehaviour
    {
        public float AttackPower { get; set; }
        public float CoolTime{ get; set; }
        public float AttackSpeed { get; set; }
        public float WeaponSize { get; set; }
        public float ProjectileCount{ get; set; }
        public float CriticalChance { get; set; }
        public float CriticalDamage { get; set; }
        public float Health { get; set; }
        public float HealthRegen { get; set; }
        public float Defense  { get; set; }
        public float MoveSpeed { get; set; }
        public float ExpGain { get; set; }
        public float GoldGain { get; set; }
        public float ItemGainRange{ get; set; }
        public float Reroll { get; set; }

        public float Level { get; set; }
        public Vector3 AttackAngle { get; set; }

        public int characterIndex; //현재 캐릭터 인덱스
        
        public bool ishit = false;
        public Weaphon_base WeaphonBase { get; set; }

        public enum playerState
        {
            Idle,
            Move,
            Hit,
            Attack
        }
        private playerState _playState;

        public playerState PlayState
        {
            get => _playState;
            set
            {
                _playState = value;
                setPlayerState(_playState);
            }
        }

        public virtual void OnEnable()
        {
            WeaphonBase = FindFirstObjectByType<Weaphon_base>();
            WeaphonBase.transform.SetParent(transform);
            WeaphonBase.transform.localPosition = Vector3.zero;
        }

        public virtual void OnCollisionStay2D(Collision2D other)
        {
            if (other.gameObject.CompareTag("Mob"))
            {
                if (!ishit)
                {
                    ishit = true;
                    DelayAction(1f, () => ishit = false);
                    PlayState = playerState.Hit;
                }
            }
            
            if (other.gameObject.CompareTag("Exp"))
            {
               Debug.Log("Exp");
               
               EXP_Obj expObj = other.gameObject.GetComponent<EXP_Obj>();
               
               expObj.objectPoolSpawner.ExpObjectPool.Release(expObj);
            }
            
            if (other.gameObject.CompareTag("Coin"))
            {
                Debug.Log("Coin");
                
                Coin_Obj coinObj = other.gameObject.GetComponent<Coin_Obj>();

                PlayerDataManagerDontdesytoy.Instance.scritpableobjPlayerData.currency1 += 1;

                
                coinObj.objectPoolSpawner.CoinObjectPool.Release(coinObj);
            }
            
        }
        public void setPlayerState(playerState state)
        {
            switch (state)
            {
                case playerState.Idle:
                    Player_Idle();
                    break;
                case playerState.Move:
                    PlayerMovement();
                    break;
                case playerState.Hit:
                    Player_Hit();
                    break;
                case playerState.Attack:
                    Player_attack(AttackAngle);
                    break;
            }
        }

        public virtual void Player_attack( Vector3 attackAngle)
        {
            attackAngle = this.AttackAngle;
        }
        public virtual void Player_Die()
        {
        }

        public virtual void Player_Hit()
        {
            
        }

        public virtual void Player_Idle()
        {
        }
        
        public virtual void PlayerMovement()
        {
        }
        
        public UniTask DelayAction(float delay, Action action)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(delay)).ContinueWith(() => action());
        }

       
      
    }
}