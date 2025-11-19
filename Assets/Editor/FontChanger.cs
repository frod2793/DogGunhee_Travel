using UnityEngine;
using UnityEditor;
using TMPro;

public class FontChanger : EditorWindow
{
    // 변경하고자 하는 새로운 폰트 애셋을 할당할 변수
    private TMP_FontAsset newFont;

    // 에디터 윈도우를 열기 위한 메뉴 아이템 추가
    [MenuItem("Tools/TMP Font Changer")]
    public static void ShowWindow()
    {
        GetWindow<FontChanger>("TMP Font Changer");
    }

    // 에디터 윈도우의 GUI를 그리는 함수
    private void OnGUI()
    {
        GUILayout.Label("모든 TMP 텍스트의 폰트를 변경합니다.", EditorStyles.boldLabel);
        
        // 사용자가 새 폰트를 드래그 앤 드롭할 수 있는 필드
        newFont = (TMP_FontAsset)EditorGUILayout.ObjectField("New Font Asset", newFont, typeof(TMP_FontAsset), false);

        // 버튼을 눌렀을 때 폰트 변경 함수 실행
        if (GUILayout.Button("현재 씬의 모든 폰트 변경"))
        {
            if (newFont != null)
            {
                ChangeFontsInScene();
            }
            else
            {
                EditorUtility.DisplayDialog("경고", "새로운 폰트 애셋을 먼저 지정해주세요.", "확인");
            }
        }
    }

    // 실제 폰트를 변경하는 로직
    private void ChangeFontsInScene()
    {
        // 현재 씬에 있는 모든 TextMeshProUGUI 컴포넌트를 찾음 (비활성화된 오브젝트 포함)
        TextMeshProUGUI[] allTmpTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int changedCount = 0;

        foreach (TextMeshProUGUI tmp in allTmpTexts)
        {
            // 폰트를 변경하기 전에 Undo(실행 취소) 기능을 위해 현재 상태를 기록
            Undo.RecordObject(tmp, "Change Font");
            tmp.font = newFont;
            changedCount++;
        }

        // 월드 스페이스의 TextMeshPro 컴포넌트도 변경
        TextMeshPro[] allWorldTmpTexts = FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TextMeshPro tmp in allWorldTmpTexts)
        {
            Undo.RecordObject(tmp, "Change Font");
            tmp.font = newFont;
            changedCount++;
        }

        EditorUtility.DisplayDialog("완료", $"{changedCount}개의 텍스트 오브젝트의 폰트가 변경되었습니다.", "확인");
    }
}