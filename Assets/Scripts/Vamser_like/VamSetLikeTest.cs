using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

#if (UNITY_EDITOR) 
namespace DogGuns_Games.vamsir
{
    [CustomEditor(typeof(VamserLikeGameManager))]
    public class VamSetLikeTest : Editor
    {
        #region 필드 및 변수

        [FormerlySerializedAs("Character_Index")]
        [Header("<color=green> 인게임내에 캐릭터 및 무기 변경")]
        [SerializeField] private int characterIndex;
        
        [FormerlySerializedAs("Weapon_Index")] 
        [SerializeField] private int weaponIndex;

        #endregion

        #region 에디터 UI

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            VamserLikeGameManager vamserLikeGameManager = (VamserLikeGameManager) target;

            characterIndex = EditorGUILayout.IntField("캐릭터 인덱스", characterIndex);
            weaponIndex = EditorGUILayout.IntField("무기 인덱스", weaponIndex);

            if (GUILayout.Button("캐릭터 및 무기 변경"))
            {
                ChangeCharacterAndWeapon(vamserLikeGameManager);
            }
        }

        #endregion

        #region 버튼 액션

        private void ChangeCharacterAndWeapon(VamserLikeGameManager gameManager)
        {
            PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex = characterIndex;
            PlayerDataManagerDontdesytoy.Instance.SelectWeaponIndex = weaponIndex;
            gameManager.ChangeCharacterAndWeapon_Spawn();
        }

        #endregion
    }
}
#endif