using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

/// <summary>
/// LogManager의 인스펙터 UI를 커스터마이징하여 가독성과 사용성을 개선합니다.
/// </summary>
[CanEditMultipleObjects]
[CustomEditor(typeof(LogManager))]
public class LogManagerEditor : Editor
{
    private SerializedProperty m_enableDebugLogProp;
    private SerializedProperty m_enableErrorLogProp;
    private SerializedProperty m_enableWarningLogProp;
    private SerializedProperty m_logCategoryEnablesProp;

    private void OnEnable()
    {
        // SerializedProperty를 미리 찾아 캐싱합니다.
        m_enableDebugLogProp = serializedObject.FindProperty("m_enableDebugLog");
        m_enableErrorLogProp = serializedObject.FindProperty("m_enableErrorLog");
        m_enableWarningLogProp = serializedObject.FindProperty("m_enableWarningLog");
        m_logCategoryEnablesProp = serializedObject.FindProperty("m_logCategoryEnables");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 전역 로그 설정 UI
        EditorGUILayout.PropertyField(m_enableDebugLogProp, new GUIContent("전체 디버그 로그 활성화"));
        EditorGUILayout.PropertyField(m_enableErrorLogProp, new GUIContent("전체 오류 로그 활성화"));
        EditorGUILayout.PropertyField(m_enableWarningLogProp, new GUIContent("전체 경고 로그 활성화"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("카테고리별 로그 활성화", EditorStyles.boldLabel);

        // LogCategory 열거형의 모든 값을 가져옵니다.
        var categories = Enum.GetValues(typeof(LogManager.LogCategory)).Cast<LogManager.LogCategory>().ToArray();

        // 리스트의 크기가 열거형의 멤버 수와 일치하는지 확인하고, 다르면 조정합니다.
        if (m_logCategoryEnablesProp.arraySize != categories.Length)
        {
            m_logCategoryEnablesProp.arraySize = categories.Length;
        }

        // 각 카테고리에 대해 토글 UI를 한 줄에 표시합니다.
        for (int i = 0; i < categories.Length; i++)
        {
            SerializedProperty element = m_logCategoryEnablesProp.GetArrayElementAtIndex(i);
            EditorGUILayout.PropertyField(element, new GUIContent(categories[i].ToString()));
        }

        // 변경 사항을 적용합니다.
        serializedObject.ApplyModifiedProperties();
    }
}