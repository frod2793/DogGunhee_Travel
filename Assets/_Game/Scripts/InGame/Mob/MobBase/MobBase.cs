using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using InGame.Manager;
using InGame.Player.Player_Base;
using InGame;

namespace InGame.Mob.MobBase
{
    
    public abstract class MobBase : MonoBehaviour, IObjectPoolUser
    {
        #region 프로퍼티 (공통 스탯)

        public ObjectPoolSpawner ObjectPoolSpawner { get; set; }
        public float MoveSpeed { get; set; }
        public float CurrentHp { get; protected set; }
        public float AttackDamage { get; set; }
        public float AttackSpeed { get; set; }
        public float AttackRange { get; set; }
        public float StunTime { get; set; }
        public bool IsDead { get; protected set; }
        public bool IsHit { get; protected set; }
        public bool IsMoveEnabled { get; protected set; }

        #endregion

        #region 내부 참조 및 상태

        protected PlayerBase m_player; 
        protected Transform m_playerTransform;

        public enum MobState { Idle, Move, Stun, Attack, Die }
        [SerializeField] protected MobState m_currentState;
        public MobState CurrentState => m_currentState;

        private static float s_lastHitSoundTime;
        private const float k_HitSoundCooldown = 0.1f;

        // [추가] 지속 데미지(DoT) 관리용
        private CancellationTokenSource m_dotCts;

        #endregion

        #region Unity 라이프사이클

        public virtual void OnEnable()
        {
            IsDead = false;
            IsHit = false;
            IsMoveEnabled = true;
            m_dotCts?.Cancel(); // 재사용 시 이전 DoT 취소
            PlayStateManager.OnGamePause += OnPause;
            PlayStateManager.OnGameResume += OnResume;
            PlayStateManager.OnGameOver += OnGameOver;
        }

        protected virtual void OnDisable()
        {
            m_dotCts?.Cancel(); // 비활성화 시 DoT 중지
            PlayStateManager.OnGamePause -= OnPause;
            PlayStateManager.OnGameResume -= OnResume;
            PlayStateManager.OnGameOver -= OnGameOver;
        }

        #endregion

        #region 초기화 및 타겟 설정

        public virtual void SetTarget(PlayerBase target)
        {
            m_player = target;
            if (m_player != null)
            {
                m_playerTransform = m_player.transform.parent;
            }
        }

        #endregion

        #region 상태 관리 (FSM)

        public void SetState(MobState state)
        {
            m_currentState = state;
            switch (state)
            {
                case MobState.Idle: OnIdle(); break;
                case MobState.Move: OnMove(); break;
                case MobState.Stun: OnStun(); break;
                case MobState.Attack: OnAttack(); break;
                case MobState.Die: OnDie(); break;
            }
        }

        protected virtual void OnIdle() { }
        protected virtual void OnMove() { IsMoveEnabled = true; }
        protected virtual void OnStun() { IsMoveEnabled = false; }
        protected virtual void OnAttack() { }

        protected virtual void OnDie()
        {
            if (!IsDead)
            {
                IsDead = true;
                IsMoveEnabled = false;
                m_dotCts?.Cancel(); // 사망 시 DoT 중지
                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.PlayerData.nowPlayMObkillCOunt++;
                }
                if (ObjectPoolSpawner != null)
                {
                    ObjectPoolSpawner.MobObjectPool.Release(this);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }

        #endregion

        #region 게임 상태 이벤트 핸들러

        protected virtual void OnPause() => IsMoveEnabled = false;
        
        protected virtual void OnResume()
        {
            if (!IsDead && m_currentState != MobState.Stun)
            {
                IsMoveEnabled = true;
            }
        }

        private void OnGameOver() => IsMoveEnabled = false;

        #endregion

        #region 전투 인터페이스

        public virtual void TakeDamage(float damage, float stunTime = 0f) { }

        /// <summary>
        /// 지속 피해(DoT)를 적용합니다. 새로운 DoT가 적용되면 이전 DoT는 취소됩니다.
        /// </summary>
        public void ApplyDamageOverTime(float totalDamage, float duration, int tickCount)
        {
            if (IsDead || tickCount <= 0 || duration <= 0) return;

            m_dotCts?.Cancel();
            m_dotCts = new CancellationTokenSource();
            
            float damagePerTick = totalDamage / tickCount;
            float interval = duration / tickCount;

            DamageOverTimeLoopAsync(damagePerTick, interval, tickCount, m_dotCts.Token).Forget();
        }

        private async UniTaskVoid DamageOverTimeLoopAsync(float damage, float interval, int ticks, CancellationToken token)
        {
            for (int i = 0; i < ticks; i++)
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(interval), cancellationToken: token);
                if (IsDead) break;
                
                TakeDotDamage(damage);
            }
        }

        /// <summary>
        /// 지속 피해 전용 데미지 처리 (피격음, 스턴, 무적 시간 없음)
        /// </summary>
        protected virtual void TakeDotDamage(float damage)
        {
            if (IsDead) return;
            CurrentHp -= damage;
            // TODO: DoT 데미지 텍스트 표시 로직 (필요 시)
            if (CurrentHp <= 0)
            {
                OnDie();
            }
        }

        public virtual void ApplySlow(float slowMultiplier, float duration) { }

        protected bool CanPlayHitSound()
        {
            if (Time.time >= s_lastHitSoundTime + k_HitSoundCooldown)
            {
                s_lastHitSoundTime = Time.time;
                return true;
            }
            return false;
        }

        #endregion
    }
}