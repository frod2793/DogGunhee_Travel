using System;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// VamserLikeGameManager의 커스텀 에디터.
    /// 플레이 전: 시작 캐릭터/무기/레벨 설정
    /// 플레이 중: 실시간 캐릭터/무기/레벨 교체 테스트
    /// </summary>
    [CustomEditor(typeof(VamserLikeGameManager))]
    public class VamserLikeGameManagerEditor : Editor
    {
        #region 내부 상태 변수

        private SerializedProperty m_startCharacterIndexProp;
        private SerializedProperty m_startWeaponIndexProp;
        private SerializedProperty m_startWeaponUpgradeLv2Prop; // [추가]

        private int m_runtimeCharacterIndex;
        private int m_runtimeWeaponIndex;
        private bool m_runtimeWeaponUpgradeLv2; // [추가]
        
        private bool m_isChanging; // 비동기 작업 진행 상태

        #endregion

        #region Unity 에디터 라이프사이클

        private void OnEnable()
        {
            // 매니저 스크립트의 변수와 연결
            m_startCharacterIndexProp = serializedObject.FindProperty("m_startCharacterIndex");
            m_startWeaponIndexProp = serializedObject.FindProperty("m_startWeaponIndex");
            m_startWeaponUpgradeLv2Prop = serializedObject.FindProperty("m_startWeaponUpgradeLv2"); // [추가]

            // 런타임 중이라면 현재 적용된 값으로 초기화
            if (Application.isPlaying)
            {
                SyncDataFromManager();
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();

            EditorGUILayout.Space(15);
            
            if (Application.isPlaying)
            {
                DrawRuntimeControls();
            }
            else
            {
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
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.HelpBox("게임을 시작할 때 적용될 설정을 입력하세요.", MessageType.None);
            EditorGUILayout.Space(5);

            EditorGUILayout.PropertyField(m_startCharacterIndexProp, new GUIContent("시작 캐릭터 ID"));
            EditorGUILayout.PropertyField(m_startWeaponIndexProp, new GUIContent("시작 무기 ID"));
            
            // [추가] 레벨업 토글
            EditorGUILayout.PropertyField(m_startWeaponUpgradeLv2Prop, new GUIContent("무기 레벨 2 적용"));

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 런타임(플레이 중) UI
        /// </summary>
        private void DrawRuntimeControls()
        {
            EditorGUILayout.LabelField("🎮 런타임 테스트 컨트롤", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.HelpBox("게임 실행 중에 캐릭터와 무기를 즉시 교체하거나 레벨을 변경합니다.", MessageType.None);
            EditorGUILayout.Space(5);

            m_runtimeCharacterIndex = EditorGUILayout.IntField("교체할 캐릭터 ID", m_runtimeCharacterIndex);
            m_runtimeWeaponIndex = EditorGUILayout.IntField("교체할 무기 ID", m_runtimeWeaponIndex);
            
            // [추가] 런타임 레벨업 토글
            m_runtimeWeaponUpgradeLv2 = EditorGUILayout.Toggle("무기 레벨 2 적용", m_runtimeWeaponUpgradeLv2);

            EditorGUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(m_isChanging);
            
            GUI.backgroundColor = Color.green; 
            if (GUILayout.Button("설정 즉시 적용 (Respawn)", GUILayout.Height(30)))
            {
                VamserLikeGameManager manager = (VamserLikeGameManager)target;
                ChangeCharacterAndWeaponAsync(manager).Forget();
            }
            GUI.backgroundColor = Color.white;

            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 현재 게임 상태값으로 런타임 변수를 동기화합니다.
        /// </summary>
        private void SyncDataFromManager()
        {
            if (PlayerDataManagerDontdesytoy.Instance != null)
            {
                m_runtimeCharacterIndex = PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex;
                m_runtimeWeaponIndex = PlayerDataManagerDontdesytoy.Instance.SelectWeaponIndex;
            }

            // [추가] 현재 스폰된 플레이어의 무기 레벨 상태 가져오기
            var manager = (VamserLikeGameManager)target;
            if (manager.SpawnedPlayer != null && manager.SpawnedPlayer.WeaphonBase != null)
            {
                m_runtimeWeaponUpgradeLv2 = manager.SpawnedPlayer.WeaphonBase.isUpgradelv2;
            }
        }

        /// <summary>
        /// 캐릭터, 무기, 레벨을 비동기적으로 변경합니다. (런타임 전용)
        /// </summary>
        private async UniTaskVoid ChangeCharacterAndWeaponAsync(VamserLikeGameManager gameManager)
        {
            if (m_isChanging) return;

            m_isChanging = true;
            Repaint();

            try
            {
                if (PlayerDataManagerDontdesytoy.Instance == null)
                {
                    Debug.LogError("[Editor] PlayerDataManager 인스턴스가 없습니다.");
                    return;
                }

                // 1. 캐릭터/무기 인덱스 설정
                PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex = m_runtimeCharacterIndex;
                PlayerDataManagerDontdesytoy.Instance.SelectWeaponIndex = m_runtimeWeaponIndex;

                Debug.Log($"[Editor] 변경 요청: Char({m_runtimeCharacterIndex}), Wep({m_runtimeWeaponIndex}), Lv2({m_runtimeWeaponUpgradeLv2})");

                // 2. 리스폰 (캐릭터/무기 교체)
                await gameManager.ChangeCharacterAndWeapon_Spawn();

                // 3. [추가] 스폰 완료 후 무기 레벨 적용
                if (gameManager.SpawnedPlayer != null && gameManager.SpawnedPlayer.WeaphonBase != null)
                {
                    gameManager.SpawnedPlayer.WeaphonBase.isUpgradelv2 = m_runtimeWeaponUpgradeLv2;
                    Debug.Log($"[Editor] 무기 레벨 설정 완료: Lv {(m_runtimeWeaponUpgradeLv2 ? 2 : 1)}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Editor] 변경 중 오류 발생: {e.Message}");
            }
            finally
            {
                m_isChanging = false;
                Repaint();
            }
        }

        #endregion
    }
}
#endif