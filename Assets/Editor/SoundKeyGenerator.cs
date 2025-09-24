using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

/// <summary>
/// 프로젝트 내의 모든 SoundData 에셋을 기반으로 SoundKeys enum을 자동으로 생성하는 에디터 스크립트입니다.
/// </summary>
public class SoundKeyGenerator
{
    // SoundKeys.cs 파일이 위치한 경로입니다.
    private const string FilePath = "Assets/Scripts/Data/SoundKeys.cs";

    [MenuItem("Tools/Generate SoundKeys Enum")]
    public static void GenerateSoundKeysEnum()
    {
        // 1. 프로젝트 내의 모든 SoundData 에셋 찾기
        string[] guids = AssetDatabase.FindAssets("t:SoundData");
        if (guids.Length == 0)
        {
            Debug.LogWarning("No SoundData assets found in the project.");
            return;
        }

        // 2. 모든 SoundData에서 고유한 키 수집 (중복 방지)
        var keys = new HashSet<string>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SoundData soundData = AssetDatabase.LoadAssetAtPath<SoundData>(path);
            if (soundData != null)
            {
                foreach (var audioInfo in soundData.audioClips)
                {
                    if (!string.IsNullOrEmpty(audioInfo.key) && IsValidIdentifier(audioInfo.key))
                    {
                        keys.Add(audioInfo.key);
                    }
                }
            }
        }

        // 3. SoundKeys.cs 파일 내용 생성
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("/// <summary>");
        stringBuilder.AppendLine("/// SoundData에 등록된 오디오 클립의 키 값을 정의합니다.");
        stringBuilder.AppendLine("/// 이 파일은 SoundKeyGenerator.cs에 의해 자동으로 생성됩니다.");
        stringBuilder.AppendLine("/// </summary>");
        stringBuilder.AppendLine("public enum SoundKeys");
        stringBuilder.AppendLine("{");

        foreach (string key in keys.OrderBy(k => k)) // 키를 알파벳 순으로 정렬하여 가독성 향상
        {
            stringBuilder.AppendLine($"    {key},");
        }

        stringBuilder.AppendLine("}");

        // 4. 파일 쓰기 및 에셋 데이터베이스 새로고침
        File.WriteAllText(FilePath, stringBuilder.ToString());
        AssetDatabase.Refresh();

        Debug.Log($"SoundKeys.cs has been successfully generated with {keys.Count} keys.");
    }

    // C# 식별자로 유효한지 간단히 확인하는 함수
    private static bool IsValidIdentifier(string str)
    {
        if (string.IsNullOrEmpty(str) || !char.IsLetter(str[0]) && str[0] != '_') return false;
        for (int i = 1; i < str.Length; i++)
        {
            if (!char.IsLetterOrDigit(str[i]) && str[i] != '_') return false;
        }
        return true;
    }
}