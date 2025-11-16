using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Serialization;
using UnityEngine;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 플레이어 캐릭터의 기본 동작과 속성을 정의하는 기본 클래스
    /// </summary>
    public class PlayerBase : MonoBehaviour
    {
        #region 플레이어 스탯

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
        public int characterIndex; // 현재 캐릭터 인덱스

        [Header("경험치 시스템")] public float CurrentExp { get; set; } = 0f;
        public float MaxExp { get; set; } = 100f;

        // 이벤트
        public static event Action<float> OnLevelUp;
        public static event Action<float, float> OnExpChanged; // currentExp, maxExp
        public event Action<float, float> OnHealthChanged; // currentHealth, maxHealth

        #endregion

        #region 플레이어 상태 관리

        /// <summary>
        /// 플레이어의 상태를 정의하는 열거형
        /// </summary>
        public enum PlayerState
        {
            Idle,
            Move,
            Attack
        }

        private PlayerState m_playState;
        private bool m_isHit = false; // 피격 후 짧은 무적 상태
        private bool m_isColliderActive = true;
        public Weaphon_base WeaphonBase { get; set; }

        /// <summary>
        /// 플레이어 상태 프로퍼티 - 상태 변경시 SetPlayerState 메서드 호출
        /// </summary>
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

        /// <summary>
        /// 플레이어의 상태에 따른 동작 분기 처리
        /// </summary>
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

        /// <summary>
        /// 오브젝트가 활성화될 때 호출되는 메서드
        /// </summary>
        public virtual void OnEnable()
        {
            //    InitializeWeapon();
            // 스폰 또는 풀에서 재사용될 때 스탯을 초기화합니다.
            Level = 1f;
            CurrentExp = 0f;
            MaxExp = CalculateMaxExpForLevel(Level);
            CurrentHealth = MaxHealth;

            PlayStateManager.OnGameOver += OnGameOver;
            PlayStateManager.OnGamePause += OnGamePause;
            PlayStateManager.OnGameResume += OnGameResume;
            // UI 등 다른 리스너들에게 초기 상태를 통지합니다.
            OnLevelUp?.Invoke(Level);
            OnExpChanged?.Invoke(CurrentExp, MaxExp);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        private void OnGameResume()
        {
            SetPlayerState(PlayState);
            m_isColliderActive = true;
        }

        private void OnGamePause()
        {
            SetPlayerState(PlayerState.Idle);
            m_isColliderActive = false;
        }


        protected virtual void OnDisable()
        {
            PlayStateManager.OnGameOver -= OnGameOver;
            PlayStateManager.OnGamePause -= OnGamePause;
            PlayStateManager.OnGameResume -= OnGameResume;
        }

        private void OnGameOver()
        {
            // 플레이어 이동 정지: 상태를 Idle로 변경하거나, 이동 관련 변수/컨트롤러 비활성화
            PlayState = PlayerState.Idle;
            // 필요시 이동 관련 추가 변수/컨트롤러도 비활성화
        }

        /// <summary>
        /// 무기 초기화 및 위치 설정
        /// </summary>
        public void InitializeWeapon(Weaphon_base weapon)
        {
            if (weapon != null)
            {
                WeaphonBase = weapon;
                WeaphonBase.transform.SetParent(transform);
                WeaphonBase.transform.localPosition = Vector3.zero;
            }
            else
            {
                Debug.LogWarning("전달된 무기 베이스가 없습니다.");
            }
        }

        #endregion

        #region 충돌 처리

        /// <summary>
        /// 플레이어와 다른 오브젝트 간의 충돌 처리
        /// </summary>
        public virtual void OnCollisionEnter2D(Collision2D other)
        {
            if (m_isColliderActive)
            {
                GameObject colliderObject = other.gameObject;
                string objectTag = colliderObject.tag; // CompareTag를 사용하는 것이 더 효율적입니다.

                switch (objectTag)
                {
                    case "Mob":
                        HandleMobCollision(colliderObject);
                        break;
                    case "Exp":
                        HandleExpCollision(colliderObject);
                        break;
                    case "Coin":
                        HandleCoinCollision(colliderObject);
                        break;
                }
            }
        }

        /// <summary>
        /// 몹과의 충돌 처리 및 피해 계산
        /// </summary>
        /// <param name="mobObject">충돌한 몹 게임오브젝트</param>
        private void HandleMobCollision(GameObject mobObject)
        {
            // 이미 피격 상태면 추가 처리 없음
            if (m_isHit) return;

            // 피격 처리 및 쿨다운 시작
            EnableHitCooldown(1f).Forget();

            // 몹으로부터 피해 계산
            VamserMobBase mob = mobObject.GetComponent<VamserMobBase>();
            if (mob != null)
            {
                float damageAmount = CalculateIncomingDamage(mob.Mob_AttackDamage);
                ApplyDamage(damageAmount);
            }
        }

        /// <summary>
        /// 방어력을 고려한 최종 피해량 계산
        /// </summary>
        private float CalculateIncomingDamage(float rawDamage)
        {
            // 방어력 공식 적용 (방어력이 높을수록 피해 감소)
            return Mathf.Max(1, rawDamage * (100 / (100 + Defense)));
        }

        /// <summary>
        /// 플레이어에게 피해 적용 및 효과 처리
        /// </summary>
        private void ApplyDamage(float damageAmount)
        {
            CurrentHealth -= damageAmount;
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            // 피해량 디버그 로그
            LogManager.Log($"플레이어가 <color=#FF0000>{damageAmount:F1}</color> 데미지를 받음 (남은 체력: {CurrentHealth:F1})", LogManager.LogCategory.PlayerBase, this);

            // 이펙트 매니저를 직접 호출하여 카메라 흔들림 효과를 재생합니다.
            EffectManager.Instance?.PlayPlayerHitCameraShake();

            // 피격 효과 재생
            PlayHitEffect();

            // 데미지를 입은 후 체력을 확인하여 사망 처리
            if (CurrentHealth <= 0)
            {
                Player_Die();
            }
        }

        /// <summary>
        /// 경험치 아이템과의 충돌 처리
        /// </summary>
        private void HandleExpCollision(GameObject expObject)
        {
            EXP_Obj expObj = expObject.GetComponent<EXP_Obj>();
            if (expObj != null && expObj.objectPoolSpawner != null)
            {
                // 경험치 획득 처리 추가
                float expAmount = expObj.ExpValue * ExpGain; // 아이템의 경험치 값에 획득 보너스 적용
                LogManager.Log($"경험치 {expAmount} 획득", LogManager.LogCategory.PlayerBase);
                // 경험치 증가 및 UI 업데이트
                AddExperience(expAmount);
                // 오브젝트 풀로 반환
                expObj.objectPoolSpawner.ExpObjectPool.Release(expObj);
                SoundManager.PlaySound(Sound.SFX, SoundKeys.GetExp, false);
            }
        }

        /// <summary>
        /// 코인 아이템과의 충돌 처리
        /// </summary>
        private void HandleCoinCollision(GameObject coinObject)
        {
            Coin_Obj coinObj = coinObject.GetComponent<Coin_Obj>();
            if (coinObj != null && coinObj.objectPoolSpawner != null)
            {
                // 코인 획득량에 골드 획득 보너스 적용 가능
                float goldBonus = GoldGain > 0 ? GoldGain : 1;
                int coinsToAdd = Mathf.RoundToInt(1 * goldBonus);
                // 실제 코인 증가
                PlayerDataManagerDontdesytoy.Instance.PlayerData.ingameCoin += coinsToAdd;
                LogManager.Log($"코인 {coinsToAdd}개 획득", LogManager.LogCategory.PlayerBase);
                // 오브젝트 풀로 반환
                coinObj.objectPoolSpawner.CoinObjectPool.Release(coinObj);
                SoundManager.PlaySound(Sound.SFX, SoundKeys.GetCoin, false);
            }
        }

        #endregion

        #region 플레이어 액션

        /// <summary>
        /// 플레이어 공격 동작
        /// </summary>
        public virtual void Player_attack(Vector3 attackAngle)
        {
            attackAngle = this.AttackAngle;
            // 자식 클래스에서 구현
        }

        /// <summary>
        /// 플레이어 사망 처리
        /// </summary>
        public virtual void Player_Die()
        {
            LogManager.Log("플레이어 사망 - 게임 오버 처리 시작", LogManager.LogCategory.PlayerBase);
            // 게임 오버 상태로 변경
            if (PlayStateManager.instance != null)
            {
                PlayStateManager.instance.PlayState = PlayStateManager.GameState.GameOver;
            }
            // 플레이어 비활성화 (선택사항)
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 피격 효과 재생
        /// </summary>
        protected virtual void PlayHitEffect()
        {
            // 애니메이션 효과, 사운드 효과 등 구현
            // AudioManager.Instance.PlaySound("PlayerHit");
            LogManager.Log("피격 효과 재생", LogManager.LogCategory.PlayerBase);
        }

        /// <summary>
        /// 플레이어 대기 동작
        /// </summary>
        public virtual void Player_Idle()
        {
            // 자식 클래스에서 구현
        }
        
        /// <summary>
        /// 플레이어 이동 동작
        /// </summary>
        public virtual void PlayerMovement()
        {
            // 자식 클래스에서 구현
        }

        #endregion
        
        #region 경험치 시스템

        /// <summary>
        /// 경험치를 추가하고 레벨업을 체크합니다.
        /// </summary>
        /// <param name="expAmount">추가할 경험치 양</param>
        public void AddExperience(float expAmount)
        {
            CurrentExp += expAmount;
            // 레벨업 체크
            CheckLevelUp();
        }

        /// <summary>
        /// 경험치를 확인하여 필요 시 레벨업을 처리하고, 최종 상태를 이벤트로 알립니다.
        /// </summary>
        private void CheckLevelUp()
        {
            while (CurrentExp >= MaxExp)
            {
                CurrentExp -= MaxExp;
                Level++;
                MaxExp = CalculateMaxExpForLevel(Level);
                
                OnLevelUp?.Invoke(Level);
                HandleLevelUp();
                LogManager.Log($"레벨업! 현재 레벨: {Level}, 필요 경험치: {MaxExp}", LogManager.LogCategory.PlayerBase);
            }
            
            // 경험치 획득 또는 레벨업 처리가 모두 끝난 후, 최종 상태를 한 번만 알립니다.
            OnExpChanged?.Invoke(CurrentExp, MaxExp);
        }

        /// <summary>
        /// 레벨에 따른 최대 경험치를 계산합니다.
        /// </summary>
        /// <param name="level">다음 레벨업에 필요한 경험치를 계산할 현재 레벨</param>
        /// <returns>해당 레벨에서 필요한 최대 경험치</returns>
        private float CalculateMaxExpForLevel(float level)
        {
            // 레벨 1->2: 20, 레벨 2->3: 30, ...
            return (level + 1) * 10f;
        }

        /// <summary>
        /// 레벨업 시 플레이어 능력치 증가를 처리합니다.
        /// </summary>
        private void HandleLevelUp()
        {
            // 레벨업 시 스탯 증가
            AttackPower += 5f;
            MaxHealth += 20f;
            Defense += 2f;
            //MoveSpeed += 0.1f;
            
            // 레벨업 사운드 효과 (선택사항)
            SoundManager.PlaySound(Sound.SFX, SoundKeys.Levelup, false);
            
        }

        /// <summary>
        /// 현재 경험치 진행률을 0~1 범위로 반환합니다.
        /// </summary>
        /// <returns>경험치 진행률 (0~1)</returns>
        public float GetExpProgress()
        {
            return MaxExp > 0 ? CurrentExp / MaxExp : 0f;
        }

        #endregion
        
        #region 유틸리티

        /// <summary>
        /// 지정된 시간 동안 피격 무적 상태를 활성화합니다.
        /// </summary>
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
