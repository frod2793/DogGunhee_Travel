using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 플레이어 캐릭터의 기본 동작, 스탯, 충돌 처리(틱 데미지 포함)를 정의하는 기본 클래스
    /// </summary>
    public class PlayerBase : MonoBehaviour
    {
        #region 플레이어 스탯 (인스펙터)

        [Header("공격 관련 스탯")]
        [FormerlySerializedAs("AttackPower")] 
        [Tooltip("기본 공격력")] [SerializeField] private float m_attackPower = 10f;
        public float AttackPower { get => m_attackPower; set => m_attackPower = value; }

        [FormerlySerializedAs("CoolTime")] 
        [Tooltip("공격 쿨타임")] [SerializeField] private float m_coolTime = 1f;
        public float CoolTime { get => m_coolTime; set => m_coolTime = value; }

        [FormerlySerializedAs("AttackSpeed")] 
        [Tooltip("공격 속도 (투사체 속도 등)")] [SerializeField] private float m_attackSpeed = 1f;
        public float AttackSpeed { get => m_attackSpeed; set => m_attackSpeed = value; }

        [FormerlySerializedAs("WeaponSize")] 
        [Tooltip("무기 크기 배율")] [SerializeField] private float m_weaponSize = 1f;
        public float WeaponSize { get => m_weaponSize; set => m_weaponSize = value; }

        [FormerlySerializedAs("ProjectileCount")] 
        [Tooltip("투사체 개수")] [SerializeField] private float m_projectileCount = 1f;
        public float ProjectileCount { get => m_projectileCount; set => m_projectileCount = value; }

        [FormerlySerializedAs("CriticalChance")] 
        [Tooltip("치명타 확률 (%)")] [SerializeField] private float m_criticalChance = 5f;
        public float CriticalChance { get => m_criticalChance; set => m_criticalChance = value; }

        [FormerlySerializedAs("CriticalDamage")] 
        [Tooltip("치명타 피해량 배율")] [SerializeField] private float m_criticalDamage = 1.5f;
        public float CriticalDamage { get => m_criticalDamage; set => m_criticalDamage = value; }

        [Header("방어 및 생존 관련 스탯")]
        [FormerlySerializedAs("Health")] 
        [Tooltip("최대 체력")] [SerializeField] private float m_maxHealth = 100f;
        public float MaxHealth
        {
            get => m_maxHealth;
            set
            {
                if (Mathf.Approximately(m_maxHealth, value)) return;
                m_maxHealth = value;
                OnHealthChanged?.Invoke(CurrentHealth, m_maxHealth);
            }
        }
        public float CurrentHealth { get; private set; }

        [FormerlySerializedAs("HealthRegen")] 
        [Tooltip("초당 체력 재생량")] [SerializeField] private float m_healthRegen = 0f;
        public float HealthRegen { get => m_healthRegen; set => m_healthRegen = value; }

        [FormerlySerializedAs("Defense")] 
        [Tooltip("방어력")] [SerializeField] private float m_defense = 0f;
        public float Defense { get => m_defense; set => m_defense = value; }

        [FormerlySerializedAs("MoveSpeed")] 
        [Tooltip("이동 속도")] [SerializeField] private float m_moveSpeed = 5f;
        public float MoveSpeed { get => m_moveSpeed; set => m_moveSpeed = value; }

        [Header("자원 획득 관련 스탯")]
        [FormerlySerializedAs("ExpGain")] 
        [Tooltip("경험치 획득량 배율")] [SerializeField] private float m_expGain = 1f;
        public float ExpGain { get => m_expGain; set => m_expGain = value; }

        [FormerlySerializedAs("GoldGain")] 
        [Tooltip("골드 획득량 배율")] [SerializeField] private float m_goldGain = 1f;
        public float GoldGain { get => m_goldGain; set => m_goldGain = value; }

        [FormerlySerializedAs("ItemGainRange")] 
        [Tooltip("아이템 획득 범위")] [SerializeField] private float m_itemGainRange = 1f;
        public float ItemGainRange { get => m_itemGainRange; set => m_itemGainRange = value; }

        [FormerlySerializedAs("Reroll")] 
        [Tooltip("리롤 횟수")] [SerializeField] private float m_reroll = 1f;
        public float Reroll { get => m_reroll; set => m_reroll = value; }

        [Header("캐릭터 정보")]
        public float Level { get; set; } = 1f;
        public Vector3 AttackAngle { get; set; }
        public int CharacterIndex { get; set; }

        [Header("경험치 시스템")] 
        public float CurrentExp { get; set; } = 0f;
        public float MaxExp { get; set; } = 100f;

        // 이벤트
        public static event Action<float> OnLevelUp;
        public static event Action<float, float> OnExpChanged; // currentExp, maxExp
        public event Action<float, float> OnHealthChanged; // currentHealth, maxHealth

        #endregion

        #region 내부 상태 관리

        public enum PlayerState
        {
            Idle,
            Move,
            Attack
        }

        private PlayerState m_playState;
        private bool m_isHit = false; // 무적 상태 플래그
        private bool m_isColliderActive = true;

        // 지속 충돌(틱 데미지) 관련 변수
        private float m_damageTickTimer = 0f;
        private const float k_ContactDamageInterval = 1.0f; // 1초마다 데미지
        
        private List<WeaphonBase> m_weapons = new List<WeaphonBase>();
        public IReadOnlyList<WeaphonBase> Weapons => m_weapons.AsReadOnly();


        public PlayerState PlayState
        {
            get => m_playState;
            set
            {
                if (m_playState == value) return;
                m_playState = value;
                SetPlayerState(m_playState);
            }
        }

        private void SetPlayerState(PlayerState state)
        {
            switch (state)
            {
                case PlayerState.Idle:
                    Player_Idle();
                    break;
                case PlayerState.Move:
                    PlayerMovement();
                    break;
            }
        }

        #endregion

        #region 초기화

        public virtual void OnEnable()
        {
            InitializeStats();
            SubscribeEvents();
            
            // 초기 상태 통지
            OnLevelUp?.Invoke(Level);
            OnExpChanged?.Invoke(CurrentExp, MaxExp);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        protected virtual void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void InitializeStats()
        {
            Level = 1f;
            CurrentExp = 0f;
            MaxExp = CalculateMaxExpForLevel(Level);
            CurrentHealth = MaxHealth;
            m_isHit = false;
            m_isColliderActive = true;
            m_damageTickTimer = 0f; // 타이머 초기화
            m_weapons.Clear();
        }

        private void SubscribeEvents()
        {
            PlayStateManager.OnGameOver += OnGameOver;
            PlayStateManager.OnGamePause += OnGamePause;
            PlayStateManager.OnGameResume += OnGameResume;
        }

        private void UnsubscribeEvents()
        {
            PlayStateManager.OnGameOver -= OnGameOver;
            PlayStateManager.OnGamePause -= OnGamePause;
            PlayStateManager.OnGameResume -= OnGameResume;
        }

        private void OnGameResume()
        {
            SetPlayerState(PlayState);
            m_isColliderActive = true;
        }

        private void OnGamePause()
        {
            Player_Idle(); 
            m_isColliderActive = false;
        }

        private void OnGameOver()
        {
            PlayState = PlayerState.Idle;
            m_isColliderActive = false;
        }

        public void AddWeapon(WeaphonBase weapon)
        {
            if (weapon != null)
            {
                m_weapons.Add(weapon);
                weapon.transform.SetParent(transform);
                weapon.transform.localPosition = Vector3.zero;
            }
            else
            {
                Debug.LogWarning("[PlayerBase] 전달된 무기가 null입니다.");
            }
        }

        #endregion

        #region 충돌 처리 및 틱 데미지

        public virtual void OnCollisionEnter2D(Collision2D other)
        {
            if (!m_isColliderActive) return;

            if (other.gameObject.CompareTag("Mob"))
            {
                // 충돌 시작 시 즉시 데미지 및 타이머 초기화
                HandleMobCollision(other.gameObject);
                m_damageTickTimer = 0f;
            } 
            if (other.gameObject.CompareTag("Exp"))
            {
                HandleExpCollision(other.gameObject);
            } 
            if (other.gameObject.CompareTag("Coin"))
            {
                HandleCoinCollision(other.gameObject);
            }
        }

        // [지속 충돌 처리] 몹과 닿아있는 동안 1초마다 데미지 적용
        public virtual void OnCollisionStay2D(Collision2D other)
        {
            if (!m_isColliderActive) return;

            if (other.gameObject.CompareTag("Mob"))
            {
                m_damageTickTimer += Time.fixedDeltaTime;

                if (m_damageTickTimer >= k_ContactDamageInterval)
                {
                    HandleMobCollision(other.gameObject);
                    m_damageTickTimer = 0f; // 데미지 적용 후 타이머 리셋
                }
            }
        }

        // [충돌 종료] 몹과 떨어지면 타이머 초기화
        public virtual void OnCollisionExit2D(Collision2D other)
        {
            if (other.gameObject.CompareTag("Mob"))
            {
                m_damageTickTimer = 0f;
            }
        }

        private void HandleMobCollision(GameObject mobObject)
        {
            // 무적 시간(피격 후 딜레이) 중이면 데미지 무시
            if (m_isHit) return;

            if (mobObject.TryGetComponent(out MobBase mob))
            {
                // 최적화된 프로퍼티 이름 사용 (AttackDamage)
                float damageAmount = CalculateIncomingDamage(mob.AttackDamage); 
                ApplyDamage(damageAmount);
                
                // 피격 후 잠시 무적 (0.5초 동안 연속 피격 방지)
                EnableHitCooldown(0.5f).Forget();
            }
        }

        private float CalculateIncomingDamage(float rawDamage)
        {
            return Mathf.Max(1, rawDamage * (100 / (100 + Defense)));
        }

        private void ApplyDamage(float damageAmount)
        {
            CurrentHealth -= damageAmount;
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            LogManager.Log($"피격! 데미지: {damageAmount:F1} (잔여 체력: {CurrentHealth:F1})", LogManager.LogCategory.PlayerBase, this);

            EffectManager.Instance?.PlayPlayerHitCameraShake();
            PlayHitEffect();

            if (CurrentHealth <= 0)
            {
                Player_Die();
            }
        }

        private void HandleExpCollision(GameObject expObject)
        {
            bool hasComponent = expObject.TryGetComponent(out EXP_Obj expObj);

            if (hasComponent && expObj.ObjectPoolSpawner != null)
            {
                // [정상 처리]
                float expAmount = expObj.ExpValue * ExpGain;
                AddExperience(expAmount);
                
                expObj.ObjectPoolSpawner.ExpObjectPool.Release(expObj);
                SoundManager.PlaySound(Sound.SFX, SoundKeys.GetExp, false);
            }
        }

        private void HandleCoinCollision(GameObject coinObject)
        {
            if (coinObject.TryGetComponent(out Coin_Obj coinObj) && coinObj.ObjectPoolSpawner != null)
            {
                float goldBonus = GoldGain > 0 ? GoldGain : 1;
                int coinsToAdd = Mathf.RoundToInt(1 * goldBonus);
                
                if (PlayerDataManagerDontdesytoy.Instance != null)
                {
                    PlayerDataManagerDontdesytoy.Instance.PlayerData.ingameCoin += coinsToAdd;
                }

                coinObj.ObjectPoolSpawner.CoinObjectPool.Release(coinObj);
                SoundManager.PlaySound(Sound.SFX, SoundKeys.GetCoin, false);
            }
        }

        #endregion

        #region 플레이어 액션 (가상 메서드)

        public virtual void Player_attack(Vector3 attackAngle) { }
        
        public virtual void Player_Die()
        {
            LogManager.Log("Game Over", LogManager.LogCategory.PlayerBase);
            if (PlayStateManager.instance != null)
            {
                PlayStateManager.instance.PlayState = PlayStateManager.GameState.GameOver;
            }
        }

        protected virtual void PlayHitEffect() { }
        public virtual void Player_Idle() { }
        public virtual void PlayerMovement() { }

        #endregion
        
        #region 경험치 시스템

        public void AddExperience(float expAmount)
        {
            CurrentExp += expAmount;
            CheckLevelUp();
        }

        private void CheckLevelUp()
        {
            bool leveledUp = false;
            while (CurrentExp >= MaxExp)
            {
                CurrentExp -= MaxExp;
                Level++;
                MaxExp = CalculateMaxExpForLevel(Level);
                
                leveledUp = true;
                HandleLevelUp(); 
                OnLevelUp?.Invoke(Level);
            }
            
            if (leveledUp)
            {
                LogManager.Log($"레벨업! Lv.{Level}", LogManager.LogCategory.PlayerBase);
            }
            
            OnExpChanged?.Invoke(CurrentExp, MaxExp);
        }

        private float CalculateMaxExpForLevel(float level)
        {
            return (level + 1) * 10f;
        }

        private void HandleLevelUp()
        {
            AttackPower += 5f;
            MaxHealth += 20f;
            Defense += 2f;
            
            SoundManager.PlaySound(Sound.SFX, SoundKeys.Levelup, false);
        }

        public float GetExpProgress()
        {
            return MaxExp > 0 ? CurrentExp / MaxExp : 0f;
        }

        #endregion
        
        #region 유틸리티

        private async UniTaskVoid EnableHitCooldown(float duration)
        {
            m_isHit = true;
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            finally
            {
                m_isHit = false;
            }
        }

        #endregion
    }
}