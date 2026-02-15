using System;
using System.Collections.Generic;
using UnityEngine;
using InGame.Managers;
using InGame.Weapon.Base;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// [설명]: 플레이어의 공통 데이터(HP, 경험치, 스탯)와 핵심 시스템(무기, 충돌 처리)을 관리하는 최상위 클래스입니다.
    /// 외부 시스템과의 의존성을 주입받아 초기화되며, 플레이어의 생존 및 무기 업데이트 사이클을 주도합니다.
    /// </summary>
    public class PlayerBase : MonoBehaviour
    {
        #region 에디터 설정

        [Header("기본 설정")]
        [SerializeField, Tooltip("캐릭터 기본 설정 데이터 (ScriptableObject)")]
        private CharacterConfigSO m_config;

        [Header("데이터 및 시스템")]
        [SerializeField, Tooltip("현재 플레이어의 스탯 정보 객체")]
        private PlayerStats m_stats = new PlayerStats();

        #endregion

        #region 내부 필드 및 서브 시스템

        /// <summary> 플레이어의 경험치 및 레벨업 관리 시스템 </summary>
        private ExperienceSystem m_expSystem = new ExperienceSystem();

        /// <summary> 무기 생성 및 리스트 관리자 </summary>
        private PlayerWeaponManager m_weaponManager;

        /// <summary> 트리거 및 컬렉션 이벤트 충돌 처리기 </summary>
        private PlayerCollisionHandler m_collisionHandler;

        /// <summary> 플레이어 데이터 비즈니스 로직 서비스 </summary>
        private InGame.Services.PlayerDataService m_playerService;

        private InGame.Services.ISoundManager m_soundManager;

        #endregion

        #region 이벤트

        /// <summary> [설명]: 전역 레벨업 이벤트 (전달 파라미터: 새로운 레벨) </summary>
        public static event Action<float> OnLevelUp;

        /// <summary> [설명]: 전역 경험치 변경 이벤트 (현재 경험치, 최대 경험치) </summary>
        public static event Action<float, float> OnExpChanged;

        /// <summary> [설명]: 현재 플레이어의 인스턴스 체력 변경 이벤트 (현재 체력, 최대 체력) </summary>
        public event Action<float, float> OnHealthChanged;

        #endregion

        #region 공개 프로퍼티

        /// <summary> [설명]: 현재 플레이어의 방어력 값입니다. </summary>
        public float Defense
        {
            get => m_stats.Defense;
            set => m_stats.Defense = value;
        }

        /// <summary> [설명]: 현재 플레이어의 이동 속도 값입니다. </summary>
        public float MoveSpeed
        {
            get => m_stats.MoveSpeed;
            set => m_stats.MoveSpeed = value;
        }

        /// <summary> [설명]: 현재 인스턴스의 남은 체력량입니다. </summary>
        public float CurrentHealth => m_stats.CurrentHealth;

        /// <summary> [설명]: 현재 인스턴스의 최대 체력량입니다. </summary>
        public float MaxHealth
        {
            get => m_stats.MaxHealth;
            set
            {
                m_stats.MaxHealth = value;
                OnHealthChanged?.Invoke(m_stats.CurrentHealth, m_stats.MaxHealth);
            }
        }

        /// <summary> [설명]: 플레이어의 현재 레벨 값입니다. </summary>
        public float Level => m_expSystem.Level;

        /// <summary> [설명]: 현재 보유 중인 경험치량입니다. </summary>
        public float CurrentExp => m_expSystem.CurrentExp;

        /// <summary> [설명]: 다음 레벨업을 위한 총 경험치량입니다. </summary>
        public float MaxExp => m_expSystem.MaxExp;

        /// <summary> [설명]: 현재 장착된 모든 무기 컨트롤러 목록입니다. </summary>
        public IReadOnlyList<IWeaponController> Weapons => m_weaponManager?.Controllers;

        #endregion

        #region 유니티 생명주기

        /// <summary>
        /// [설명]: 플레이어 오브젝트가 활성화될 때 필수 시스템이 초기화되지 않았다면 초기화를 수행합니다.
        /// </summary>
        public virtual void OnEnable()
        {
            if (m_weaponManager == null || m_expSystem == null)
            {
                Init();
            }
        }

        /// <summary>
        /// [설명]: 비활성화 시 등록된 시스템 이벤트를 구독 해제하여 메모리 누수를 방지합니다.
        /// </summary>
        protected virtual void OnDisable()
        {
            UnsubscribeEvents();
        }

        /// <summary>
        /// [설명]: 매 프레임 무기 시스템의 업데이트 로직을 호출합니다.
        /// </summary>
        private void Update()
        {
            m_weaponManager?.OnUpdate();
        }

        /// <summary>
        /// [설명]: 로직 업데이트 이후 무기 시스템의 후처리를 수행합니다.
        /// </summary>
        private void LateUpdate()
        {
            m_weaponManager?.OnLateUpdate();
        }

        /// <summary>
        /// [설명]: 파괴 시 장착된 모든 무기 리소스를 정리합니다.
        /// </summary>
        protected virtual void OnDestroy()
        {
            m_weaponManager?.ClearAllWeapons();
        }

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 플레이어 시스템의 의존성을 주입하고 내부 스탯과 기능을 초기화합니다.
        /// </summary>
        /// <param name="weaponManager">무기 관리자 주입 (생략 시 기본 생성)</param>
        /// <param name="expSystem">경험치 시스템 주입 (생략 시 기본 생성)</param>
        public void Init(PlayerWeaponManager weaponManager = null, ExperienceSystem expSystem = null, InGame.Services.PlayerDataService playerService = null, InGame.Services.ISoundManager soundManager = null)
        {
            // 의존성 주입 또는 기본 객체 생성
            m_weaponManager = weaponManager ?? CreateDefaultWeaponManager();
            if (soundManager != null)
            {
                m_weaponManager.SetSoundManager(soundManager);
            }
            
            m_expSystem = expSystem ?? new ExperienceSystem();
            m_playerService = playerService;
            m_soundManager = soundManager;

            // 내부 컴포넌트 및 데이터 로드
            InitializeComponents();
            InitializeStats();

            // 게임 이벤트 연결
            SubscribeEvents();

            // 초기 UI 갱신을 위한 알림 호출
            NotifyInitialState();

            LogManager.Log("[PlayerBase] 초기화 완료", LogManager.LogCategory.PlayerBase);
        }

        /// <summary>
        /// [설명]: 무기 관리를 담당하는 기본 객체를 생성합니다.
        /// </summary>
        private PlayerWeaponManager CreateDefaultWeaponManager()
        {
            return new PlayerWeaponManager();
        }

        /// <summary>
        /// [설명]: 충돌 핸들러 등 필수 MonoBehaviour 컴포넌트를 캐싱하거나 추가합니다.
        /// </summary>
        private void InitializeComponents()
        {
            m_collisionHandler = GetComponent<PlayerCollisionHandler>();
            if (m_collisionHandler == null)
            {
                m_collisionHandler = gameObject.AddComponent<PlayerCollisionHandler>();
            }

            m_collisionHandler.Init(m_soundManager);
        }

        /// <summary>
        /// [설명]: SO 설정 데이터를 로드하여 기본 전투 스탯을 할당합니다.
        /// </summary>
        private void InitializeStats()
        {
            float maxHp = m_config ? m_config.BaseMaxHealth : 100f;
            float speed = m_config ? m_config.BaseMoveSpeed : 5f;
            float attack = m_config ? m_config.BaseAttackPower : 10f;

            m_stats.Init(maxHp, speed, attack);
            m_expSystem.Init();
        }

        /// <summary>
        /// [설명]: 초기화 완료 후 첫 이벤트를 발생시켜 UI와 연동된 시스템을 동기화합니다.
        /// </summary>
        private void NotifyInitialState()
        {
            OnLevelUp?.Invoke(Level);
            OnExpChanged?.Invoke(CurrentExp, MaxExp);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        #endregion

        #region 공개 인터페이스

        /// <summary>
        /// [설명]: 플레이어에게 새로운 무기 컨트롤러를 추가합니다.
        /// </summary>
        public void AddController(IWeaponController weapon) => m_weaponManager?.AddController(weapon);

        /// <summary>
        /// [설명]: 무기 식별 코드를 기반으로 특정 무기를 장착 해제합니다.
        /// </summary>
        public void RemoveWeapon(string skillCode) => m_weaponManager?.RemoveWeapon(skillCode);

        /// <summary>
        /// [설명]: 플레이어의 체력이 0이 되어 사망했을 때의 처리를 수행합니다. (Game Over 전환 및 사운드 재생)
        /// </summary>
        public virtual void Player_Die()
        {
            if (GameManager.Instance.State != null)
            {
                GameManager.Instance.State.GameOver();
            }

            SoundKeys deathSound = m_config ? m_config.DeathSoundKey : SoundKeys.PlayerDeth;
            if (m_soundManager != null)
            {
                m_soundManager.Play(deathSound.ToString(), Sound.SFX, 1.0f, false);
            }
        }

        /// <summary>
        /// [설명]: 현재 경험지 진행 상태(0~1)를 반환합니다.
        /// </summary>
        public float GetExpProgress() => m_expSystem.GetProgress();

        #endregion

        #region 내부 핸들링 및 이벤트 응답

        /// <summary>
        /// [설명]: 데미지를 입었을 때 스탯을 갱신하고 연출(카메라 쉐이크, VFX)을 실행합니다.
        /// </summary>
        private void ApplyDamage(float damageAmount)
        {
            m_stats.ApplyDamage(damageAmount);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (EffectManager.Instance != null)
            {
                EffectManager.Instance.PlayPlayerHitCameraShake();
            }

            PlayHitEffect();

            if (m_stats.IsDead)
            {
                Player_Die();
            }
        }

        /// <summary>
        /// [설명]: 피격 시 사운드 및 피드백 효과를 재생합니다.
        /// </summary>
        protected virtual void PlayHitEffect()
        {
            SoundKeys hitSound = m_config ? m_config.HitSoundKey : SoundKeys.playerHit;
            if (m_soundManager != null)
            {
                m_soundManager.Play(hitSound.ToString(), Sound.SFX, 1.0f, false);
            }
        }

        /// <summary>
        /// [설명]: 외부 전역 시스템 및 내부 모듈의 이벤트를 바인딩합니다.
        /// </summary>
        private void SubscribeEvents()
        {
            if (GameManager.Instance.State != null)
            {
                GameManager.Instance.State.OnGameOver += OnGameOver;
                GameManager.Instance.State.OnGamePause += OnGamePause;
                GameManager.Instance.State.OnGameResume += OnGameResume;
            }

            m_expSystem.OnLevelUp += HandleLevelUp;
            m_expSystem.OnExpChanged += (cur, max) => OnExpChanged?.Invoke(cur, max);

            if (m_collisionHandler != null)
            {
                m_collisionHandler.OnDamageReceived += ApplyDamage;
                m_collisionHandler.OnExpCollected += m_expSystem.AddExperience;
                m_collisionHandler.OnCoinCollected += HandleCoinCollected;
            }
        }

        /// <summary>
        /// [설명]: 파괴 또는 비활성화 시 등록된 시스템 이벤트를 모두 해제합니다.
        /// </summary>
        private void UnsubscribeEvents()
        {
            if (GameManager.Instance.State != null)
            {
                GameManager.Instance.State.OnGameOver -= OnGameOver;
                GameManager.Instance.State.OnGamePause -= OnGamePause;
                GameManager.Instance.State.OnGameResume -= OnGameResume;
            }

            m_expSystem.OnLevelUp -= HandleLevelUp;
        }

        /// <summary> [설명]: 게임 재개 시 물리 상호작용을 재활성화합니다. </summary>
        private void OnGameResume()
        {
            if (m_collisionHandler != null)
            {
                m_collisionHandler.SetColliderActive(true);
            }
        }

        /// <summary> [설명]: 게임 일시정지 시 불필요한 물리 판정을 차단합니다. </summary>
        private void OnGamePause()
        {
            if (m_collisionHandler != null)
            {
                m_collisionHandler.SetColliderActive(false);
            }
        }

        /// <summary> [설명]: 게임 오버 시 모든 충돌 감지를 중단합니다. </summary>
        private void OnGameOver()
        {
            if (m_collisionHandler != null)
            {
                m_collisionHandler.SetColliderActive(false);
            }
        }

        /// <summary>
        /// [설명]: 레벨업 시 전역 알림을 수행하고 연출 사운드를 재생합니다.
        /// </summary>
        private void HandleLevelUp(int level)
        {
            OnLevelUp?.Invoke(level);
            if (m_soundManager != null)
            {
                m_soundManager.Play(SoundKeys.Levelup.ToString(), Sound.SFX, 1.0f, false);
            }
        }

        /// <summary>
        /// [설명]: 코인 수집 시 플레이어의 재화 데이터를 갱신합니다.
        /// </summary>
        private void HandleCoinCollected(int coinValue)
        {
            if (m_playerService != null)
            {
                m_playerService.AddCurrency("ingameCoin", coinValue);
            }
        }

        #endregion
    }
}