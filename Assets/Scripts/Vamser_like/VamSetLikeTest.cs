using System;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// VamserLikeGameManager의 커스텀 에디터 클래스 (최적화됨)
    /// 인스펙터에서 런타임 중 캐릭터와 무기를 즉시 교체하는 테스트 도구를 제공합니다.
    /// </summary>
    [CustomEditor(typeof(VamserLikeGameManager))]
    public class VamserLikeGameManagerEditor : Editor
    {
        #region 내부 상태 변수

        private int m_characterIndex;
        private int m_weaponIndex;
        private bool m_isChanging; // 비동기 작업 진행 상태

        #endregion

        #region Unity 에디터 라이프사이클

        /// <summary>
        /// 인스펙터가 활성화될 때 호출됩니다.
        /// 현재 게임 데이터와 에디터 입력값을 동기화합니다.
        /// </summary>
        private void OnEnable()
        {
            SyncDataFromManager();
        }

        public override void OnInspectorGUI()
        {
            // 기본 인스펙터 UI 그리기
            base.OnInspectorGUI();

            // 구분선 및 헤더
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("🎮 인게임 테스트 컨트롤", EditorStyles.boldLabel);

            // [안정성] 플레이 모드가 아닐 경우 안내 메시지 표시 후 리턴
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("이 기능은 플레이 모드(Play Mode)에서만 사용할 수 있습니다.", MessageType.Info);
                return;
            }

            DrawTestControls();
        }

        #endregion

        #region UI 그리기 및 로직

        private void DrawTestControls()
        {
            // 입력 필드 그리기
            m_characterIndex = EditorGUILayout.IntField("캐릭터 인덱스", m_characterIndex);
            m_weaponIndex = EditorGUILayout.IntField("무기 인덱스", m_weaponIndex);

            EditorGUILayout.Space(5);

            // 비동기 작업 중 버튼 비활성화 처리
            EditorGUI.BeginDisabledGroup(m_isChanging);
            
            if (GUILayout.Button("캐릭터 및 무기 변경 적용", GUILayout.Height(30)))
            {
                VamserLikeGameManager manager = (VamserLikeGameManager)target;
                ChangeCharacterAndWeaponAsync(manager).Forget();
            }
            
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// PlayerDataManager의 현재 값으로 에디터 필드를 초기화합니다.
        /// </summary>
        private void SyncDataFromManager()
        {
            if (Application.isPlaying && PlayerDataManagerDontdesytoy.Instance != null)
            {
                m_characterIndex = PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex;
                m_weaponIndex = PlayerDataManagerDontdesytoy.Instance.SelectWeaponIndex;
            }
        }

        /// <summary>
        /// 캐릭터와 무기를 비동기적으로 변경합니다.
        /// </summary>
        private async UniTaskVoid ChangeCharacterAndWeaponAsync(VamserLikeGameManager gameManager)
        {
            if (m_isChanging) return;

            m_isChanging = true;
            
            // 변경 시작 시점의 상태를 UI에 즉시 반영
            Repaint(); 

            try
            {
                // 싱글톤 참조 안전성 체크
                if (PlayerDataManagerDontdesytoy.Instance == null)
                {
                    Debug.LogError("[Editor] PlayerDataManager 인스턴스가 없습니다.");
                    return;
                }

                // 데이터 설정
                PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex = m_characterIndex;
                PlayerDataManagerDontdesytoy.Instance.SelectWeaponIndex = m_weaponIndex;

                Debug.Log($"[Editor] 변경 요청: Char({m_characterIndex}), Wep({m_weaponIndex})");

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
                
                // 작업 완료 후 버튼 활성화를 위해 다시 그리기
                Repaint(); 
            }
        }

        #endregion
    }
}
#endif