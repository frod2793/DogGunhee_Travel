using System.Collections.Generic;
using UnityEngine;

namespace DogGuns_Games
{
    public enum SkillType
    {
        Passive,
        Weapon
    }

    [System.Serializable]
    public class SkillData
    {
        [Tooltip("XML 데이터와 연결하는 고유 키(Key)입니다. XML의 'key' 속성과 일치해야 합니다.")]
        public string skillCode;

        // 이 필드들은 XML에서 런타임에 로드됩니다.
        public string skillName;
        public SkillType skillType;
        public string upgradeItemCode; // [추가] 업그레이드에 필요한 아이템 코드
        public string flavorText;
        public string skillDescription;
        public string stats;
        public string weaponAddressableKey;

        [Tooltip("룰렛 UI에 표시될 스킬의 아이콘 이미지입니다.")]
        public Sprite skillIcon;

        [System.NonSerialized]
        public Dictionary<string, float> BaseStats = new Dictionary<string, float>();

        [System.NonSerialized]
        public EvolutionData EvolutionInfo;

        [System.NonSerialized]
        public Dictionary<int, List<StatModification>> Upgrades = new Dictionary<int, List<StatModification>>();
    }
}