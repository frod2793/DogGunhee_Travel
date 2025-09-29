using System.Collections.Generic;
using UnityEngine;

namespace DogGuns_Games
{
    /// <summary>
    /// 게임에 등장하는 모든 스킬 데이터를 관리하는 ScriptableObject입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillDatabase", menuName = "GameData/Skill Database")]
    public class SkillDatabase : ScriptableObject
    {
        [Tooltip("게임 내에서 사용 가능한 모든 스킬의 목록입니다.")]
        public List<SkillData> allSkills;
    }
}