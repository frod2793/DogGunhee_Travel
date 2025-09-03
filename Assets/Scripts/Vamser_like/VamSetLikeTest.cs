using System;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

#if (UNITY_EDITOR) 
namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// VamserLikeGameManager의 커스텀 에디터 클래스입니다.
    /// 인스펙터에서 직접 캐릭터와 무기를 변경하는 테스트 기능을 제공합니다.
    /// </summary>
    [CustomEditor(typeof(VamserLikeGameManager))]
    public class VamserLikeGameManagerEditor : Editor
    {
        #region 필드 및 변수

        // Editor 클래스의 필드는 SerializeField로 직렬화되지 않으므로, 일반 private 필드로 선언합니다.
        private int _characterIndex;
        private int _weaponIndex;
        private bool _isChanging; // 비동기 작업 진행 상태를 추적하는 플래그

        #endregion

        #region 에디터 UI

        public override void OnInspectorGUI()
        {
            // 기본 인스펙터 UI를 먼저 그립니다.
            base.OnInspectorGUI();

            VamserLikeGameManager vamserLikeGameManager = (VamserLikeGameManager) target;

            // 테스트 기능 UI를 구분하기 위해 시각적인 구획을 추가합니다.
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("인게임 테스트 컨트롤", EditorStyles.boldLabel);
            
            _characterIndex = EditorGUILayout.IntField("캐릭터 인덱스", _characterIndex);
            _weaponIndex = EditorGUILayout.IntField("무기 인덱스", _weaponIndex);

            // 비동기 작업이 진행 중일 때는 버튼을 비활성화하여 중복 실행을 방지합니다.
            EditorGUI.BeginDisabledGroup(_isChanging);
            if (GUILayout.Button("캐릭터 및 무기 변경"))
            {
                // 버튼 클릭 시 비동기 작업을 시작하고, UI는 즉시 반환됩니다. (Fire and Forget)
                ChangeCharacterAndWeaponAsync(vamserLikeGameManager).Forget();
            }
            EditorGUI.EndDisabledGroup();
        }

        #endregion

        #region 버튼 액션

        /// <summary>
        /// 캐릭터와 무기를 비동기적으로 변경하고, 작업이 완료될 때까지 UI를 갱신합니다.
        /// </summary>
        private async UniTask ChangeCharacterAndWeaponAsync(VamserLikeGameManager gameManager)
        {
            if (_isChanging) return;
            _isChanging = true;
            Repaint(); // 인스펙터를 다시 그려서 비활성화된 버튼을 즉시 표시합니다.

            try
            {
                PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex = _characterIndex;
                PlayerDataManagerDontdesytoy.Instance.SelectWeaponIndex = _weaponIndex;
                await gameManager.ChangeCharacterAndWeapon_Spawn();
            }
            catch (Exception e)
            {
                Debug.LogError($"캐릭터/무기 변경 중 에디터에서 예외 발생: {e.Message}");
            }
            finally
            {
                _isChanging = false;
                Repaint(); // 작업 완료 후 버튼을 다시 활성화하기 위해 인스펙터를 다시 그립니다.
            }
        }

        #endregion
    }
}
#endif