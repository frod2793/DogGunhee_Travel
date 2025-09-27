using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

class buildEditorScript
{
    // [MenuItem] 속성은 Unity 에디터에서 수동으로 테스트할 때 유용합니다.
    [MenuItem("Build/Build Android (APK)")]
    public static void PerformAndroidBuild_APK()
    {
        // 안드로이드 빌드 시에는 기본 프로필로 모든 어드레서블을 빌드합니다.
        if (!BuildAddressables()) ExitWithFailure();
        SetupAndroidBuild("DogGunhee.apk", false);
    }

    [MenuItem("Build/Build Android (AAB)")]
    public static void PerformAndroidBuild_AAB()
    {
        if (!BuildAddressables()) ExitWithFailure();
        SetupAndroidBuild("DogGunhee.aab", true);
    }

    [MenuItem("Build/Build WebGL")]
    public static void PerformWebGLBuild()
    {
        // 'TestWebGl' 프로필을 활성화하고 모든 Addressable 그룹을 빌드합니다.
        if (!SetActiveProfileAndBuildAddressables("TestWebGl")) ExitWithFailure();

        // Unity 버전 호환성을 위해 구식(obsolete) API를 사용합니다.
        PlayerSettings.SetPropertyInt("webglCodeOptimization", 0, BuildTargetGroup.WebGL);
        PlayerSettings.SetPropertyBool("webglLinkTimeOptimization", true, BuildTargetGroup.WebGL);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = FindEnabledEditorScenes(),
            locationPathName = "Builds/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.CleanBuildCache
        };
        BuildAndReport(options);
    }

    [MenuItem("Build/Build Windows")]
    static void PerformWindowsBuild()
    {
        if (!BuildAddressables()) ExitWithFailure();

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = FindEnabledEditorScenes(),
            locationPathName = "Builds/Windows/DogGunhee.exe",
            target = BuildTarget.StandaloneWindows,
            options = BuildOptions.None
        };
        BuildAndReport(options);
    }

    /// <summary>
    /// 지정된 프로필을 활성화하고 모든 Addressable 그룹을 빌드합니다.
    /// </summary>
    /// <param name="profileName">활성화할 프로필의 이름</param>
    /// <returns>성공 여부</returns>
    private static bool SetActiveProfileAndBuildAddressables(string profileName)
    {
        Debug.Log($"Addressable 프로필 '{profileName}'을(를) 활성화합니다...");
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        string profileId = settings.profileSettings.GetProfileId(profileName);

        if (string.IsNullOrEmpty(profileId))
        {
            Debug.LogError($"Addressable 프로필 '{profileName}'을(를) 찾을 수 없습니다!");
            return false;
        }

        settings.activeProfileId = profileId;
        Debug.Log($"'{profileName}' 프로필이 활성화되었습니다. 전체 Addressable 빌드를 시작합니다.");

        // 활성화 후, 모든 그룹을 빌드하는 공통 메서드를 호출합니다.
        return BuildAddressables();
    }

    /// <summary>
    /// 현재 활성화된 프로필을 사용하여 빌드에 포함된 모든 Addressable 그룹을 빌드합니다.
    /// </summary>
    private static bool BuildAddressables()
    {
        try
        {
            AddressableAssetSettings.CleanPlayerContent(AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilder);
            AddressableAssetSettings.BuildPlayerContent();
            Debug.Log("어드레서블 빌드가 완료되었습니다.");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"어드레서블 빌드 중 예외가 발생했습니다: {e.Message}");
            return false;
        }
    }

    private static void SetupAndroidBuild(string fileName, bool isBundle)
    {
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.DefaultCompany.DogGun_E_Run");
        EditorUserBuildSettings.buildAppBundle = isBundle;

        /*
        PlayerSettings.Android.keystoreName = "path/to/your/keystore.keystore";
        PlayerSettings.Android.keystorePass = "yourKeystorePassword";
        PlayerSettings.Android.keyaliasName = "yourAliasName";
        PlayerSettings.Android.keyaliasPass = "yourAliasPassword";
        */

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = FindEnabledEditorScenes(),
            locationPathName = Path.Combine("Builds", "Android", fileName),
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        Debug.Log("Android 빌드를 시작합니다...");
        BuildAndReport(buildPlayerOptions);
    }

    private static void BuildAndReport(BuildPlayerOptions buildPlayerOptions)
    {
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"빌드 성공! 경로: {summary.outputPath}, 용량: {summary.totalSize / 1024f / 1024f:F2} MB");
            CopyBuildToDestination(summary.outputPath);
        }
        else
        {
            Debug.LogError($"빌드 실패! 에러: {summary.totalErrors}개");
            ExitWithFailure();
        }
    }

    private static void CopyBuildToDestination(string sourcePath)
    {
        string destinationDir = Environment.GetEnvironmentVariable("UNITY_BUILD_COPY_PATH");

        if (string.IsNullOrEmpty(destinationDir))
        {
            Debug.LogWarning("환경 변수 'UNITY_BUILD_COPY_PATH'가 설정되지 않아 파일 복사를 건너뜁니다.");
            return;
        }

        try
        {
            if (!Directory.Exists(destinationDir)) Directory.CreateDirectory(destinationDir);

            if (Path.GetExtension(sourcePath) == "")
            {
                string sourceDirName = new DirectoryInfo(sourcePath).Name;
                string destDirPath = Path.Combine(destinationDir, sourceDirName);
                if (Directory.Exists(destDirPath)) Directory.Delete(destDirPath, true);
                CopyDirectory(sourcePath, destDirPath);
                Debug.Log($"빌드 결과 폴더를 다음 경로에 복사했습니다: {destDirPath}");
            }
            else
            {
                string destFilePath = Path.Combine(destinationDir, Path.GetFileName(sourcePath));
                File.Copy(sourcePath, destFilePath, true);
                Debug.Log($"빌드 결과물을 다음 경로에 복사했습니다: {destFilePath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"빌드 결과물 복사 실패: {e.Message}");
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        foreach (DirectoryInfo subDir in dir.GetDirectories())
        {
            string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }

    private static string[] FindEnabledEditorScenes()
    {
        return EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
    }

    private static void ExitWithFailure()
    {
        Debug.LogError("빌드 프로세스에 심각한 오류가 발생하여 중단합니다.");
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(1);
        }
    }
}