using System;
using System.Collections.Generic;
using UnityEngine;
using InGame.Manager;
using InGame.Weapon.Base;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 공통 데이터(HP, Exp, Stat)와 핵심 시스템(무기, 충돌)을 관리하는 최상위 클래스입니다.
    /// <br/> Manager 및 외부 시스템과의 의존성을 주입(DI)받아 초기화할 수 있습니다.
    /// </summary>
    public class PlayerBase : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("기본 설정")] [SerializeField, Tooltip("캐릭터 기본 설정 데이터 (SO)")]
        private CharacterConfigSO m_config;

        [Header("데이터 및 시스템")] [SerializeField, Tooltip("현재 플레이어의 스탯 정보")]
        private PlayerStats m_stats = new PlayerStats();

        #endregion

        #region 2. 내부 변수 및 시스템

        // 하위 시스템 객체
        private ExperienceSystem m_expSystem = new ExperienceSystem();
        private PlayerWeaponManager m_weaponManager;
        private PlayerCollisionHandler m_collisionHandler;

        #endregion

        #region 3. 이벤트 선언

        // 전역 이벤트 (Static)
        public static event Action<float> OnLevelUp;
        public static event Action<float, float> OnExpChanged;

        // 인스턴스 이벤트
        public event Action<float, float> OnHealthChanged;

        #endregion

        #region 4. 프로퍼티 (데이터 접근)

        // 스탯 연결 (Pass-through)
        public float Defense
        {
            get => m_stats.Defense;
            set => m_stats.Defense = value;
        }

        public float MoveSpeed
        {
            get => m_stats.MoveSpeed;
            set => m_stats.MoveSpeed = value;
        }

        // 체력 로직
        public float CurrentHealth => m_stats.CurrentHealth;

        public float MaxHealth
        {
            get => m_stats.MaxHealth;
            set
            {
                m_stats.MaxHealth = value;
                // 최대 체력이 변경되면 UI 갱신을 위해 현재 체력 비율과 함께 이벤트를 호출합니다.
                OnHealthChanged?.Invoke(m_stats.CurrentHealth, m_stats.MaxHealth);
            }
        }

        // 시스템 접근자
        public float Level => m_expSystem.Level;
        public float CurrentExp => m_expSystem.CurrentExp;
        public float MaxExp => m_expSystem.MaxExp;
        public IReadOnlyList<IWeaponController> Weapons => m_weaponManager?.Controllers;

        #endregion

        #region 5. 유니티 생명주기

        /// <summary>
        /// 오브젝트가 활성화될 때 호출됩니다. 초기화가 누락되었을 경우 안전장치로 Init을 호출합니다.
        /// </summary>
        public virtual void OnEnable()
        {
            if (m_weaponManager == null || m_expSystem == null)
            {
                Init();
            }
        }

        /// <summary>
        /// 오브젝트가 비활성화될 때 호출됩니다. 모든 이벤트를 구독 해제하여 메모리 누수를 방지합니다.
        /// </summary>
        protected virtual void OnDisable()
        {
            UnsubscribeEvents();
        }

        /// <summary>
        /// 매 프레임 호출됩니다. 무기 시스템의 업데이트 로직을 위임합니다.
        /// </summary>
        private void Update()
        {
            m_weaponManager?.OnUpdate();
        }

        /// <summary>
        /// 모든 Update가 끝난 후 호출됩니다. 카메라 추적이나 무기의 후처리 로직을 위임합니다.
        /// </summary>
        private void LateUpdate()
        {
            m_weaponManager?.OnLateUpdate();
        }

        /// <summary>
        /// 오브젝트가 파괴될 때 호출됩니다. 무기 풀링 등 리소스를 정리합니다.
        /// </summary>
        protected virtual void OnDestroy()
        {
            m_weaponManager?.ClearAllWeapons();
        }

        #endregion

        #region 6. 초기화 및 의존성 주입

        /// <summary>
        /// 플레이어 시스템을 초기화합니다. 
        /// 테스트 코드나 외부 매니저에서 의존성을 주입(DI)할 수 있습니다.
        /// </summary>
        /// <param name="weaponManager">무기 관리자 (null일 경우 기본값 생성)</param>
        /// <param name="expSystem">경험치 시스템 (null일 경우 기본값 생성)</param>
        public void Init(PlayerWeaponManager weaponManager = null, ExperienceSystem expSystem = null)
        {
            // 1. 의존성 주입 또는 기본값 생성
            m_weaponManager = weaponManager ?? CreateDefaultWeaponManager();
            m_expSystem = expSystem ?? new ExperienceSystem();

            // 2. 내부 컴포넌트 및 데이터 설정
            InitializeComponents();
            InitializeStats();

            // 3. 이벤트 연결
            SubscribeEvents();

            // 4. 초기 상태 UI 갱신을 위한 알림
            NotifyInitialState();

            LogManager.Log("[PlayerBase] 초기화 완료", LogManager.LogCategory.PlayerBase);
        }

        /// <summary>
        /// 기본 무기 관리자를 생성합니다. GameManager의 오브젝트 풀을 참조합니다.
        /// </summary>
        private PlayerWeaponManager CreateDefaultWeaponManager()
        {
            return new PlayerWeaponManager();
        }

        /// <summary>
        /// 충돌 처리기 등 필수 컴포넌트를 가져오거나 없으면 동적으로 추가합니다.
        /// </summary>
        private void InitializeComponents()
        {
            m_collisionHandler = GetComponent<PlayerCollisionHandler>();
            if (m_collisionHandler == null)
            {
                m_collisionHandler = gameObject.AddComponent<PlayerCollisionHandler>();
            }

            m_collisionHandler.Init();
        }

        /// <summary>
        /// ScriptableObject(Config)에서 기본 스탯을 불러와 초기화합니다.
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
        /// 초기화 직후 현재 레벨, 경험치, 체력 상태를 UI에 알립니다.
        /// </summary>
        private void NotifyInitialState()
        {
            OnLevelUp?.Invoke(Level);
            OnExpChanged?.Invoke(CurrentExp, MaxExp);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        #endregion

        #region 7. 공개 메서드 (외부 제어)

        /// <summary>
        /// 특정 무기 컨트롤러(IWeaponController)를 플레이어에게 추가하고 관리 대상에 등록합니다.
        /// </summary>
        public void AddController(IWeaponController weapon) => m_weaponManager?.AddController(weapon);

        /// <summary>
        /// 스킬 코드(ID)를 기반으로 해당 무기를 찾아 제거합니다.
        /// </summary>
        public void RemoveWeapon(string skillCode) => m_weaponManager?.RemoveWeapon(skillCode);

        /// <summary>
        /// 플레이어 사망 시 호출됩니다. 게임 오버 상태로 전환하고 사운드를 재생합니다.
        /// </summary>
        public virtual void Player_Die()
        {
            if (GameManager.Instance.State != null)
            {
                GameManager.Instance.State.GameOver();
            }

            SoundKeys deathSound = m_config ? m_config.DeathSoundKey : SoundKeys.PlayerDeth;
            SoundManager.PlaySound(Sound.SFX, deathSound, false);
        }

        /// <summary>
        /// 현재 경험치의 진행률(0.0 ~ 1.0)을 반환합니다. UI 슬라이더 표시에 사용됩니다.
        /// </summary>
        public float GetExpProgress() => m_expSystem.GetProgress();

        #endregion

        #region 8. 내부 로직 및 이벤트 핸들러

        /// <summary>
        /// 데미지를 적용하고, 체력 변화 이벤트를 발생시킵니다. 체력이 0이 되면 사망 처리합니다.
        /// </summary>
        /// <param name="damageAmount">받은 데미지 양</param>
        private void ApplyDamage(float damageAmount)
        {
            m_stats.ApplyDamage(damageAmount);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            // 피격 피드백 (카메라 쉐이크 및 사운드)
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
        /// 피격 시 사운드를 재생합니다. (자식 클래스에서 VFX 등을 추가하도록 override 가능)
        /// </summary>
        protected virtual void PlayHitEffect()
        {
            SoundKeys hitSound = m_config ? m_config.HitSoundKey : SoundKeys.playerHit;
            SoundManager.PlaySound(Sound.SFX, hitSound, false);
        }

        /// <summary>
        /// 게임 매니저 및 내부 시스템의 이벤트들을 구독합니다.
        /// </summary>
        private void SubscribeEvents()
        {
            // 1. 게임 상태 변경 이벤트 구독
            if (GameManager.Instance.State != null)
            {
                GameManager.Instance.State.OnGameOver += OnGameOver;
                GameManager.Instance.State.OnGamePause += OnGamePause;
                GameManager.Instance.State.OnGameResume += OnGameResume;
            }

            // 2. 내부 시스템(경험치, 충돌) 이벤트 구독
            m_expSystem.OnLevelUp += HandleLevelUp;
            m_expSystem.OnExpChanged += (cur, max) => OnExpChanged?.Invoke(cur, max); // 람다로 외부 정적 이벤트 연결

            // 3. 충돌 핸들러 이벤트 연결
            if (m_collisionHandler != null)
            {
                m_collisionHandler.OnDamageReceived += ApplyDamage;
                m_collisionHandler.OnExpCollected += m_expSystem.AddExperience;
                m_collisionHandler.OnCoinCollected += HandleCoinCollected;
            }
        }

        /// <summary>
        /// 구독했던 모든 이벤트를 해제합니다.
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

        // --- 이벤트 콜백 (Event Callbacks) ---

        /// <summary>
        /// 게임 재개 시 충돌체를 다시 활성화합니다.
        /// </summary>
        private void OnGameResume()
        {
            if (m_collisionHandler != null)
            {
                m_collisionHandler.SetColliderActive(true);
            }
        }

        /// <summary>
        /// 게임 일시정지 시 충돌체를 비활성화하여 불필요한 물리 연산을 막습니다.
        /// </summary>
        private void OnGamePause()
        {
            if (m_collisionHandler != null)
            {
                m_collisionHandler.SetColliderActive(false);
            }
        }

        /// <summary>
        /// 게임 오버 시 충돌체를 비활성화합니다.
        /// </summary>
        private void OnGameOver()
        {
            if (m_collisionHandler != null)
            {
                m_collisionHandler.SetColliderActive(false);
            }
        }

        /// <summary>
        /// 레벨업 발생 시 전역 이벤트를 호출하고 축하 사운드를 재생합니다.
        /// </summary>
        private void HandleLevelUp(int level)
        {
            OnLevelUp?.Invoke(level);
            SoundManager.PlaySound(Sound.SFX, SoundKeys.Levelup, false);
        }

        /// <summary>
        /// 코인 획득 시 플레이어 데이터 매니저에 재화를 추가합니다.
        /// </summary>
        private void HandleCoinCollected(int coinValue)
        {
            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.PlayerData.ingameCoin += coinValue;
            }
        }

        #endregion
    }
}