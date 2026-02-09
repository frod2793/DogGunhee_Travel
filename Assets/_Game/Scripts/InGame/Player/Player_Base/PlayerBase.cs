using System;
using System.Collections.Generic;
using InGame.Manager;
using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 공통 데이터와 핵심 시스템(HP, 경험치, 무기 관리 등)을 관리하는 베이스 클래스입니다.
    /// </summary>
    public class PlayerBase : MonoBehaviour
    {
        #region 설정 데이터

        [Header("캐릭터 설정")]
        [SerializeField] private CharacterConfigSO m_config;
        
        [Header("데이터 및 시스템")]
        [SerializeField] private PlayerStats m_stats = new PlayerStats();

        #endregion

        #region 내부 상태 및 캐시

        private ExperienceSystem m_expSystem = new ExperienceSystem();
        private PlayerCollisionHandler m_collisionHandler;
        private PlayerWeaponManager m_weaponManager;

        #endregion

        #region 프로퍼티

        public float AttackPower { get => m_stats.AttackPower; set => m_stats.AttackPower = value; }
        public float CoolTime { get => m_stats.CoolTime; set => m_stats.CoolTime = value; }
        public float AttackSpeed { get => m_stats.AttackSpeed; set => m_stats.AttackSpeed = value; }
        public float WeaponSize { get => m_stats.WeaponSize; set => m_stats.WeaponSize = value; }
        public float ProjectileCount { get => m_stats.ProjectileCount; set => m_stats.ProjectileCount = value; }
        
        public float MaxHealth 
        { 
            get => m_stats.MaxHealth; 
            set 
            { 
                m_stats.MaxHealth = value; 
                OnHealthChanged?.Invoke(m_stats.CurrentHealth, m_stats.MaxHealth); 
            } 
        }
        
        public float CurrentHealth => m_stats.CurrentHealth;
        public float Defense { get => m_stats.Defense; set => m_stats.Defense = value; }
        public float MoveSpeed { get => m_stats.MoveSpeed; set => m_stats.MoveSpeed = value; }

        public float Level => m_expSystem.Level;
        public float CurrentExp => m_expSystem.CurrentExp;
        public float MaxExp => m_expSystem.MaxExp;
        
        public IReadOnlyList<IWeaponController> Weapons => m_weaponManager?.Controllers;

        #endregion

        #region 이벤트

        public static event Action<float> OnLevelUp;
        public static event Action<float, float> OnExpChanged;
        public event Action<float, float> OnHealthChanged;

        #endregion

        #region Unity 라이프사이클

        public virtual void OnEnable()
        {
            // Init이 명시적으로 호출되지 않았을 경우를 대비한 안전장치
            if (m_weaponManager == null || m_expSystem == null)
            {
                Init();
            }
        }

        protected virtual void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Update()
        {
            m_weaponManager?.OnUpdate();
        }

        private void LateUpdate()
        {
            m_weaponManager?.OnLateUpdate();
        }

        #endregion

        #region 초기화 및 의존성 주입

        /// <summary>
        /// 플레이어 시스템을 초기화합니다. 외부(GameManager 등)에서 의존성 주입이 가능합니다.
        /// </summary>
        public void Init(PlayerWeaponManager weaponManager = null, ExperienceSystem expSystem = null)
        {
            // 의존성 주입 처리
            m_weaponManager = weaponManager ?? new PlayerWeaponManager(transform);
            m_expSystem = expSystem ?? new ExperienceSystem();
            
            InitializeComponents();
            InitializeSystems();
            SubscribeEvents();
            
            // 초기 상태 알림
            OnLevelUp?.Invoke(Level);
            OnExpChanged?.Invoke(CurrentExp, MaxExp);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
            
            LogManager.Log("[PlayerBase] 초기화 완료 (DI 적용)", LogManager.LogCategory.PlayerBase);
        }

        private void InitializeComponents()
        {
            m_collisionHandler = GetComponent<PlayerCollisionHandler>();
            if (m_collisionHandler == null)
            {
                m_collisionHandler = gameObject.AddComponent<PlayerCollisionHandler>();
            }
            m_collisionHandler.Init(this);
        }

        private void InitializeSystems()
        {
            float maxHp = m_config != null ? m_config.BaseMaxHealth : 100f;
            float speed = m_config != null ? m_config.BaseMoveSpeed : 5f;
            float attack = m_config != null ? m_config.BaseAttackPower : 10f;

            m_stats.Init(maxHp, speed, attack);
            m_expSystem.Init();
        }

        #endregion

        #region 전투 및 데미지 로직

        /// <summary>
        /// 플레이어에게 데미지를 적용하고 사망 여부를 확인합니다.
        /// </summary>
        private void ApplyDamage(float damageAmount)
        {
            m_stats.ApplyDamage(damageAmount);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
            EffectManager.Instance?.PlayPlayerHitCameraShake();
            PlayHitEffect();
            
            if (m_stats.IsDead) Player_Die();
        }

        public virtual void Player_attack(Vector3 attackAngle) { }
        
        protected virtual void PlayHitEffect() 
        { 
            SoundKeys hitSound = m_config != null ? m_config.HitSoundKey : SoundKeys.playerHit;
            SoundManager.PlaySound(Sound.SFX, hitSound, false);
        }

        public virtual void Player_Die()
        {
            if (GameManager.Instance.State != null)
            {
                GameManager.Instance.State.PlayState = PlayStateManager.GameState.GameOver;
            }
            
            SoundKeys deathSound = m_config != null ? m_config.DeathSoundKey : SoundKeys.PlayerDeth;
            SoundManager.PlaySound(Sound.SFX, deathSound, false);
        }

        #endregion

        #region 무기 제어 (위임)

        public void AddController(IWeaponController weapon) => m_weaponManager?.AddController(weapon);
        public void RemoveWeapon(string skillCode) => m_weaponManager?.RemoveWeapon(skillCode);
        public void SetTargetProvider(Func<Vector3> provider) => m_weaponManager?.SetTargetProvider(provider);
        public void EquipWeapon(WeaponDataSO data) => m_weaponManager?.EquipWeapon(data);

        #endregion

        #region 이벤트 핸들러

        private void SubscribeEvents()
        {
            if (GameManager.Instance.State == null) return;
            GameManager.Instance.State.OnGameOver += OnGameOver;
            GameManager.Instance.State.OnGamePause += OnGamePause;
            GameManager.Instance.State.OnGameResume += OnGameResume;
            
            m_expSystem.OnLevelUp += HandleLevelUp;
            m_expSystem.OnExpChanged += (cur, max) => OnExpChanged?.Invoke(cur, max);
            
            if (m_collisionHandler != null)
            {
                m_collisionHandler.OnDamageReceived += ApplyDamage;
                m_collisionHandler.OnExpCollected += m_expSystem.AddExperience;
                m_collisionHandler.OnCoinCollected += HandleCoinCollected;
            }
        }

        private void UnsubscribeEvents()
        {
            if (GameManager.Instance.State == null) return;
            GameManager.Instance.State.OnGameOver -= OnGameOver;
            GameManager.Instance.State.OnGamePause -= OnGamePause;
            GameManager.Instance.State.OnGameResume -= OnGameResume;
        }

        private void OnGameResume() => m_collisionHandler?.SetColliderActive(true);
        private void OnGamePause() => m_collisionHandler?.SetColliderActive(false);
        private void OnGameOver() => m_collisionHandler?.SetColliderActive(false);

        private void HandleLevelUp(int level)
        {
            OnLevelUp?.Invoke(level);
            SoundManager.PlaySound(Sound.SFX, SoundKeys.Levelup, false);
        }
        
        private void HandleCoinCollected(int coinValue)
        {
            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.PlayerData.ingameCoin += coinValue;
            }
        }

        #endregion

        #region 유틸리티

        public float GetExpProgress() => m_expSystem.GetProgress();

        #endregion
    }
}