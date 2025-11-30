using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace DogGuns_Games.Test.Editor
{
    /// <summary>
    /// TestManager의 인스펙터를 확장하여, 플레이 모드에서 위치를 저장하는 기능을 추가합니다.
    /// </summary>
    [CustomEditor(typeof(TestManager))]
    public class TestManagerEditor : UnityEditor.Editor
    {
        // 플레이 모드 종료 후 값을 적용하기 위한 임시 저장 변수
        private static Vector3? s_pendingHiddenPos;
        private static Vector3? s_pendingShownPos;

        private void OnEnable()
        {
            // 플레이 모드 상태 변경 이벤트를 구독합니다.
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            // 스크립트가 비활성화될 때 이벤트 구독을 해제합니다.
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        public override void OnInspectorGUI()
        {
            // 기본 인스펙터를 먼저 그립니다.
            DrawDefaultInspector();

            TestManager testManager = (TestManager)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Panel Position Saver", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서 패널을 열고/닫은 후, 각 상태의 위치를 저장하세요.", MessageType.Info);

                if (GUILayout.Button("Save Shown Position"))
                {
                    if (testManager.panelTransform != null)
                    {
                        // [수정] 위치 값을 임시 변수에 '캡처'만 합니다.
                        s_pendingShownPos = testManager.panelTransform.position;
                        Debug.Log($"Shown Position 캡처 완료: {s_pendingShownPos.Value}. 플레이 모드 종료 시 적용됩니다.");
                    }
                }

                if (GUILayout.Button("Save Hidden Position"))
                {
                    if (testManager.panelTransform != null)
                    {
                        // [수정] 위치 값을 임시 변수에 '캡처'만 합니다.
                        s_pendingHiddenPos = testManager.panelTransform.position;
                        Debug.Log($"Hidden Position 캡처 완료: {s_pendingHiddenPos.Value}. 플레이 모드 종료 시 적용됩니다.");
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("위치 저장은 플레이 모드에서만 가능합니다.", MessageType.Info);
            }
        }

        /// <summary>
        /// 플레이 모드 상태가 변경될 때 호출되는 이벤트 핸들러입니다.
        /// </summary>
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // 플레이 모드를 빠져나와 에디터 모드로 돌아올 때
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // target이 유효한지 확인
                if (target == null) return;
                
                TestManager testManager = (TestManager)target;
                bool changed = false;

                // 저장 대기 중인 'Shown' 위치가 있으면 적용
                if (s_pendingShownPos.HasValue && testManager.shownPosition != null)
                {
                    Undo.RecordObject(testManager.shownPosition, "Apply Shown Position");
                    testManager.shownPosition.position = s_pendingShownPos.Value;
                    s_pendingShownPos = null; // 처리 후 초기화
                    changed = true;
                }

                // 저장 대기 중인 'Hidden' 위치가 있으면 적용
                if (s_pendingHiddenPos.HasValue && testManager.hiddenPosition != null)
                {
                    Undo.RecordObject(testManager.hiddenPosition, "Apply Hidden Position");
                    testManager.hiddenPosition.position = s_pendingHiddenPos.Value;
                    s_pendingHiddenPos = null; // 처리 후 초기화
                    changed = true;
                }

                // 변경사항이 있었다면 씬을 저장하도록 표시
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(testManager.gameObject.scene);
                    Debug.Log("플레이 모드에서 캡처된 패널 위치가 씬에 적용되었습니다.");
                }
            }
        }
    }
}