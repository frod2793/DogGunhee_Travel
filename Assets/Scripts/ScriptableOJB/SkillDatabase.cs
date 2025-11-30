using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

namespace Vamser_like
{
    public class SkillDatabase : ScriptableObject
    {
        private struct SkillXmlData
        {
            public string Name;
            public SkillType Type;
            public string UpgradeItemCode;
            public string FlavorText;
            public string Description;
            public string Stats;
            public string WeaponAddressableKey;
            public Dictionary<string, float> BaseStats;
            public EvolutionData EvolutionInfo;
            public Dictionary<int, List<StatModification>> Upgrades;
        }

        [Tooltip("게임 내에서 사용 가능한 모든 스킬의 목록입니다.")]
        public List<SkillData> allSkills;

        private const string k_DescriptionFilePath = "Data/SkillDescription";

        private void OnEnable()
        {
            LoadDataFromXML();
        }

        public void LoadDataFromXML()
        {
            var xmlData = new Dictionary<string, SkillXmlData>();
            TextAsset xmlFile = Resources.Load<TextAsset>(k_DescriptionFilePath);

            if (xmlFile == null) return;

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlFile.text);

            XmlNodeList skillNodes = xmlDoc.SelectNodes("SkillDescriptions/Skill");
            foreach (XmlNode skillNode in skillNodes)
            {
                try
                {
                    string skillCode = skillNode.Attributes["key"].Value.Trim();
                    string name = skillNode["Name"].InnerText.Trim();
                    string typeStr = skillNode["SkillType"].InnerText;
                    string upgradeItemCode = skillNode["UpgradeItemCode"]?.InnerText.Trim() ?? string.Empty;
                    string flavorText = skillNode["FlavorText"].InnerText;
                    string description = skillNode["Description"].InnerText;
                    string stats = skillNode["Stats"].InnerText;
                    string addressableKey = skillNode["WeaponAddressableKey"]?.InnerText ?? string.Empty;

                    var baseStats = new Dictionary<string, float>();
                    XmlNode statValuesNode = skillNode.SelectSingleNode("StatValues");
                    if (statValuesNode != null)
                    {
                        foreach (XmlNode statNode in statValuesNode.ChildNodes)
                        {
                            string statName = statNode.Attributes["name"].Value;
                            float value = float.Parse(statNode.InnerText);
                            baseStats[statName] = value;
                        }
                    }

                    EvolutionData evolutionInfo = null;
                    XmlNode evolutionNode = skillNode.SelectSingleNode("Evolution");
                    if (evolutionNode != null)
                    {
                        evolutionInfo = new EvolutionData
                        {
                            Name = evolutionNode["Name"]?.InnerText,
                            FlavorText = evolutionNode["FlavorText"]?.InnerText
                        };
                    }
                    
                    var upgrades = new Dictionary<int, List<StatModification>>();
                    XmlNode upgradesNode = skillNode.SelectSingleNode("Upgrades");
                    if (upgradesNode != null)
                    {
                        foreach (XmlNode levelNode in upgradesNode.SelectNodes("Level"))
                        {
                            int levelNum = int.Parse(levelNode.Attributes["num"].Value);
                            string statName = levelNode.Attributes["stat"].Value;
                            float value = float.Parse(levelNode.Attributes["value"].Value);
                            ModificationMode mode = (ModificationMode)Enum.Parse(typeof(ModificationMode), levelNode.Attributes["mode"].Value, true);

                            var modification = new StatModification { StatName = statName, Value = value, Mode = mode };

                            if (!upgrades.ContainsKey(levelNum))
                            {
                                upgrades[levelNum] = new List<StatModification>();
                            }
                            upgrades[levelNum].Add(modification);
                        }
                    }

                    if (Enum.TryParse<SkillType>(typeStr, true, out SkillType skillType))
                    {
                        if (!xmlData.ContainsKey(skillCode))
                        {
                            xmlData.Add(skillCode, new SkillXmlData 
                            { 
                                Name = name,
                                Type = skillType, 
                                UpgradeItemCode = upgradeItemCode,
                                FlavorText = flavorText,
                                Description = description,
                                Stats = stats,
                                WeaponAddressableKey = addressableKey,
                                BaseStats = baseStats,
                                EvolutionInfo = evolutionInfo,
                                Upgrades = upgrades
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SkillDatabase] XML 파싱 중 오류 발생: {ex.Message}");
                }
            }

            foreach (var skill in allSkills)
            {
                if (!string.IsNullOrEmpty(skill.skillCode) && xmlData.TryGetValue(skill.skillCode.Trim(), out SkillXmlData data))
                {
                    skill.skillName = data.Name;
                    skill.skillType = data.Type;
                    skill.upgradeItemCode = data.UpgradeItemCode;
                    skill.flavorText = data.FlavorText;
                    skill.skillDescription = data.Description;
                    skill.stats = data.Stats;
                    skill.weaponAddressableKey = data.WeaponAddressableKey;
                    skill.BaseStats = data.BaseStats;
                    skill.EvolutionInfo = data.EvolutionInfo;
                    skill.Upgrades = data.Upgrades;
                }
            }
        }
    }
}