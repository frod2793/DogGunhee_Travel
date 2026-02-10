using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using InGame.Manager;

namespace InGame.Test.Editor
{
    /// <summary>
    /// TestManager의 인스펙터를 확장하여, 플레이 모드에서 설정한 패널 위치를 
    /// 에디터 모드로 돌아왔을 때 저장/적용하는 기능을 제공합니다.
    /// </summary>
    [CustomEditor(typeof(TestManager))]
    public class TestManagerEditor : UnityEditor.Editor
    {
        #region 내부 상태 (Static Fields)

        // 플레이 모드에서 에디터 모드로 전환될 때 데이터를 유지하기 위한 정적 변수
        private static Vector3? s_pendingHiddenPos;
        private static Vector3? s_pendingShownPos;

        #endregion

        #region 초기화 (Static Constructor)

        /// <summary>
        /// 정적 생성자를 통해 에디터 로드 시 이벤트를 한 번만 구독합니다.
        /// 이를 통해 인스펙터가 열려있지 않아도 상태 변경 감지가 가능합니다.
        /// </summary>
        static TestManagerEditor()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        #endregion

        #region 인스펙터 GUI (OnInspectorGUI)

        public override void OnInspectorGUI()
        {
            // 기본 필드 그리기
            base.OnInspectorGUI();

            TestManager manager = (TestManager)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Play Mode Position Saver", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("플레이 모드에서 원하는 위치로 패널을 이동시킨 후 버튼을 누르세요.\n플레이 모드가 종료되면 해당 위치가 씬에 저장됩니다.", MessageType.Info);

            // 플레이 모드일 때만 저장 버튼 활성화
            using (new EditorGUI.DisabledGroupScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Save Shown Pos"))
                {
                    CapturePosition(manager.PanelTransform, true);
                }

                if (GUILayout.Button("Save Hidden Pos"))
                {
                    CapturePosition(manager.PanelTransform, false);
                }

                EditorGUILayout.EndHorizontal();
            }

            // 현재 캡처된 상태 표시
            if (s_pendingShownPos.HasValue || s_pendingHiddenPos.HasValue)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Pending Changes:", EditorStyles.miniBoldLabel);
                if (s_pendingShownPos.HasValue) EditorGUILayout.LabelField($"- Shown: {s_pendingShownPos.Value}");
                if (s_pendingHiddenPos.HasValue) EditorGUILayout.LabelField($"- Hidden: {s_pendingHiddenPos.Value}");
            }
        }

        private void CapturePosition(Transform panelTransform, bool isShownPos)
        {
            if (panelTransform == null)
            {
                Debug.LogWarning("[TestManagerEditor] Panel Transform이 할당되지 않았습니다.");
                return;
            }

            if (isShownPos)
            {
                s_pendingShownPos = panelTransform.position;
                Debug.Log($"[Editor] Shown Position 캡처됨: {s_pendingShownPos.Value} (플레이 종료 시 적용)");
            }
            else
            {
                s_pendingHiddenPos = panelTransform.position;
                Debug.Log($"[Editor] Hidden Position 캡처됨: {s_pendingHiddenPos.Value} (플레이 종료 시 적용)");
            }
        }

        #endregion

        #region 상태 변경 핸들러 (State Change Handler)

        /// <summary>
        /// 플레이 모드 상태 변경 시 호출됩니다.
        /// 에디터 모드로 진입(EnteredEditMode)했을 때 캡처된 값을 원본 프리팹/씬 객체에 적용합니다.
        /// </summary>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // [중요] ExitingPlayMode가 아니라 EnteredEditMode에서 적용해야 
            // 런타임 객체가 아닌 '씬 원본 객체'를 찾아 수정할 수 있습니다.
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                ApplyCapturedPositions();
            }
        }

        private static void ApplyCapturedPositions()
        {
            // 저장할 데이터가 없으면 리턴
            if (s_pendingShownPos == null && s_pendingHiddenPos == null) return;

            // 에디터 모드이므로 FindFirstObjectByType을 사용하여 씬의 TestManager를 찾습니다.
            // (런타임의 target 객체는 이미 파괴되었음)
            TestManager manager = FindAnyObjectByType<TestManager>();

            if (manager != null)
            {
                bool isDirty = false;

                // Shown Position 적용
                if (s_pendingShownPos.HasValue && manager.ShownPosition != null)
                {
                    Undo.RecordObject(manager.ShownPosition, "Apply Shown Position");
                    manager.ShownPosition.position = s_pendingShownPos.Value;
                    s_pendingShownPos = null;
                    isDirty = true;
                    Debug.Log("[Editor] Shown Position이 씬에 적용되었습니다.");
                }

                // Hidden Position 적용
                if (s_pendingHiddenPos.HasValue && manager.HiddenPosition != null)
                {
                    Undo.RecordObject(manager.HiddenPosition, "Apply Hidden Position");
                    manager.HiddenPosition.position = s_pendingHiddenPos.Value;
                    s_pendingHiddenPos = null;
                    isDirty = true;
                    Debug.Log("[Editor] Hidden Position이 씬에 적용되었습니다.");
                }

                // 변경 사항 저장 표시
                if (isDirty)
                {
                    EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                }
            }
            else
            {
                // 씬에 매니저가 없다면 데이터 폐기
                s_pendingShownPos = null;
                s_pendingHiddenPos = null;
            }
        }

        #endregion
    }
}