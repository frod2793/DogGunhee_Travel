using System;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// VamserLikeGameManager의 커스텀 에디터.
    /// 플레이 전: 시작 캐릭터/무기 설정
    /// 플레이 중: 실시간 캐릭터/무기 교체 테스트
    /// </summary>
    [CustomEditor(typeof(VamserLikeGameManager))]
    public class VamserLikeGameManagerEditor : Editor
    {
        #region 내부 상태 변수

        private SerializedProperty m_startCharacterIndexProp;
        private SerializedProperty m_startWeaponIndexProp;

        private int m_runtimeCharacterIndex;
        private int m_runtimeWeaponIndex;
        private bool m_isChanging; // 비동기 작업 진행 상태

        #endregion

        #region Unity 에디터 라이프사이클

        private void OnEnable()
        {
            // 매니저 스크립트의 변수와 연결
            m_startCharacterIndexProp = serializedObject.FindProperty("m_startCharacterIndex");
            m_startWeaponIndexProp = serializedObject.FindProperty("m_startWeaponIndex");

            // 런타임 중이라면 현재 적용된 값으로 초기화
            if (Application.isPlaying)
            {
                SyncDataFromManager();
            }
        }

        public override void OnInspectorGUI()
        {
            // 기본 인스펙터(스크립트 필드 등) 그리기
            base.OnInspectorGUI();

            serializedObject.Update();

            EditorGUILayout.Space(15);
            
            if (Application.isPlaying)
            {
                // 플레이 중: 즉시 교체 기능
                DrawRuntimeControls();
            }
            else
            {
                // 플레이 전: 시작 설정 기능
                DrawEditorControls();
            }

            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region UI 그리기 및 로직

        /// <summary>
        /// 에디터 모드(플레이 전) UI
        /// </summary>
        private void DrawEditorControls()
        {
            EditorGUILayout.LabelField("🛠️ 에디터 테스트 설정", EditorStyles.boldLabel);
            
            // 박스 스타일로 감싸기
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.HelpBox("게임을 시작할 때 적용될 캐릭터와 무기를 설정합니다.", MessageType.None);
            EditorGUILayout.Space(5);

            EditorGUILayout.PropertyField(m_startCharacterIndexProp, new GUIContent("시작 캐릭터 ID"));
            EditorGUILayout.PropertyField(m_startWeaponIndexProp, new GUIContent("시작 무기 ID"));

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 런타임(플레이 중) UI
        /// </summary>
        private void DrawRuntimeControls()
        {
            EditorGUILayout.LabelField("🎮 런타임 테스트 컨트롤", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.HelpBox("게임 실행 중에 캐릭터와 무기를 즉시 교체합니다.", MessageType.None);
            EditorGUILayout.Space(5);

            m_runtimeCharacterIndex = EditorGUILayout.IntField("교체할 캐릭터 ID", m_runtimeCharacterIndex);
            m_runtimeWeaponIndex = EditorGUILayout.IntField("교체할 무기 ID", m_runtimeWeaponIndex);

            EditorGUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(m_isChanging);
            
            GUI.backgroundColor = Color.green; // 버튼 강조
            if (GUILayout.Button("캐릭터 및 무기 즉시 변경", GUILayout.Height(30)))
            {
                VamserLikeGameManager manager = (VamserLikeGameManager)target;
                ChangeCharacterAndWeaponAsync(manager).Forget();
            }
            GUI.backgroundColor = Color.white; // 색상 복구

            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// PlayerDataManager의 현재 값으로 런타임 변수를 동기화합니다.
        /// </summary>
        private void SyncDataFromManager()
        {
            if (PlayerDataManagerDontdesytoy.Instance != null)
            {
                m_runtimeCharacterIndex = PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex;
                m_runtimeWeaponIndex = PlayerDataManagerDontdesytoy.Instance.SelectWeaponIndex;
            }
        }

        /// <summary>
        /// 캐릭터와 무기를 비동기적으로 변경합니다. (런타임 전용)
        /// </summary>
        private async UniTaskVoid ChangeCharacterAndWeaponAsync(VamserLikeGameManager gameManager)
        {
            if (m_isChanging) return;

            m_isChanging = true;
            Repaint(); // 버튼 비활성화 즉시 반영

            try
            {
                if (PlayerDataManagerDontdesytoy.Instance == null)
                {
                    Debug.LogError("[Editor] PlayerDataManager 인스턴스가 없습니다.");
                    return;
                }

                // 데이터 설정
                PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex = m_runtimeCharacterIndex;
                PlayerDataManagerDontdesytoy.Instance.SelectWeaponIndex = m_runtimeWeaponIndex;

                Debug.Log($"[Editor] 변경 요청: Char({m_runtimeCharacterIndex}), Wep({m_runtimeWeaponIndex})");

                // 실제 게임 로직 호출
                await gameManager.ChangeCharacterAndWeapon_Spawn();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Editor] 변경 중 오류 발생: {e.Message}");
            }
            finally
            {
                m_isChanging = false;
                Repaint(); // 버튼 활성화 반영
            }
        }

        #endregion
    }
}
#endif