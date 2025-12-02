using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using InGame.Manager;

namespace InGame.Editor
{
    [CustomEditor(typeof(GameManager))]
    public class GameManagerEditor : UnityEditor.Editor
    {
        private SkillDatabase m_skillDatabase;
        private List<SkillData> m_weaponSkills;
        private string[] m_weaponNames;
        private int m_selectedWeaponIndex = 0;

        private void OnEnable()
        {
            FindSkillDatabase();
        }

        public override void OnInspectorGUI()
        {
            // 기본 인스펙터 UI를 먼저 그립니다.
            base.OnInspectorGUI();

            // 대상 GameManager 인스턴스를 가져옵니다.
            GameManager gameManager = (GameManager)target;

            // SkillDatabase를 찾았는지 확인합니다.
            if (m_skillDatabase == null)
            {
                EditorGUILayout.HelpBox("프로젝트에서 SkillDatabase를 찾을 수 없습니다.", MessageType.Warning);
                if (GUILayout.Button("SkillDatabase 다시 찾기"))
                {
                    FindSkillDatabase();
                }
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("테스트 무기 추가", EditorStyles.boldLabel);

            // 드롭다운 메뉴와 버튼을 가로로 배치합니다.
            EditorGUILayout.BeginHorizontal();
            m_selectedWeaponIndex = EditorGUILayout.Popup(m_selectedWeaponIndex, m_weaponNames);

            if (GUILayout.Button("추가", GUILayout.Width(50)))
            {
                AddTestWeapon(gameManager);
            }
            EditorGUILayout.EndHorizontal();

            // 리스트 초기화 버튼
            if (GUILayout.Button("테스트 무기 리스트 초기화"))
            {
                ClearTestWeapons(gameManager);
            }
        }

        /// <summary>
        /// 프로젝트에서 SkillDatabase 에셋을 찾아 무기 목록을 로드합니다.
        /// </summary>
        private void FindSkillDatabase()
        {
            string[] guids = AssetDatabase.FindAssets("t:SkillDatabase");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                m_skillDatabase = AssetDatabase.LoadAssetAtPath<SkillDatabase>(path);

                // XML 데이터가 최신 상태가 아닐 수 있으므로 로드합니다.
                m_skillDatabase.LoadDataFromXML();

                // 무기 스킬만 필터링하여 목록을 만듭니다.
                m_weaponSkills = m_skillDatabase.allSkills
                    .Where(skill => skill.skillType == SkillType.Weapon && !string.IsNullOrEmpty(skill.skillName))
                    .ToList();
                
                m_weaponNames = m_weaponSkills.Select(skill => skill.skillName).ToArray();
            }
            else
            {
                Debug.LogWarning("[GameManagerEditor] 프로젝트에서 SkillDatabase 에셋을 찾지 못했습니다.");
            }
        }

        /// <summary>
        /// 선택된 무기를 GameManager의 테스트 리스트에 추가합니다.
        /// </summary>
        private void AddTestWeapon(GameManager gameManager)
        {
            if (m_weaponSkills != null && m_selectedWeaponIndex < m_weaponSkills.Count)
            {
                SkillData selectedSkill = m_weaponSkills[m_selectedWeaponIndex];

                // 중복 추가 방지
                if (!gameManager.TestWeapons.Any(w => w.skillCode == selectedSkill.skillCode))
                {
                    gameManager.TestWeapons.Add(selectedSkill);
                    EditorUtility.SetDirty(gameManager); // 변경사항 저장
                    Debug.Log($"[GameManagerEditor] 테스트 무기 추가: {selectedSkill.skillName}");
                }
                else
                {
                    Debug.LogWarning($"[GameManagerEditor] '{selectedSkill.skillName}'은(는) 이미 리스트에 있습니다.");
                }
            }
        }

        /// <summary>
        /// GameManager의 테스트 무기 리스트를 초기화합니다.
        /// </summary>
        private void ClearTestWeapons(GameManager gameManager)
        {
            if (gameManager.TestWeapons.Count > 0)
            {
                gameManager.TestWeapons.Clear();
                EditorUtility.SetDirty(gameManager);
                Debug.Log("[GameManagerEditor] 테스트 무기 리스트가 초기화되었습니다.");
            }
        }
    }
}