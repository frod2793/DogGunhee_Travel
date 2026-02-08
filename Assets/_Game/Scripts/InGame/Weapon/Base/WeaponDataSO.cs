using UnityEngine;
using System.Collections.Generic;

namespace InGame.Weapon.Base
{
    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "Game/Weapon/WeaponData")]
    public class WeaponDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        [Tooltip("XML 데이터와 연결되는 고유 키값입니다. (예: WP_BONE)")]
        public string SkillCode;

        [Tooltip("게임 내 UI에 표시될 무기의 이름입니다.")]
        public string WeaponName;

        [Tooltip("스킬 인벤토리나 룰렛 등에 표시될 아이콘 이미지입니다.")]
        public Sprite Icon;

        [Tooltip("무기에 대한 상세 설명입니다.")]
        [TextArea] public string Description;

        [Header("기본 스탯")]
        [Tooltip("무기의 기본 공격력입니다.")]
        public float BaseAttackPower;

        [Tooltip("무기의 기본 재사용 대기시간(초)입니다.")]
        public float BaseCoolTime;

        [Tooltip("공격 속도 배율입니다. (기본값: 1.0)")]
        public float BaseAttackSpeed = 1.0f;

        [Tooltip("공격 범위 또는 투사체 크기 등에 사용되는 거리 수치입니다.")]
        public float BaseAttackRange;

        [Tooltip("지속성 무기의 경우 효과가 유지되는 시간(초)입니다.")]
        public float BaseDuration; 

        [Tooltip("한 번에 발사되거나 생성되는 투사체의 개수입니다.")]
        public int BaseProjectileCount = 1;

        [Header("프리팹 참조")]
        [Tooltip("발사될 투사체(Projectile) 프리팹입니다.")]
        public GameObject ProjectilePrefab;

        [Tooltip("무기의 시각적 모델(Model) 프리팹입니다.")]
        public GameObject ModelPrefab;

        [Tooltip("공격 시 발생할 파티클 등 이펙트 프리팹입니다.")]
        public GameObject EffectPrefab;

        [Header("성장 테이블")]
        [Tooltip("진화에 필요한 패시브 아이템 코드입니다. (예: PS_COLLAR)")]
        public string EvolutionItemCode;

        [Tooltip("레벨별 강화 정보 목록입니다.")]
        public List<WeaponUpgradeData> Upgrades;
    }

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
