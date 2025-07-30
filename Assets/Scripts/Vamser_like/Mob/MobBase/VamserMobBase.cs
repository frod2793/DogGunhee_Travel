using UnityEngine;
namespace DogGuns_Games.vamsir
{
    public class VamserMobBase : MonoBehaviour, IObjectPoolSpawnerSettable
    {
        public ObjectPoolSpawner objectPoolSpawner { get; set; }

        
        public float Mob_Speed { get; set; }
        protected float Mob_Hp { get; set; }
        public float Mob_AttackDamage { get; set; }
        public float Mob_AttackSpeed { get; set; }
        public float Mob_AttackRange { get; set; }
        public float Mob_StunTime { get; set; }
        protected bool  Mob_IsDie { get; set; }
        protected bool  Mob_IsHit { get; set; }
        
        protected PlayerBase player; // 플레이어 참조를 저장할 필드

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
            // objectPoolSpawner는 풀에서 생성될 때 외부에서 할당해 주므로 Find는 불필요합니다.
            Mob_IsDie = false;
            PlayStateManager.OnGamePause += Pause;
            PlayStateManager.OnGameResume += Resume;
            PlayStateManager.OnGameOver += OnGameOver;
        }

        private void OnDisable()
        {
            PlayStateManager.OnGamePause -= Pause;
            PlayStateManager.OnGameResume -= Resume;
            PlayStateManager.OnGameOver -= OnGameOver;
        }

        private void OnGameOver()
        {
            ismove = false;
        }
        
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                // 플레이 모드에서만 SetMobState 호출
                SetMobState(mobState);
            }
        }
        /// <summary>
        /// 외부에서 플레이어(타겟)를 설정합니다.
        /// </summary>
        public virtual void SetTarget(PlayerBase target)
        {
            player = target;
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
                objectPoolSpawner.MobObjectPool.Release(this);
                PlayerDataManagerDontdesytoy.Instance.scritpableobjPlayerData.nowPlayMObkillCOunt++;
                Debug.Log("Die : " + name);
            }
        }
    }
}