using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using InGame.Weapon.Base;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace InGame
{  [CreateAssetMenu(fileName = "NewSkillDatabase", menuName = "Game/Weapon/SkillDatabase")]
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

            if (allSkills == null) allSkills = new List<SkillData>();

#if UNITY_EDITOR
            // [자동 탐색] 프로젝트 내의 모든 WeaponDataSO 에셋을 미리 검색하여 매핑
            var weaponMap = new Dictionary<string, WeaponDataSO>(StringComparer.OrdinalIgnoreCase);
            string[] guids = AssetDatabase.FindAssets("t:WeaponDataSO");
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                WeaponDataSO weapon = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(path);
                if (weapon == null) continue;

                // 1. SkillCode가 입력된 경우 우선 매핑
                if (!string.IsNullOrEmpty(weapon.SkillCode))
                {
                    weaponMap[weapon.SkillCode.Trim()] = weapon;
                }
                // 2. SkillCode가 비어있는 경우 파일명 기반 매칭 시도
                else
                {
                    string filename = System.IO.Path.GetFileNameWithoutExtension(path);
                    foreach (var key in xmlData.Keys)
                    {
                        // 규칙: 파일명이 키와 같거나, 접미사를 제외한 이름이 키와 연관됨
                        // 예: "BoneWeaponData" -> "BONE" (WP_BONE)
                        string coreKey = key.Replace("WP_", "").Replace("PS_", "");
                        if (filename.Equals(key, StringComparison.OrdinalIgnoreCase) || 
                            filename.StartsWith(coreKey, StringComparison.OrdinalIgnoreCase) ||
                            filename.Contains(coreKey))
                        {
                            weapon.SkillCode = key; // 자동 기입
                            weaponMap[key] = weapon;
                            EditorUtility.SetDirty(weapon);
                            Debug.Log($"[SkillDatabase] Auto-assigned SkillCode '{key}' to WeaponDataSO '{filename}'");
                            break;
                        }
                    }
                }
            }
#endif

            // XML 데이터를 기반으로 allSkills 리스트 동기화 (없으면 생성, 있으면 업데이트)
            foreach (var kvp in xmlData)
            {
                string skillCode = kvp.Key;
                SkillXmlData data = kvp.Value;

                // 기존 항목 검색
                var skill = allSkills.Find(s => s.skillCode == skillCode);
                if (skill == null)
                {
                    skill = new SkillData { skillCode = skillCode };
                    allSkills.Add(skill);
                    Debug.Log($"[SkillDatabase] Added new SkillData entry for '{skillCode}'");
                }

                // 공통 필드 업데이트
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

#if UNITY_EDITOR
                // WeaponDataSO 연결 자동화 및 자동 생성
                if (weaponMap.TryGetValue(skillCode, out WeaponDataSO matchedWeapon))
                {
                    skill.weaponData = matchedWeapon;
                    SyncWeaponData(matchedWeapon, data);
                    EditorUtility.SetDirty(matchedWeapon);
                }
                else if (skill.skillType == SkillType.Weapon)
                {
                    // 매칭되는 에셋이 없는 경우 자동 생성
                    string directory = "Assets/_Game/ScriptableObjects/Weapons";
                    if (!AssetDatabase.IsValidFolder(directory))
                    {
                        if (!AssetDatabase.IsValidFolder("Assets/_Game/ScriptableObjects"))
                        {
                            AssetDatabase.CreateFolder("Assets/_Game", "ScriptableObjects");
                        }
                        AssetDatabase.CreateFolder("Assets/_Game/ScriptableObjects", "Weapons");
                    }

                    string assetPath = $"{directory}/{skillCode}Data.asset";
                    WeaponDataSO newWeapon = ScriptableObject.CreateInstance<WeaponDataSO>();
                    newWeapon.SkillCode = skillCode;
                    
                    // 파일 생성 전 데이터 먼저 동기화
                    SyncWeaponData(newWeapon, data);
                    
                    AssetDatabase.CreateAsset(newWeapon, assetPath);
                    skill.weaponData = newWeapon;
                    
                    EditorUtility.SetDirty(newWeapon);
                    
                    Debug.Log($"[SkillDatabase] Auto-created new WeaponDataSO for '{skillCode}' at '{assetPath}'");
                }
#endif
            }

#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
                Debug.Log($"[SkillDatabase] XML Sync & Persistence Complete.");
            };
#endif
        }

        private void SyncWeaponData(WeaponDataSO weapon, SkillXmlData data)
        {
            // 기본 정보 동기화
            weapon.WeaponName = data.Name;
            weapon.Description = data.Description;

            // 기본 스탯 동기화
            if (data.BaseStats != null)
            {
                if (data.BaseStats.TryGetValue("Damage", out float damage)) weapon.BaseAttackPower = damage;
                if (data.BaseStats.TryGetValue("Cooldown", out float cooldown)) weapon.BaseCoolTime = cooldown;
                if (data.BaseStats.TryGetValue("AttackSpeed", out float speed)) weapon.BaseAttackSpeed = speed;
                if (data.BaseStats.TryGetValue("WeaponSize", out float size)) weapon.BaseAttackRange = size;
                if (data.BaseStats.TryGetValue("Duration", out float duration)) weapon.BaseDuration = duration;
                if (data.BaseStats.TryGetValue("ProjectileCount", out float count)) weapon.BaseProjectileCount = (int)count;
            }

            // 진화 아이템 코드 동기화
            weapon.EvolutionItemCode = data.UpgradeItemCode;

            // 업그레이드 정보 동기화
            if (data.Upgrades != null)
            {
                if (weapon.Upgrades == null) weapon.Upgrades = new List<WeaponUpgradeData>();

                foreach (var xmlUpgrade in data.Upgrades)
                {
                    int level = xmlUpgrade.Key;
                    var mods = xmlUpgrade.Value;

                    var existingUpgrade = weapon.Upgrades.Find(u => u.Level == level);
                    if (existingUpgrade == null)
                    {
                        existingUpgrade = new WeaponUpgradeData { Level = level };
                        weapon.Upgrades.Add(existingUpgrade);
                    }

                    existingUpgrade.Modifications = mods;

                    if (string.IsNullOrEmpty(existingUpgrade.Description))
                    {
                        string desc = "";
                        foreach (var mod in mods)
                        {
                            desc += $"{mod.StatName} {mod.Mode} {mod.Value}, ";
                        }
                        existingUpgrade.Description = desc.TrimEnd(' ', ',');
                    }
                }

                weapon.Upgrades.Sort((a, b) => a.Level.CompareTo(b.Level));
            }
        }
    }
}