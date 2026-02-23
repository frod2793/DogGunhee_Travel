using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using InGame.Managers;

namespace InGame.Test.Editor
{
    /// <summary>
    /// [설명]: TestManager의 인스펙터를 확장하여, 플레이 모드에서 설정한 패널 위치를 
    /// 에디터 모드로 돌아왔을 때 실제 데이터로 저장 및 적용하는 기능을 제공하는 에디터 스크립트입니다.
    /// </summary>
    [CustomEditor(typeof(TestManager))]
    public class TestManagerEditor : UnityEditor.Editor
    {
        #region 내부 필드

        /// <summary> 플레이 모드가 종료될 때 적용하기 위해 임시로 저장해둔 노출(Shown) 위치 좌표 </summary>
        private static Vector3? s_pendingHiddenPos;

        /// <summary> 플레이 모드가 종료될 때 적용하기 위해 임시로 저장해둔 숨김(Hidden) 위치 좌표 </summary>
        private static Vector3? s_pendingShownPos;

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 정적 생성자를 통해 에디터가 로드될 때 플레이 모드 상태 변경 이벤트를 구독합니다.
        /// 이를 통해 인스펙터가 명시적으로 열려 있지 않은 상태에서도 데이터 동기화가 가능합니다.
        /// </summary>
        static TestManagerEditor()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        #endregion

        #region GUI 렌더링

        /// <summary>
        /// [설명]: 인스펙터 창의 GUI를 렌더링하며, 위치 저장을 위한 커스텀 버튼들을 출력합니다.
        /// </summary>
        public override void OnInspectorGUI()
        {
            // 기본 인스펙터 필드 출력
            base.OnInspectorGUI();

            TestManager manager = (TestManager)target;
            if (manager == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Play Mode Position Saver", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("플레이 모드에서 패널을 이동시킨 후 저장 버튼을 누르세요.\n플레이 모드가 종료되면 해당 위치가 씬 데이터에 반영됩니다.", MessageType.Info);

            // 런타임(플레이 모드) 환경에서만 저장 버튼을 활성화함
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

            // 현재 메모리에 캡처되어 적용 대기 중인 좌표 정보 표시
            if (s_pendingShownPos.HasValue || s_pendingHiddenPos.HasValue)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Pending Changes (Apply on Exit Play):", EditorStyles.miniBoldLabel);
                
                if (s_pendingShownPos.HasValue)
                {
                    EditorGUILayout.LabelField($"- Shown: {s_pendingShownPos.Value}");
                }
                
                if (s_pendingHiddenPos.HasValue)
                {
                    EditorGUILayout.LabelField($"- Hidden: {s_pendingHiddenPos.Value}");
                }
            }
        }

        /// <summary>
        /// [설명]: 지정된 트랜스폼의 현재 월드 좌표를 정적 변수에 캡처합니다.
        /// </summary>
        /// <param name="panelTransform">캡처할 대상 트랜스폼</param>
        /// <param name="isShownPos">노출 위치인지 숨김 위치인지 여부</param>
        private void CapturePosition(Transform panelTransform, bool isShownPos)
        {
            if (panelTransform == null)
            {
                Debug.LogWarning("[TestManagerEditor] Panel Transform이 할당되지 않아 위치를 캡처할 수 없습니다.");
                return;
            }

            if (isShownPos)
            {
                s_pendingShownPos = panelTransform.position;
                Debug.Log($"[Editor] Shown Position 캡처됨: {s_pendingShownPos.Value}");
            }
            else
            {
                s_pendingHiddenPos = panelTransform.position;
                Debug.Log($"[Editor] Hidden Position 캡처됨: {s_pendingHiddenPos.Value}");
            }
        }

        #endregion

        #region 내부 이벤트 및 상태 처리

        /// <summary>
        /// [설명]: 유니티 플레이 모드 상태가 변경될 때 호출됩니다. 
        /// 플레이 모드가 종료되고 에디터 모드로 완전 진입한 시점에 캡처된 데이터를 적용합니다.
        /// </summary>
        /// <param name="state">플레이 모드 상태 변화 값</param>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // 에디터 모드로 완전히 복귀했을 때만 씬 오브젝트에 데이터를 기록함
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                ApplyCapturedPositions();
            }
        }

        /// <summary>
        /// [설명]: 캡처된 좌표 정보를 씬에 배치된 실제 TestManager 데이터 객체에 할당하고 저장(Undo 포함) 처리합니다.
        /// </summary>
        private static void ApplyCapturedPositions()
        {
            // 적용할 데이터가 하나도 없으면 루틴 중단
            if (s_pendingShownPos == null && s_pendingHiddenPos == null)
            {
                return;
            }

            // 런타임 캐시가 아닌 씬에 영구 보존된 매니저 객체를 검색
            TestManager manager = FindAnyObjectByType<TestManager>();

            if (manager != null)
            {
                bool isDirty = false;

                // 노출 위치(Shown Position) 데이터 반영
                if (s_pendingShownPos.HasValue && manager.ShownPosition != null)
                {
                    Undo.RecordObject(manager.ShownPosition, "Apply Captured Shown Position");
                    manager.ShownPosition.position = s_pendingShownPos.Value;
                    s_pendingShownPos = null;
                    isDirty = true;
                    Debug.Log("[Editor] Captured Shown Position이 성공적으로 적용되었습니다.");
                }

                // 숨김 위치(Hidden Position) 데이터 반영
                if (s_pendingHiddenPos.HasValue && manager.HiddenPosition != null)
                {
                    Undo.RecordObject(manager.HiddenPosition, "Apply Captured Hidden Position");
                    manager.HiddenPosition.position = s_pendingHiddenPos.Value;
                    s_pendingHiddenPos = null;
                    isDirty = true;
                    Debug.Log("[Editor] Captured Hidden Position이 성공적으로 적용되었습니다.");
                }

                // 변경된 씬 데이터 보존을 위해 더티 마크 설정
                if (isDirty)
                {
                    EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                }
            }
            else
            {
                // 작업을 수행할 대상 객체가 씬에 존재하지 않으면 데이터 폐기
                s_pendingShownPos = null;
                s_pendingHiddenPos = null;
            }
        }

        #endregion
    }
}