using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace DogGuns_Games.vamsir
{
    public class PlayerBase : MonoBehaviour
    {
        #region 플레이어 스탯 (인스펙터)

        [Header("공격 관련 스탯")]
        [SerializeField] private float m_attackPower = 10f;
        public float AttackPower { get => m_attackPower; set => m_attackPower = value; }
        [SerializeField] private float m_coolTime = 1f;
        public float CoolTime { get => m_coolTime; set => m_coolTime = value; }
        [SerializeField] private float m_attackSpeed = 1f;
        public float AttackSpeed { get => m_attackSpeed; set => m_attackSpeed = value; }
        [SerializeField] private float m_weaponSize = 1f;
        public float WeaponSize { get => m_weaponSize; set => m_weaponSize = value; }
        [SerializeField] private float m_projectileCount = 1f;
        public float ProjectileCount { get => m_projectileCount; set => m_projectileCount = value; }
        
        [Header("방어 및 생존 관련 스탯")]
        [SerializeField] private float m_maxHealth = 100f;
        public float MaxHealth { get => m_maxHealth; set { m_maxHealth = value; OnHealthChanged?.Invoke(CurrentHealth, m_maxHealth); } }
        public float CurrentHealth { get; private set; }
        [SerializeField] private float m_defense = 0f;
        public float Defense { get => m_defense; set => m_defense = value; }
        [SerializeField] private float m_moveSpeed = 5f;
        public float MoveSpeed { get => m_moveSpeed; set => m_moveSpeed = value; }

        [Header("캐릭터 정보")]
        public float Level { get; set; } = 1f;
        public float CurrentExp { get; set; } = 0f;
        public float MaxExp { get; set; } = 100f;

        public static event Action<float> OnLevelUp;
        public static event Action<float, float> OnExpChanged;
        public event Action<float, float> OnHealthChanged;

        #endregion

        #region 내부 상태 관리
        
        private bool m_isHit = false;
        private bool m_isColliderActive = true;
        private float m_damageTickTimer = 0f;
        private const float k_ContactDamageInterval = 1.0f;
        
        private List<WeaphonBase> m_weapons = new List<WeaphonBase>();
        public IReadOnlyList<WeaphonBase> Weapons => m_weapons.AsReadOnly();

        #endregion

        #region 초기화

        public virtual void OnEnable()
        {
            InitializeStats();
            SubscribeEvents();
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
            m_damageTickTimer = 0f;
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

        private void OnGameResume() => m_isColliderActive = true;
        private void OnGamePause() => m_isColliderActive = false;
        private void OnGameOver() => m_isColliderActive = false;

        public void AddWeapon(WeaphonBase weapon)
        {
            if (weapon != null)
            {
                m_weapons.Add(weapon);
                weapon.transform.SetParent(transform);
                weapon.transform.localPosition = Vector3.zero;
            }
        }

        public void RemoveWeapon(string skillCode)
        {
            var weaponToRemove = m_weapons.FirstOrDefault(w => w.skillCode == skillCode);
            if (weaponToRemove != null)
            {
                m_weapons.Remove(weaponToRemove);
                Destroy(weaponToRemove.gameObject);
            }
        }

        #endregion

        #region 충돌 처리 및 틱 데미지

        public virtual void OnCollisionEnter2D(Collision2D other)
        {
            if (!m_isColliderActive) return;
            if (other.gameObject.CompareTag("Mob"))
            {
                HandleMobCollision(other.gameObject);
                m_damageTickTimer = 0f;
            } 
            if (other.gameObject.CompareTag("Exp"))
            {
                HandleExpCollision(other.gameObject);
            } 
        }

        public virtual void OnCollisionStay2D(Collision2D other)
        {
            if (!m_isColliderActive || !other.gameObject.CompareTag("Mob")) return;
            m_damageTickTimer += Time.fixedDeltaTime;
            if (m_damageTickTimer >= k_ContactDamageInterval)
            {
                HandleMobCollision(other.gameObject);
                m_damageTickTimer = 0f;
            }
        }

        public virtual void OnCollisionExit2D(Collision2D other)
        {
            if (other.gameObject.CompareTag("Mob"))
            {
                m_damageTickTimer = 0f;
            }
        }

        private void HandleMobCollision(GameObject mobObject)
        {
            if (m_isHit) return;
            if (mobObject.TryGetComponent(out MobBase mob))
            {
                float damageAmount = CalculateIncomingDamage(mob.AttackDamage); 
                ApplyDamage(damageAmount);
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
            EffectManager.Instance?.PlayPlayerHitCameraShake();
            PlayHitEffect(); // [복원]
            if (CurrentHealth <= 0)
            {
                Player_Die();
            }
        }

        private void HandleExpCollision(GameObject expObject)
        {
            if (expObject.TryGetComponent(out EXP_Obj expObj) && expObj.ObjectPoolSpawner != null)
            {
                AddExperience(expObj.ExpValue);
                expObj.ObjectPoolSpawner.ExpObjectPool.Release(expObj);
                SoundManager.PlaySound(Sound.SFX, SoundKeys.GetExp, false);
            }
        }

        #endregion

        #region 플레이어 액션

        // [복원] 자식 클래스들이 override 할 수 있도록 virtual 메서드 추가
        public virtual void Player_attack(Vector3 attackAngle) { }
        protected virtual void PlayHitEffect() { }

        public virtual void Player_Die()
        {
            if (PlayStateManager.instance != null)
            {
                PlayStateManager.instance.PlayState = PlayStateManager.GameState.GameOver;
            }
        }

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
                OnLevelUp?.Invoke(Level);
            }
            if (leveledUp)
            {
                SoundManager.PlaySound(Sound.SFX, SoundKeys.Levelup, false);
            }
            OnExpChanged?.Invoke(CurrentExp, MaxExp);
        }

        private float CalculateMaxExpForLevel(float level)
        {
            return (level + 1) * 10f;
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