using System.Collections.Generic;
using UnityEngine;

public enum Sound
{
    BGM,
    SFX,
    Max // 오디오 소스 배열의 크기를 위해 추가
}

[System.Serializable]
public class AudioClipInfo
{
    public string key;
    public Sound Type;
    public AudioClip clip;
}

#region 씬-BGM 매핑 데이터
/// <summary>
/// [설명]: 특정 씬에 연결된 BGM 클립 정보를 담는 직렬화 가능 구조체입니다.
/// </summary>
[System.Serializable]
public class SceneBgmEntry
{
    [Tooltip("대상 씬 이름 (SceneNames 상수와 동일하게 입력)")]
    public string SceneName;

    [Tooltip("해당 씬에서 재생할 BGM 오디오 클립")]
    public AudioClip BgmClip;

    [Tooltip("크로스페이드 소요 시간 (초). 0이면 즉시 전환")]
    public float CrossfadeDuration = 1.0f;
}
#endregion

[CreateAssetMenu(fileName = "SoundData", menuName = "ScriptableObjects/SoundData")]
public class SoundData : ScriptableObject
{
    public List<AudioClipInfo> audioClips;

    #region 씬-BGM 매핑
    [Header("<color=cyan>씬별 BGM 매핑</color>")]
    [SerializeField, Tooltip("씬 이름별로 재생할 BGM과 크로스페이드 시간을 설정합니다.")]
    private List<SceneBgmEntry> m_sceneBgmMappings = new List<SceneBgmEntry>();

    /// <summary>
    /// [설명]: 씬 이름에 매핑된 BGM 엔트리를 조회합니다.
    /// </summary>
    /// <param name="sceneName">조회할 씬 이름</param>
    /// <returns>매핑된 SceneBgmEntry, 없으면 null</returns>
    public SceneBgmEntry FindBgmEntryByScene(string sceneName)
    {
        if (m_sceneBgmMappings == null) return null;

        for (int i = 0; i < m_sceneBgmMappings.Count; i++)
        {
            if (m_sceneBgmMappings[i] != null &&
                m_sceneBgmMappings[i].SceneName == sceneName)
            {
                return m_sceneBgmMappings[i];
            }
        }

        return null;
    }
    #endregion
}
