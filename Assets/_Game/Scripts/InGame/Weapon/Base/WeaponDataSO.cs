using UnityEngine;
using System.Collections.Generic;

namespace InGame.Weapon.Base
{
    /// <summary>
    /// 무기의 구성 정보와 기본 스탯을 저장하는 ScriptableObject 기반 데이터 클래스입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "Game/Weapon/WeaponData")]
    public class WeaponDataSO : ScriptableObject
    {
        #region 기본 정보

        [Header("무기 식별 정보")]
        [Tooltip("XML 데이터와 연결되는 고유 키값입니다. (예: WP_BONE)")]
        [SerializeField] private string m_skillCode;

        [Tooltip("게임 내 UI에 표시될 무기의 이름입니다.")]
        [SerializeField] private string m_weaponName;

        [Tooltip("스킬 인벤토리나 룰렛 등에 표시될 아이콘 이미지입니다.")]
        [SerializeField] private Sprite m_icon;

        [Tooltip("무기에 대한 상세 설명입니다.")]
        [TextArea] 
        [SerializeField] private string m_description;

        #endregion

        #region 기본 스탯

        [Header("기본 스탯")]
        [Tooltip("무기의 기본 공격력입니다.")]
        [SerializeField] private float m_baseAttackPower;

        [Tooltip("무기의 기본 재사용 대기시간(초)입니다.")]
        [SerializeField] private float m_baseCoolTime;

        [Tooltip("공격 속도 배율입니다. (기본값: 1.0)")]
        [SerializeField] private float m_baseAttackSpeed = 1.0f;

        [Tooltip("공격 범위 또는 투사체 크기 등에 사용되는 거리 수치입니다.")]
        [SerializeField] private float m_baseAttackRange;

        [Tooltip("지속성 무기의 경우 효과가 유지되는 시간(초)입니다.")]
        [SerializeField] private float m_baseDuration;

        [Tooltip("한 번에 발사되거나 생성되는 투사체의 개수입니다.")]
        [SerializeField] private int m_baseProjectileCount = 1;

        #endregion

        #region 프리팹 참조

        [Header("에셋 참조")]
        [Tooltip("발사될 투사체(Projectile) 프리발입니다.")]
        [SerializeField] private GameObject m_projectilePrefab;

        [Tooltip("무기의 시각적 모델(Model) 프리팹입니다.")]
        [SerializeField] private GameObject m_modelPrefab;

        [Tooltip("공격 시 발생할 파티클 등 이펙트 프리팹입니다.")]
        [SerializeField] private GameObject m_effectPrefab;

        #endregion

        #region 성장 및 레벨업

        [Header("성장 및 레벨업")]
        [Tooltip("진화에 필요한 패시브 아이템 코드입니다. (예: PS_COLLAR)")]
        [SerializeField] private string m_evolutionItemCode;

        [Tooltip("레벨별 강화 정보 목록입니다.")]
        [SerializeField] private List<WeaponUpgradeData> m_upgrades;

        #endregion

        #region 공개 프로퍼티 (Getter & Setter)

        public string SkillCode { get => m_skillCode; set => m_skillCode = value; }
        public string WeaponName { get => m_weaponName; set => m_weaponName = value; }
        public Sprite Icon { get => m_icon; set => m_icon = value; }
        public string Description { get => m_description; set => m_description = value; }

        public float BaseAttackPower { get => m_baseAttackPower; set => m_baseAttackPower = value; }
        public float BaseCoolTime { get => m_baseCoolTime; set => m_baseCoolTime = value; }
        public float BaseAttackSpeed { get => m_baseAttackSpeed; set => m_baseAttackSpeed = value; }
        public float BaseAttackRange { get => m_baseAttackRange; set => m_baseAttackRange = value; }
        public float BaseDuration { get => m_baseDuration; set => m_baseDuration = value; }
        public int BaseProjectileCount { get => m_baseProjectileCount; set => m_baseProjectileCount = value; }

        public GameObject ProjectilePrefab { get => m_projectilePrefab; set => m_projectilePrefab = value; }
        public GameObject ModelPrefab { get => m_modelPrefab; set => m_modelPrefab = value; }
        public GameObject EffectPrefab { get => m_effectPrefab; set => m_effectPrefab = value; }

        public string EvolutionItemCode { get => m_evolutionItemCode; set => m_evolutionItemCode = value; }
        public List<WeaponUpgradeData> Upgrades { get => m_upgrades; set => m_upgrades = value; }

        #endregion
    }

    /// <summary>
    /// 무기의 레벨별 강화 정보를 저장하는 클래스입니다.
    /// </summary>
    [System.Serializable]
    public class WeaponUpgradeData
    {
        [Tooltip("강화 레벨입니다.")]
        public int Level;

        [Tooltip("강화에 대한 설명입니다.")]
        public string Description;

        [Tooltip("강화 시 변경될 스탯 정보 목록입니다.")]
        public List<StatModification> Modifications;
    }
}
