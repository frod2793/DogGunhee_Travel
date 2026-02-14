using UnityEngine;
using System.Collections.Generic;

namespace InGame.Weapon.Base
{
    /// <summary>
    /// [설명]: 무기의 구성 정보와 기본 스탯을 저장하는 ScriptableObject 기반 데이터 클래스입니다.
    /// 에디터에서 기획 데이터를 관리하고 런타임에 참조할 수 있도록 설계되었습니다.
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

        [Tooltip("적 타격 시 기본 경직 시간(초)입니다.")]
        [SerializeField] private float m_baseStunDuration = 0.2f;

        #endregion

        #region 프리팹 참조

        [Header("에셋 참조")]
        [Tooltip("발사될 투사체(Projectile) 프리팹입니다.")]
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

        #region 공개 프로퍼티

        /// <summary> [설명]: 스킬 고유 코드 </summary>
        public string SkillCode { get => m_skillCode; set => m_skillCode = value; }

        /// <summary> [설명]: UI 표시용 무기 이름 </summary>
        public string WeaponName { get => m_weaponName; set => m_weaponName = value; }

        /// <summary> [설명]: UI 표시용 아이콘 스프라이트 </summary>
        public Sprite Icon { get => m_icon; set => m_icon = value; }

        /// <summary> [설명]: 무기 상세 설명 </summary>
        public string Description { get => m_description; set => m_description = value; }

        /// <summary> [설명]: 기본 공격력 </summary>
        public float BaseAttackPower { get => m_baseAttackPower; set => m_baseAttackPower = value; }

        /// <summary> [설명]: 기본 쿨타임 (초) </summary>
        public float BaseCoolTime { get => m_baseCoolTime; set => m_baseCoolTime = value; }

        /// <summary> [설명]: 기본 공격 속도 배율 </summary>
        public float BaseAttackSpeed { get => m_baseAttackSpeed; set => m_baseAttackSpeed = value; }

        /// <summary> [설명]: 기본 공격 사거리 </summary>
        public float BaseAttackRange { get => m_baseAttackRange; set => m_baseAttackRange = value; }

        /// <summary> [설명]: 기본 지속 시간 (초) </summary>
        public float BaseDuration { get => m_baseDuration; set => m_baseDuration = value; }

        /// <summary> [설명]: 기본 투사체 개수 </summary>
        public int BaseProjectileCount { get => m_baseProjectileCount; set => m_baseProjectileCount = value; }

        /// <summary> [설명]: 기본 스턴 지속 시간 (초) </summary>
        public float BaseStunDuration { get => m_baseStunDuration; set => m_baseStunDuration = value; }

        /// <summary> [설명]: 투사체 프리팹 참조 </summary>
        public GameObject ProjectilePrefab { get => m_projectilePrefab; set => m_projectilePrefab = value; }

        /// <summary> [설명]: 무기 모델 프리팹 참조 </summary>
        public GameObject ModelPrefab { get => m_modelPrefab; set => m_modelPrefab = value; }

        /// <summary> [설명]: 이펙트 프리팹 참조 </summary>
        public GameObject EffectPrefab { get => m_effectPrefab; set => m_effectPrefab = value; }

        /// <summary> [설명]: 진화 아이템 코드 </summary>
        public string EvolutionItemCode { get => m_evolutionItemCode; set => m_evolutionItemCode = value; }

        /// <summary> [설명]: 레벨별 강화 데이터 목록 </summary>
        public List<WeaponUpgradeData> Upgrades { get => m_upgrades; set => m_upgrades = value; }

        #endregion
    }

    /// <summary>
    /// [설명]: 무기의 레벨별 강화 정보를 저장하는 순수 데이터 클래스입니다.
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
