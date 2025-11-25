using UnityEngine;

namespace DogGuns_Games
{
    /// <summary>
    /// 스킬의 종류를 정의합니다. (패시브, 무기 등)
    /// </summary>
    public enum SkillType
    {
        Passive, // 패시브 스킬 (스탯 강화 등)
        Weapon   // 무기 스킬 (새로운 무기 획득)
    }

    /// <summary>
    /// 레벨업 시 선택 가능한 스킬(무기, 장신구, 업그레이드)의 데이터 구조체입니다.
    /// </summary>
    [System.Serializable]
    public class SkillData
    {
        [Tooltip("스킬을 식별하는 고유 코드입니다.")]
        public int skillCode;

        [Tooltip("스킬의 종류를 나타냅니다. (패시브 또는 무기)")]
        public SkillType skillType;

        [Tooltip("UI에 표시될 스킬의 이름입니다.")]
        public string skillName;

        [Tooltip("UI에 표시될 스킬의 상세 설명입니다."), TextArea(3, 5)]
        public string skillDescription;

        [Tooltip("룰렛 UI에 표시될 스킬의 아이콘 이미지입니다.")]
        public Sprite skillIcon;
        
        [Tooltip("상단 무기/장신구 목록 UI에 표시될 썸네일 이미지입니다.")]
        public Sprite Thumnail;
        
        [Header("Weapon-Specific")]
        [Tooltip("스킬 타입이 'Weapon'일 경우, 로드할 무기 프리팹의 Addressable 키입니다.")]
        public string weaponAddressableKey;
    }
}