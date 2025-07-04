using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build; // NamedBuildTarget을 사용하기 위해 추가
using UnityEditor.Build.Reporting; // BuildReport를 사용하기 위해 추가
using UnityEngine;

class buildEditorScript
{
    // [MenuItem] 속성을 추가하여 유니티 에디터 메뉴에서 빌드를 실행할 수 있도록 합니다.
    [MenuItem("Build/Build Android (APK)")]
    public static void PerformAndroidBuild_APK()
    {
        // 어드레서블 콘텐츠를 빌드합니다.
        BuildAddressables();
        // 빌드 타겟을 Android로 설정합니다.
        SetupAndroidBuild("DogGunhee.apk", false);
    }

    [MenuItem("Build/Build Android (AAB)")]
    public static void PerformAndroidBuild_AAB()
    {
        // 어드레서블 콘텐츠를 빌드합니다.
        BuildAddressables();
        // 구글 플레이 스토어에 올리기 위한 AAB(Android App Bundle) 형식으로 빌드합니다.
        SetupAndroidBuild("DogGunhee.aab", true);
    }

    /// <summary>
    /// 어드레서블 콘텐츠를 빌드하는 메소드입니다.
    /// </summary>
    private static void BuildAddressables()
    {
        Debug.Log("어드레서블 빌드를 시작합니다...");
        // 이전 빌드 캐시를 정리하고 새로 빌드합니다.
        AddressableAssetSettings.CleanPlayerContent(
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilder);
        AddressableAssetSettings.BuildPlayerContent();
        Debug.Log("어드레서블 빌드가 완료되었습니다.");
    }

    /// <summary>
    /// 안드로이드 빌드를 위한 공통 설정 및 실행 메소드입니다.
    /// </summary>
    /// <param name="fileName">빌드 결과물 파일 이름 (확장자 포함)</param>
    /// <param name="isBundle">AAB 형식으로 빌드할지 여부</param>
    private static void SetupAndroidBuild(string fileName, bool isBundle)
    {
        // --------------------------------------------------
        // 1. 안드로이드 빌드에 필요한 PlayerSettings 설정
        // --------------------------------------------------

        // 패키지 이름(Application Identifier) 설정. 새로운 NamedBuildTarget을 사용합니다.
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.DefaultCompany.DogGun_E_Run");

        // 앱 번들(AAB)로 빌드할지 APK로 빌드할지 설정합니다.
        EditorUserBuildSettings.buildAppBundle = isBundle;

        // 릴리즈 빌드를 위해서는 키스토어(Keystore) 설정이 필요합니다.
        // 아래 주석을 풀고 실제 키스토어 경로와 비밀번호를 입력하여 사용하세요.
        /*
        PlayerSettings.Android.keystoreName = "path/to/your/keystore.keystore";
        PlayerSettings.Android.keystorePass = "yourKeystorePassword";
        PlayerSettings.Android.keyaliasName = "yourAliasName";
        PlayerSettings.Android.keyaliasPass = "yourAliasPassword";
        */


        // --------------------------------------------------
        // 2. 빌드 옵션 구성
        // --------------------------------------------------
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();

        // 빌드에 포함할 씬 목록을 가져옵니다.
        buildPlayerOptions.scenes = FindEnabledEditorScenes();

        // 빌드 결과물이 저장될 경로와 파일 이름을 설정합니다.
        buildPlayerOptions.locationPathName = "Builds/Android/" + fileName;


        // 빌드 타겟을 안드로이드로 설정합니다.
        buildPlayerOptions.target = BuildTarget.Android;

        buildPlayerOptions.options = BuildOptions.None; // 빌드 옵션을 설정합니다. 필요에 따라 추가 옵션을 설정할 수 있습니다.


        // --------------------------------------------------
        // 3. 빌드 실행
        // --------------------------------------------------
        Debug.Log("Android 빌드를 시작합니다...");

        // BuildPipeline.BuildPlayer에 옵션을 전달하여 빌드를 실행합니다.
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"빌드 성공! 경로: {summary.outputPath}, 용량: {summary.totalSize / 1024 / 1024} MB");
            // 빌드 완료 후 지정된 경로로 결과물 복사
            try
            {
                string destinationDir = @"I:\내 드라이브\공유문서\테스트 빌드\TestBuild";
                // 대상 디렉토리가 없으면 생성합니다.
                if (!System.IO.Directory.Exists(destinationDir))
                {
                    System.IO.Directory.CreateDirectory(destinationDir);
                }

                string sourceFile = summary.outputPath;
                string destFile = System.IO.Path.Combine(destinationDir, System.IO.Path.GetFileName(sourceFile));

                // 파일을 복사합니다. 이미 파일이 존재하면 덮어씁니다.
                System.IO.File.Copy(sourceFile, destFile, true);
                Debug.Log($"빌드 결과물을 다음 경로에 복사했습니다: {destFile}");
            }
            catch (Exception e)
            {
                Debug.LogError($"빌드 결과물 복사 실패: {e.Message}");
            }
        }
        else
        {
            Debug.LogError($"빌드 실패! 에러: {summary.totalErrors}개");
        }
    }

    /// <summary>
    /// Editor Build Settings에서 활성화된 씬 목록을 찾아 배열로 반환합니다.
    /// (이 함수는 원본과 동일하게 유지됩니다.)
    /// </summary>
    private static string[] FindEnabledEditorScenes()
    {
        List<string> editorScenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled) continue;
            editorScenes.Add(scene.path);
        }

        return editorScenes.ToArray();
    }

    // 참고: 원래의 윈도우 빌드 코드도 메뉴 아이템으로 만들어 둘 수 있습니다.
    [MenuItem("Build/Build Windows")]
    static void PerformWindowsBuild()
    {
        //       // 윈도우 빌드 경로는 .exe 확장자를 사용합니다.
        string buildPath = "Builds/Windows/MyGame.exe";
        BuildPipeline.BuildPlayer(FindEnabledEditorScenes(), buildPath, BuildTarget.StandaloneWindows,
            BuildOptions.None);
        Debug.Log($"Windows 빌드 완료! 경로: {buildPath}");
    }
}