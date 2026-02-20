using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using InGame.Weapon.Base;
using InGame.Data;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace InGame
{  [CreateAssetMenu(fileName = "NewSkillDatabase", menuName = "Game/Weapon/SkillDatabase")]
    public class SkillDatabase : ScriptableObject
    {
        #region 에디터 설정
        [Tooltip("게임 내에서 사용 가능한 모든 스킬의 목록입니다.")]
        public List<SkillData> allSkills;
        #endregion

        #region 내부 필드
        private const string k_DataType = "Skill";
        #endregion

        #region 유니티 생명주기
        private void OnEnable()
        {
            // 초기화 시 로컬 캐시에서 로드 시도
            LoadFromLocalCache();
        }
        #endregion

        #region 공개 메서드
        /// <summary>
        /// [설명]: 로컬 캐시(PersistentDataPath)에서 데이터를 로드합니다.
        /// </summary>
        public void LoadFromLocalCache()
        {
            string jsonData = GetCachedJson();
            if (!string.IsNullOrEmpty(jsonData))
            {
                LoadDataFromJSON(jsonData);
            }
        }

        /// <summary>
        /// [설명]: 외부(RemoteDataService 등)에서 받은 JSON 문자열을 기반으로 데이터를 갱신합니다.
        /// </summary>
        public void LoadDataFromJSON(string jsonData)
        {
            if (string.IsNullOrEmpty(jsonData)) return;

            try
            {
                var wrapper = JsonUtility.FromJson<InGame.Data.SheetDataWrapper<InGame.Data.SkillDescriptionDTO>>(jsonData);
                if (wrapper == null || wrapper.data == null) return;

                if (allSkills == null) allSkills = new List<SkillData>();

#if UNITY_EDITOR
                // 에디터에서 무기 데이터 에셋 매핑 준비
                var weaponMap = PrepareWeaponMap(wrapper.data);
#endif

                foreach (var dto in wrapper.data)
                {
#if UNITY_EDITOR
                    UpdateSkillFromDTO(dto, weaponMap);
#else
                    UpdateSkillFromDTO(dto);
#endif
                }

#if UNITY_EDITOR
                // 에디터 전용: 변경사항 저장
                SaveEditorChanges();
#endif
                Debug.Log($"<color=white>[SkillDatabase]</color> JSON 데이터 로드 및 에셋 동기화 완료 (스킬 총 <b>{wrapper.data.Count}</b>개)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SkillDatabase] JSON 파싱 중 오류 발생: {ex.Message}");
            }
        }
        #endregion

        #region 내부 로직
#if UNITY_EDITOR
        private void UpdateSkillFromDTO(InGame.Data.SkillDescriptionDTO dto, Dictionary<string, WeaponDataSO> weaponMap)
#else
        private void UpdateSkillFromDTO(InGame.Data.SkillDescriptionDTO dto)
#endif
        {
            string skillCode = dto.key.Trim();
            var skill = allSkills.Find(s => s.skillCode == skillCode);

            if (skill == null)
            {
                skill = new SkillData { skillCode = skillCode };
                allSkills.Add(skill);
            }

#if UNITY_EDITOR
            // 에디터에서 무기 데이터 에셋 자동 매칭
            if (skill.weaponData == null && weaponMap.TryGetValue(skillCode, out var matchedWeapon))
            {
                skill.weaponData = matchedWeapon;
                Debug.Log($"[SkillDatabase] 키 기반 무기 에셋 자동 매칭됨: {skillCode}");
            }
#endif

            // 기본 필드 매핑
            skill.skillName = dto.Name.Trim();
            if (Enum.TryParse<SkillType>(dto.SkillType, true, out var type))
                skill.skillType = type;
            
            skill.upgradeItemCode = dto.UpgradeItemCode?.Trim() ?? string.Empty;
            skill.flavorText = dto.FlavorText;
            skill.skillDescription = dto.Description;
            skill.stats = dto.Stats;
            skill.weaponAddressableKey = dto.WeaponAddressableKey ?? string.Empty;

            // 스탯 딕셔너리 구축 및 WeaponDataSO (업그레이드 전 기본 스탯) 동기화
            skill.BaseStats.Clear();
            if (dto.StatValues != null)
            {
                foreach (var sv in dto.StatValues)
                {
                    skill.BaseStats[sv.name] = sv.value;
                    // 스탯 동기화 전용 처리 (SyncWeaponAsset 호출 전 임시 보관 등)
                }
            }

            // 진화 정보
            if (dto.Evolution != null)
            {
                skill.EvolutionInfo = new EvolutionData
                {
                    Name = dto.Evolution.Name,
                    FlavorText = dto.Evolution.FlavorText
                };
            }

            // 업그레이드 정보
            skill.Upgrades.Clear();
            if (dto.Upgrades != null)
            {
                foreach (var uDto in dto.Upgrades)
                {
                    if (!skill.Upgrades.ContainsKey(uDto.num))
                        skill.Upgrades[uDto.num] = new List<StatModification>();

                    if (Enum.TryParse<ModificationMode>(uDto.mode, true, out var mode))
                    {
                        skill.Upgrades[uDto.num].Add(new StatModification 
                        { 
                            StatName = uDto.stat, 
                            Value = uDto.value, 
                            Mode = mode 
                        });
                    }
                }
            }

            // 무기 데이터 에셋 동기화 (런타임 임시 반영 포함)
            if (skill.weaponData != null)
            {
                SyncWeaponAsset(skill.weaponData, dto);
            }
        }

        private string GetCachedJson()
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, "DataCache", $"{k_DataType}.json");
            if (System.IO.File.Exists(path))
            {
                return System.IO.File.ReadAllText(path);
            }
            return string.Empty;
        }

        private void SyncWeaponAsset(WeaponDataSO weapon, InGame.Data.SkillDescriptionDTO dto)
        {
            weapon.WeaponName = dto.Name;
            weapon.Description = dto.Description;

            // 기본 스탯 동기화
            foreach (var sv in dto.StatValues)
            {
                if (sv.name == "Damage") weapon.BaseAttackPower = sv.value;
                else if (sv.name == "Cooldown") weapon.BaseCoolTime = sv.value;
                else if (sv.name == "AttackSpeed") weapon.BaseAttackSpeed = sv.value;
                else if (sv.name == "Range") weapon.BaseAttackRange = sv.value;
                else if (sv.name == "Duration") weapon.BaseDuration = sv.value;
                else if (sv.name == "ProjectileCount") weapon.BaseProjectileCount = (int)sv.value;
            }

            weapon.EvolutionItemCode = dto.UpgradeItemCode;

            // 업그레이드 리스트 동기화
            if (dto.Upgrades != null)
            {
                if (weapon.Upgrades == null) weapon.Upgrades = new List<WeaponUpgradeData>();
                
                // 간단히 레벨별로 그룹화하여 갱신
                var grouped = dto.Upgrades.GroupBy(u => u.num);
                foreach (var group in grouped)
                {
                    int lv = group.Key;
                    var upgrade = weapon.Upgrades.Find(u => u.Level == lv);
                    if (upgrade == null)
                    {
                        upgrade = new WeaponUpgradeData { Level = lv };
                        weapon.Upgrades.Add(upgrade);
                    }

                    upgrade.Modifications = group.Select(u => {
                        Enum.TryParse<ModificationMode>(u.mode, true, out var m);
                        return new StatModification { StatName = u.stat, Value = u.value, Mode = m };
                    }).ToList();

                    // 설명 자동 생성
                    upgrade.Description = string.Join(", ", upgrade.Modifications.Select(m => $"{m.StatName} {m.Mode} {m.Value}"));
                }
                weapon.Upgrades.Sort((a,b) => a.Level.CompareTo(b.Level));
            }
#if UNITY_EDITOR
            EditorUtility.SetDirty(weapon);
#endif
        }
        #endregion

        #region 에디터 전용 로직
#if UNITY_EDITOR
        private Dictionary<string, WeaponDataSO> PrepareWeaponMap(List<InGame.Data.SkillDescriptionDTO> data)
        {
            var weaponMap = new Dictionary<string, WeaponDataSO>(StringComparer.OrdinalIgnoreCase);
            string[] guids = AssetDatabase.FindAssets("t:WeaponDataSO");
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                WeaponDataSO weapon = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(path);
                if (weapon == null) continue;

                if (!string.IsNullOrEmpty(weapon.SkillCode))
                {
                    weaponMap[weapon.SkillCode.Trim()] = weapon;
                }
            }
            return weaponMap;
        }

        private void SaveEditorChanges()
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
            };
        }
#endif
        #endregion
    }
}