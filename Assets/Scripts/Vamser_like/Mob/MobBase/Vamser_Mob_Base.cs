using System;
using UnityEngine;
namespace DogGuns_Games.vamsir
{
    public class Vamser_Mob_Base : MonoBehaviour
    {
        public ObjectPool_Spawner objectPool_Spawner;

        
        public float Mob_Speed { get; set; }
        protected float Mob_Hp { get; set; }
        public float Mob_AttackDamage { get; set; }
        public float Mob_AttackSpeed { get; set; }
        public float Mob_AttackRange { get; set; }
        public float Mob_StunTime { get; set; }
        protected bool  Mob_IsDie { get; set; }
        protected bool  Mob_IsHit { get; set; }
        
        
        public bool ismove;
        public enum MobState
        {
            Idle,
            Move,
            Stun,
            Attack,
            Die
        }

        [SerializeField] private MobState mobState;

        public virtual void OnEnable()
        {
            objectPool_Spawner = FindFirstObjectByType<ObjectPool_Spawner>();

            Mob_IsDie = false;
            
            Play_State.OnGamePause += Pause;
            Play_State.OnGameResume += Resume;
        }

        
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                // 플레이 모드에서만 SetMobState 호출
                SetMobState(mobState);
            }
        }

        private void Pause()
        {
            ismove = false;
        }
        
        private void Resume()
        {
            ismove = true;
        }
        
        public void SetMobState(MobState state)
        {
            switch (state)
            {
                case MobState.Idle: Mob_Idle(); 
                    break;
                case MobState.Move: Mob_Move(); 
                    break;
                case MobState.Stun: Mob_Stun(); 
                    break;
                case MobState.Attack: Mob_Attack(); 
                    break;
                case MobState.Die: Mob_Die();
                    break;
            }
        }

        protected virtual void Mob_Idle()
        {
        }

        protected virtual void Mob_Move()
        {
            
        }

        protected virtual void Mob_Stun()
        {
            
        }

        protected virtual void Mob_hit()
        {
            
        }
        
        protected virtual void Mob_Attack()
        {
        }

        protected virtual void Mob_Die()
        {
            if (!Mob_IsDie)
            {
                Mob_IsDie = true;
                objectPool_Spawner.MobObjectPool.Release(this);
                Debug.Log("Die : " + name);
            }
        }
    }
}