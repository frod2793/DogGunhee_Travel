using UnityEngine;
using UnityEditor;
using Cysharp.Threading.Tasks;

namespace Tests
{
    /// <summary>
    /// [설명]: InGameAutoTester 컴포넌트를 위한 커스텀 인스펙터 에디터 클래스입니다.
    /// 에디터 상에서 버튼을 통해 테스트 기능을 즉시 실행할 수 있게 합니다.
    /// </summary>
    [CustomEditor(typeof(InGameAutoTester))]
    public class InGameAutoTesterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // 기본 인스펙터 속성 출력
            DrawDefaultInspector();

            InGameAutoTester tester = (InGameAutoTester)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("테스트 제어 (런타임 전용)", EditorStyles.boldLabel);

            // 0. 테스트 모드 토글
            Color defaultColor = GUI.color;
            bool isTestMode = (bool)tester.GetType().GetField("m_isTestMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(tester);
            
            GUI.color = isTestMode ? Color.green : Color.gray;
            if (GUILayout.Button(isTestMode ? "테스트 모드 활성화 중 (OFF 클릭)" : "테스트 모드 비활성화 중 (ON 클릭)", GUILayout.Height(40)))
            {
                tester.ToggleTestMode(!isTestMode).Forget();
            }
            GUI.color = defaultColor;

            EditorGUILayout.Space();

            // 1. 레벨업 버튼
            if (GUILayout.Button("강제 레벨업 (F8 대체)", GUILayout.Height(30)))
            {
                tester.TriggerLevelUp();
            }

            // 2. 사망 버튼
            if (GUILayout.Button("강제 사망 (F9 대체)", GUILayout.Height(30)))
            {
                tester.TriggerDeath();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("경제 제어", EditorStyles.boldLabel);

            // 3. 코인 추가 버튼
            int addCoinAmount = (int)tester.GetType().GetField("m_defaultAddCoinAmount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(tester);
            if (GUILayout.Button($"코인 {addCoinAmount}개 추가", GUILayout.Height(30)))
            {
                tester.AddPlayerCoin(addCoinAmount);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("무기 전수 테스트", EditorStyles.boldLabel);

            // 3. 시퀀스 테스트 버튼
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("무기 시퀀스 테스트 시작 (F10 대체)", GUILayout.Height(30)))
            {
                tester.TriggerSequenceTest();
            }

            // 4. 정밀 테스트 버튼
            if (GUILayout.Button("무기 정밀 테스트 시작 (F11 대체)", GUILayout.Height(30)))
            {
                tester.TriggerPrecisionTest();
            }
            GUI.enabled = true;

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("테스트 기능은 플레이 모드에서만 동작합니다.", MessageType.Info);
            }
        }
    }
}
