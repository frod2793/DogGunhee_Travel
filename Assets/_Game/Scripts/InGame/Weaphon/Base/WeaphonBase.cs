using UnityEngine;


namespace InGame.Weaphon.Base
{
    public abstract class WeaphonBase : MonoBehaviour
    {
        #region 필드 및 프로퍼티

        [Header("기본 능력치")]
        public float attackPower;
        public float coolTime;
        public float attackSpeed;
        public float attackRange;

        [Header("공격 특성")]
        public float mobStunTime;
        public bool isShooting;

        [Header("상태 및 업그레이드")]
        public string skillCode;
        public string upgradeItemCode;
        public Sprite Thumnail;

        [Header("레벨 시스템")]
        [SerializeField] private int m_currentLevel = 1;
        public int CurrentLevel => m_currentLevel;
        public const int k_MaxLevel = 6;
        public bool isEvolved = false;

        public SkillData skillData { get; set; }

        public enum WeaphonState { Idle, Attack, Reload }
        [SerializeField] protected WeaphonState weaphonState;
        public WeaphonState CurrentState => weaphonState;

        #endregion

        #region Unity 라이프사이클

        protected void OnEnable()
        {
            SetWeaphonState(WeaphonState.Idle);
        }

        protected void OnDisable() { }

        #endregion

        #region 초기화 및 스탯 적용

        public void ApplyBaseStats()
        {
            if (skillData == null) return;

            if (skillData.BaseStats.TryGetValue("Damage", out float damage))
                attackPower = damage;
            
            if (skillData.BaseStats.TryGetValue("Cooldown", out float cooldown))
                coolTime = cooldown;
        }

        #endregion

        #region 레벨업 및 상태 관리

        public void UpgradeLevel()
        {
            if (m_currentLevel < k_MaxLevel)
            {
                m_currentLevel++;
                OnLevelUp(m_currentLevel);
                LogManager.Log($"무기 '{name}' 레벨 업! -> Lv.{m_currentLevel}", LogManager.LogCategory.Weapon);
            }
            else if (m_currentLevel == k_MaxLevel && !isEvolved)
            {
                isEvolved = true;
                OnEvolve();
                LogManager.Log($"무기 '{name}' 진화!", LogManager.LogCategory.Weapon);
            }
        }

        protected virtual void OnLevelUp(int newLevel)
        {
            if (skillData == null || !skillData.Upgrades.TryGetValue(newLevel, out var modifications))
            {
                return;
            }

            foreach (var mod in modifications)
            {
                ApplyStatModification(mod);
            }
        }

        private void ApplyStatModification(StatModification mod)
        {
            float value = mod.Value;
            switch (mod.StatName)
            {
                case "Damage":
                    attackPower = (mod.Mode == ModificationMode.Add) ? attackPower + value : attackPower * value;
                    break;
                case "Cooldown":
                    coolTime = (mod.Mode == ModificationMode.Add) ? coolTime + value : coolTime * value;
                    break;
                case "AttackSpeed":
                    attackSpeed = (mod.Mode == ModificationMode.Add) ? attackSpeed + value : attackSpeed * value;
                    break;
                default:
                    Debug.LogWarning($"[WeaphonBase] 알 수 없는 스탯 이름: {mod.StatName}");
                    break;
            }
        }

        protected virtual void OnEvolve() 
        {
            if (skillData?.EvolutionInfo != null)
            {
                skillData.skillName = skillData.EvolutionInfo.Name;
                skillData.flavorText = skillData.EvolutionInfo.FlavorText;
            }
        }

        public void SetWeaphonState(WeaphonState state)
        {
            weaphonState = state;
            switch (state)
            {
                case WeaphonState.Idle: Weaphon_Idle(); break;
                case WeaphonState.Attack: Weaphon_Attack(Vector3.zero); break;
                case WeaphonState.Reload: Weaphon_Reload(); break;
            }
        }

        #endregion

        #region 핵심 동작 (추상)

        public virtual void Weaphon_Idle() { }
        public virtual void Weaphon_Attack(Vector3 attackAngle) { }
        public virtual void Weaphon_Reload() { }

        #endregion
    }
}