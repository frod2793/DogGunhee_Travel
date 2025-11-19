using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
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
            // 1. 현재 번들 버전 코드(Version Code) 값을 가져옵니다.
            int currentVersionCode = PlayerSettings.Android.bundleVersionCode;
            
            // 2. 값을 1 증가시키고 PlayerSettings에 저장합니다.
            PlayerSettings.Android.bundleVersionCode = currentVersionCode + 1;
            
            Debug.Log($"[UCB Auto] Android Bundle Version Code updated: {currentVersionCode} -> {PlayerSettings.Android.bundleVersionCode}");

            // 3. 변경사항을 강제로 저장소에 반영합니다.
            // UCB 환경에서 변경사항이 PlayerSettings 파일에 저장되도록 하여 다음 빌드에 반영되게 합니다.
            EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<PlayerSettings>("ProjectSettings/ProjectSettings.asset"));
            AssetDatabase.SaveAssets();
        }
    }
}