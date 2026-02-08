using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace InGame.Editor
{
    public class SpritePhysicsShapeFixer : EditorWindow
    {
        [MenuItem("Tools/Fix Cat Punch Sprites (High Precision)")]
        public static void FixCatPunchSprites()
        {
            // 1. 대상 폴더 설정 (냥냥펀치 이펙트 경로)
            string targetPath = "Assets/_Game/Art/Game_Resource/Common/Images/Effect/CatPhunch";
            
            if (!AssetDatabase.IsValidFolder(targetPath))
            {
                Debug.LogError($"[SpriteFixer] Target path not found: {targetPath}");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { targetPath });
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null)
                {
                    // [Fix] TextureImporter 직접 참조 대신 TextureImporterSettings를 통해 안전하게 설정
                    TextureImporterSettings settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);

                    bool changed = false;

                    // SpriteMeshType을 Tight로 설정하여 외곽선을 정밀하게 따도록 함
                    if (settings.spriteMeshType != SpriteMeshType.Tight)
                    {
                        settings.spriteMeshType = SpriteMeshType.Tight;
                        changed = true;
                    }

                    if (changed)
                    {
                        importer.SetTextureSettings(settings);
                        importer.SaveAndReimport();
                        count++;
                    }
                }
            }

            Debug.Log($"[SpriteFixer] 처리 완료! {count}개의 스프라이트 설정을 'Tight' 모드로 변경했습니다.\n" +
                      "**[중요]** 더 정밀한 콜라이더가 필요하다면:\n" +
                      "1. Sprite Editor -> Custom Physics Shape 탭으로 이동\n" +
                      "2. 'Outline Tolerance'를 0.05 정도로 낮춘 후 'Generate' 버튼을 눌러주세요.\n" +
                      "3. 'Apply'를 누르면 인게임에서 바로 적용됩니다.");
        }
    }
}
