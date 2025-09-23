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
        public bool IsDead { get; protected set; }
        public bool IsHit { get; protected set; }
        
        protected PlayerBase player; // 플레이어 참조를 저장할 필드
        protected Transform playerTransform; // 실제 움직이는 플레이어 부모 객체의 Transform

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
            IsDead = false;
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
            if (player != null)
            {
                // 플레이어 캐릭터의 부모 객체가 실제 움직임을 담당하므로, 부모의 Transform을 추적 대상으로 설정합니다.
                playerTransform = player.transform.parent;
            }
            else
            {
                playerTransform = null;
            }
        }
        protected virtual void Pause()
        {
            ismove = false;
        }
        
        protected virtual void Resume()
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
            if (!IsDead)
            {
                IsDead = true;
                objectPoolSpawner.MobObjectPool.Release(this);
                PlayerDataManagerDontdesytoy.Instance.scritpableobjPlayerData.nowPlayMObkillCOunt++;
                LogManager.Log("Die : " + name, LogManager.LogCategory.mobBase);
            }
        }

        /// <summary>
        /// 외부(틱 데미지 등)에서 몹에게 데미지를 입히는 공용 메서드입니다.
        /// </summary>
        /// <param name="damage">입힐 데미지 양</param>
        public virtual void TakeDamage(float damage)
        {
            // 하위 클래스에서 구체적인 로직을 구현합니다.
        }

        /// <summary>
        /// 몹에게 슬로우 효과를 적용하는 공용 메서드입니다.
        /// </summary>
        /// <param name="slowMultiplier">속도 감소 배율 (0.0 ~ 1.0). 0.3은 30% 감소.</param>
        /// <param name="duration">슬로우 지속 시간(초).</param>
        public virtual void ApplySlow(float slowMultiplier, float duration)
        {
            // 하위 클래스에서 구체적인 로직을 구현합니다.
        }
    }
}