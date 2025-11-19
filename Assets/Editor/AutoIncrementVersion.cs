using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System; // String.Split, int.TryParse를 위해 추가
using UnityEngine;

// IPreprocessBuildWithReport 인터페이스를 사용하여 빌드 전에 실행되도록 설정
public class AutoIncrementVersion : IPreprocessBuildWithReport
{
    // 스크립트 실행 순서 (0이 가장 먼저 실행됨)
    public int callbackOrder { get { return 0; } }

    public void OnPreprocessBuild(BuildReport report)
    {
        // 빌드 타겟이 Android일 때만 실행
        if (report.summary.platform == BuildTarget.Android)
        {
            // 1. Android Bundle Version Code 자동 증가
            int currentVersionCode = PlayerSettings.Android.bundleVersionCode;
            PlayerSettings.Android.bundleVersionCode = currentVersionCode + 1;
            Debug.Log($"[UCB Auto] Android Bundle Version Code updated: {currentVersionCode} -> {PlayerSettings.Android.bundleVersionCode}");

            // 2. Application.version (Bundle Version) 자동 갱신
            string currentBundleVersion = PlayerSettings.bundleVersion;
            string newBundleVersion = IncrementPatchVersion(currentBundleVersion);
            
            if (newBundleVersion != currentBundleVersion)
            {
                PlayerSettings.bundleVersion = newBundleVersion;
                Debug.Log($"[UCB Auto] Application.version updated: {currentBundleVersion} -> {PlayerSettings.bundleVersion}");
            }
            else
            {
                Debug.LogWarning($"[UCB Auto] Failed to increment Application.version. Current version: {currentBundleVersion}");
            }

            // 3. 변경사항을 강제로 저장소에 반영합니다.
            // UCB 환경에서 변경사항이 PlayerSettings 파일에 저장되도록 하여 다음 빌드에 반영되게 합니다.
            EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<PlayerSettings>("ProjectSettings/ProjectSettings.asset"));
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>
    /// Major.Minor.Patch 형식의 버전 문자열에서 Patch 버전을 1 증가시킵니다.
    /// Major.Minor 형식일 경우 Patch를 0.1로 추가합니다.
    /// </summary>
    private string IncrementPatchVersion(string version)
    {
        try
        {
            string[] versionParts = version.Split('.');
            if (versionParts.Length >= 3)
            {
                if (int.TryParse(versionParts[2], out int patch))
                {
                    versionParts[2] = (patch + 1).ToString();
                    return string.Join(".", versionParts);
                }
            }
            else if (versionParts.Length == 2)
            {
                return $"{version}.1"; // 예: "1.0" -> "1.0.1"
            }
            else if (versionParts.Length == 1)
            {
                return $"{version}.0.1"; // 예: "1" -> "1.0.1"
            }
            
            Debug.LogWarning($"[UCB Auto] 인식할 수 없는 버전 형식입니다: {version}. 기존 버전을 유지합니다.");
            return version;
        }
        catch (Exception e)
        {
            Debug.LogError($"[UCB Auto] 버전 증가 중 오류 발생 ('{version}'): {e.Message}");
            return version;
        }
    }
}