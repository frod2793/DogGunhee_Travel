using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EditorTools
{
    public class UnusedImageFinder
    {
        private static readonly string k_TrashFolderPath = "Assets/_TRASH_UnusedImages";
        private static readonly string[] k_ImageExtensions = new string[] 
        { 
            ".png", ".jpg", ".jpeg", ".tga", ".psd", ".bmp", ".tif", ".tiff", ".exr" 
        };

        [MenuItem("Tools/Clean Code/Find & Move Unused Images")]
        public static void FindAndMoveUnusedImages()
        {
            Debug.Log("[UnusedImageFinder] 분석 시작...");

            // 1. 모든 이미지 자산 수집
            List<string> allImagePaths = GetAllImageAssets();
            Debug.Log($"[UnusedImageFinder] 프로젝트 내 총 이미지 수: {allImagePaths.Count}");

            // 2. 사용 중인 자산 식별 (Roots)
            HashSet<string> usedAssets = new HashSet<string>();

            // 2-1. Resources 폴더 내의 모든 것은 사용 중으로 간주 (동적 로딩 가능성)
            string[] allResources = Directory.GetFiles(Application.dataPath, "*", SearchOption.AllDirectories)
                .Where(path => path.Contains("Resources") && !path.EndsWith(".meta"))
                .Select(path => "Assets" + path.Replace(Application.dataPath, "").Replace('\\', '/'))
                .ToArray();
            
            foreach (var res in allResources)
            {
                usedAssets.Add(res);
                // Resources 내부 자산의 의존성도 추가
                foreach (var dep in AssetDatabase.GetDependencies(res))
                {
                    usedAssets.Add(dep);
                }
            }

            // 2-2. 이미지가 아닌 모든 자산(씬, 프리팹, 마테리얼, 에셋 등)을 Root로 잡고 의존성 파악
            string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
            List<string> rootAssets = new List<string>();

            foreach (var path in allAssetPaths)
            {
                if (IsImage(path)) continue; // 이미지는 Root가 아님 (이미지가 이미지를 참조하는 경우는 드물지만, 아래 의존성 체크에서 커버됨)
                if (!path.StartsWith("Assets/")) continue; // 패키지 내부 제외
                if (path.Contains("/Editor/")) continue; // 에디터 스크립트 등 제외 (선택 사항이나 안전을 위해 포함 가능) 
                
                rootAssets.Add(path);
            }

            // 2-3. Root 자산들의 의존성 수집
            foreach (var root in rootAssets)
            {
                string[] dependencies = AssetDatabase.GetDependencies(root, true); // Recursive
                foreach (var dep in dependencies)
                {
                    usedAssets.Add(dep);
                }
            }

            // 3. 사용되지 않는 이미지 필터링
            List<string> unusedImages = new List<string>();
            foreach (var imgPath in allImagePaths)
            {
                // Editor 폴더에 있는 이미지는 보통 아이콘 등이므로 제외
                if (imgPath.Contains("/Editor/")) continue;
                
                if (!usedAssets.Contains(imgPath))
                {
                    unusedImages.Add(imgPath);
                }
            }

            // 4. 이동 처리
            if (unusedImages.Count > 0)
            {
                if (!AssetDatabase.IsValidFolder(k_TrashFolderPath))
                {
                    AssetDatabase.CreateFolder("Assets", "_TRASH_UnusedImages");
                }

                int moveCount = 0;
                foreach (var path in unusedImages)
                {
                    string fileName = Path.GetFileName(path);
                    string newPath = AssetDatabase.GenerateUniqueAssetPath(k_TrashFolderPath + "/" + fileName);
                    
                    string error = AssetDatabase.MoveAsset(path, newPath);
                    if (string.IsNullOrEmpty(error))
                    {
                        moveCount++;
                    }
                    else
                    {
                        Debug.LogError($"[UnusedImageFinder] 이동 실패: {path} -> {error}");
                    }
                }

                Debug.Log($"<color=green>[UnusedImageFinder] 완료! 총 {moveCount}개의 미사용 이미지를 '{k_TrashFolderPath}'로 이동했습니다.</color>");
                Debug.LogWarning("주의: 코드에서 문자열(String)로만 참조되는 이미지는 감지되지 않았을 수 있습니다. 삭제 전 반드시 확인하세요.");
            }
            else
            {
                Debug.Log("<color=yellow>[UnusedImageFinder] 사용되지 않는 이미지가 발견되지 않았습니다.</color>");
            }
            
            AssetDatabase.Refresh();
        }

        private static List<string> GetAllImageAssets()
        {
            List<string> images = new List<string>();
            string[] guids = AssetDatabase.FindAssets("t:Texture"); // Texture 타입 검색

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsImage(path) && path.StartsWith("Assets/"))
                {
                    images.Add(path);
                }
            }
            return images;
        }

        private static bool IsImage(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            return k_ImageExtensions.Contains(ext);
        }
    }
}
