using UnityEngine;
using UnityEditor;

namespace InGame.Editor
{
    /// <summary>
    /// SkillDatabase의 Unity 인스펙터를 커스터마이징하는 클래스입니다.
    /// </summary>
    [CustomEditor(typeof(SkillDatabase))]
    public class SkillDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // 기본 인스펙터 UI를 그립니다 (allSkills 리스트 등).
            base.OnInspectorGUI();

            // 대상 SkillDatabase 객체를 가져옵니다.
            SkillDatabase skillDatabase = (SkillDatabase)target;

            // 인스펙터에 버튼을 추가합니다.
            if (GUILayout.Button("로컬 데이터 갱신 (JSON)"))
            {
                // SkillDatabase의 데이터 로드 메서드를 호출합니다.
                skillDatabase.LoadFromLocalCache();

                // 변경된 데이터를 디스크에 저장하도록 Unity에 알립니다.
                EditorUtility.SetDirty(skillDatabase);
                AssetDatabase.SaveAssets();

                Debug.Log("[SkillDatabaseEditor] SkillDatabase가 로컬 JSON 데이터로 갱신되었습니다.");
            }
        }
    }
}