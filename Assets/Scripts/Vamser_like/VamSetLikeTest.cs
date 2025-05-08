using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

#if (UNITY_EDITOR) 
namespace DogGuns_Games.vamsir
{
    [CustomEditor(typeof(VamserLikeGameManager))]
    public class VamSetLikeTest : Editor
    {

        [FormerlySerializedAs("Character_Index")]
        [Header("<color=green> 인게임내에 캐릭터 및 무기 변경")]
        [SerializeField] private int characterIndex;
        [FormerlySerializedAs("Weapon_Index")] [SerializeField] private int weaponIndex;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            VamserLikeGameManager vamserLikeGameManager = (VamserLikeGameManager) target;

            characterIndex = EditorGUILayout.IntField("characterIndex", characterIndex);
            weaponIndex = EditorGUILayout.IntField("weaponIndex", weaponIndex);

            if (GUILayout.Button("Change Character And Weapon"))
            {
                PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex = characterIndex;
                PlayerDataManagerDontdesytoy.Instance.SelectWeaponIndex = weaponIndex;
                vamserLikeGameManager.ChangeCharacterAndWeapon_Spawn();
            }
        }
      
        
        
    }
}
#endif