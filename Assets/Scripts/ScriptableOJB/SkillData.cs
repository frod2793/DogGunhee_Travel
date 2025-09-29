using UnityEngine;

namespace DogGuns_Games
{
    /// <summary>
    /// 레벨업 시 선택 가능한 스킬(무기, 장신구, 업그레이드)의 데이터 구조체입니다.
    /// </summary>
    [System.Serializable]
    public class SkillData
    {
        [Tooltip("스킬을 식별하는 고유 코드입니다.")]
        public int skillCode;

        [Tooltip("UI에 표시될 스킬의 이름입니다.")]
        public string skillName;

        [Tooltip("UI에 표시될 스킬의 상세 설명입니다."), TextArea(3, 5)]
        public string skillDescription;

        [Tooltip("UI에 표시될 스킬의 아이콘 이미지입니다.")]
        public Sprite skillIcon;
    }
}