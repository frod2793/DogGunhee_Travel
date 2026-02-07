using System;
using System.Collections.Generic;
using InGame.Manager;
using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 공통 기능을 담당하는 베이스 클래스입니다.
    /// </summary>
    public class PlayerBase : MonoBehaviour
    {
        #region 구성 요소
        [Header("캐릭터 설정")]
        [SerializeField] private CharacterConfigSO m_config;
        
        [Header("데이터 설정")]
        [SerializeField] private PlayerStats m_stats = new PlayerStats();
        
        private ExperienceSystem m_expSystem = new ExperienceSystem();
        private PlayerCollisionHandler m_collisionHandler;
        private PlayerWeaponManager m_weaponManager;
        #endregion

        #region 프로퍼티 (External Access)
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
        
        public IReadOnlyList<WeaponBase> Weapons => m_weaponManager?.Weapons;
        #endregion

        #region 정적 및 인스턴스 이벤트
        public static event Action<float> OnLevelUp;
        public static event Action<float, float> OnExpChanged;
        public event Action<float, float> OnHealthChanged;
        #endregion

        #region 초기화
        /// <summary>
        /// [\ub9ac\ud329\ud1a0\ub9c1] OnEnable\uc5d0\uc11c\ub294 Initialize\uac00 \ud638\ucd9c\ub418\uc9c0 \uc54a\uc558\uc744 \uacbd\uc6b0\ub97c \ub300\ube44\ud55c \ud3f4\ubc31 \ub85c\uc9c1\uc744 \uc218\ud589\ud569\ub2c8\ub2e4.
        /// \uc678\ubd80\uc5d0\uc11c \uba85\uc2dc\uc801\uc73c\ub85c Initialize()\ub97c \ud638\ucd9c\ud558\ub294 \uac83\uc744 \uad8c\uc7a5\ud569\ub2c8\ub2e4.
        /// </summary>
        public virtual void OnEnable()
        {
            // Initialize\uac00 \ud638\ucd9c\ub418\uc9c0 \uc54a\uc558\uc73c\uba74 \uae30\ubcf8\uac12\uc73c\ub85c \ucd08\uae30\ud654 (\ud638\ud658\uc131 \uc720\uc9c0)
            if (m_weaponManager == null || m_expSystem == null)
            {
                Initialize();
            }
        }

        protected virtual void OnDisable()
        {
            UnsubscribeEvents();
        }

        #region 초기화 및 의존성 주입

        /// <summary>
        /// [리팩토링] 외부에서 의존성을 주입받아 초기화합니다.
        /// GameManager나 Builder에서 호출하세요.
        /// </summary>
        /// <param name="weaponManager">무기 매니저 (null이면 내부 생성)</param>
        /// <param name="expSystem">경험치 시스템 (null이면 내부 생성)</param>
        public void Initialize(PlayerWeaponManager weaponManager = null, ExperienceSystem expSystem = null)
        {
            // 의존성 주입 또는 기본값 생성
            m_weaponManager = weaponManager ?? new PlayerWeaponManager(transform);
            m_expSystem = expSystem ?? new ExperienceSystem();
            
            InitializeComponents();
            InitializeSystems();
            SubscribeEvents();
            
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

        #endregion

        private void Update()
        {
            m_weaponManager?.OnUpdate();
        }

        private void LateUpdate()
        {
            m_weaponManager?.OnLateUpdate();
        }

        private void InitializeSystems()
        {
            float maxHp = m_config != null ? m_config.BaseMaxHealth : 100f;
            float speed = m_config != null ? m_config.BaseMoveSpeed : 5f;
            float attack = m_config != null ? m_config.BaseAttackPower : 10f;

            m_stats.Initialize(maxHp, speed, attack);
            m_expSystem.Init();
        }

        private void SubscribeEvents()
        {
            PlayStateManager.OnGameOver += OnGameOver;
            PlayStateManager.OnGamePause += OnGamePause;
            PlayStateManager.OnGameResume += OnGameResume;
            
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
            PlayStateManager.OnGameOver -= OnGameOver;
            PlayStateManager.OnGamePause -= OnGamePause;
            PlayStateManager.OnGameResume -= OnGameResume;
        }
        #endregion

        #region 게임 상태 핸들러
        private void OnGameResume() => m_collisionHandler?.SetColliderActive(true);
        private void OnGamePause() => m_collisionHandler?.SetColliderActive(false);
        private void OnGameOver() => m_collisionHandler?.SetColliderActive(false);
        #endregion

        #region 무기 관리 (위임)
        public void AddWeapon(WeaponBase weapon) => m_weaponManager?.AddWeapon(weapon);
        public void RemoveWeapon(string skillCode) => m_weaponManager?.RemoveWeapon(skillCode);
        public void SetTargetProvider(Func<Vector3> provider) => m_weaponManager?.SetTargetProvider(provider);
        public void EquipWeapon(WeaponDataSO data) => m_weaponManager?.EquipWeapon(data);
        #endregion

        #region 데미지 및 경험치 처리
        private void ApplyDamage(float damageAmount)
        {
            m_stats.ApplyDamage(damageAmount);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
            EffectManager.Instance?.PlayPlayerHitCameraShake();
            PlayHitEffect();
            
            if (m_stats.IsDead) Player_Die();
        }

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

        #region 플레이어 액션
        public virtual void Player_attack(Vector3 attackAngle) { }
        
        protected virtual void PlayHitEffect() 
        { 
            SoundKeys hitSound = m_config != null ? m_config.HitSoundKey : SoundKeys.playerHit;
            SoundManager.PlaySound(Sound.SFX, hitSound, false);
        }

        public virtual void Player_Die()
        {
            if (PlayStateManager.instance != null)
            {
                PlayStateManager.instance.PlayState = PlayStateManager.GameState.GameOver;
            }
            
            SoundKeys deathSound = m_config != null ? m_config.DeathSoundKey : SoundKeys.PlayerDeth;
            SoundManager.PlaySound(Sound.SFX, deathSound, false);
        }
        #endregion

        #region 유틸리티
        public float GetExpProgress() => m_expSystem.GetProgress();
        #endregion
    }
}