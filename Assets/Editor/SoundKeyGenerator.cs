using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Linq;

public class SoundKeyGenerator
{
    // SoundKeys.cs 파일이 저장될 경로입니다.
    private const string SOUND_KEYS_FILE_PATH = "Assets/Scripts/Data/SoundKeys.cs";

    [MenuItem("Tools/Generate Sound Keys")]
    public static void GenerateSoundKeys()
    {
        // 1. 프로젝트 내의 모든 SoundData 에셋을 찾습니다.
        string[] guids = AssetDatabase.FindAssets("t:SoundData");
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "SoundData asset not found. Please create one from the Assets/Create/ScriptableObjects menu.", "OK");
            return;
        }

        if (guids.Length > 1)
        {
            Debug.LogWarning("Multiple SoundData assets found. Using the first one.");
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        SoundData soundData = AssetDatabase.LoadAssetAtPath<SoundData>(path);

        if (soundData == null)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to load SoundData asset from path: {path}", "OK");
            return;
        }

        // 2. SoundData에서 유효한 키를 수집합니다.
        var keys = soundData.audioClips
            .Select(clipInfo => clipInfo.key)
            .Where(key => !string.IsNullOrEmpty(key) && IsValidIdentifier(key))
            .Distinct();

        // 3. SoundKeys.cs 파일 내용을 생성합니다.
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// SoundData에 등록된 오디오 클립의 키 값을 정의합니다.");
        sb.AppendLine("/// 이 파일은 SoundKeyGenerator.cs에 의해 자동으로 생성됩니다.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public enum SoundKeys");
        sb.AppendLine("{");

        foreach (var key in keys)
        {
            sb.AppendLine($"    {key},");
        }

        sb.AppendLine("}");

        // 4. 파일에 내용을 쓰고 AssetDatabase를 새로고침합니다.
        string fullPath = Path.Combine(Application.dataPath, "..", SOUND_KEYS_FILE_PATH);
        File.WriteAllText(fullPath, sb.ToString());
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Success", $"SoundKeys.cs has been generated successfully with {keys.Count()} keys.", "OK");
    }

    // C# 식별자로 유효한지 확인하는 간단한 메서드
    private static bool IsValidIdentifier(string str)
    {
        if (string.IsNullOrEmpty(str) || !char.IsLetter(str[0]) && str[0] != '_')
            return false;

        for (int i = 1; i < str.Length; i++)
        {
            if (!char.IsLetterOrDigit(str[i]) && str[i] != '_')
                return false;
        }

        return true;
    }
}

