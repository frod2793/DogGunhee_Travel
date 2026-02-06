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

[CreateAssetMenu(fileName = "SoundData", menuName = "ScriptableObjects/SoundData")]
public class SoundData : ScriptableObject
{
    public List<AudioClipInfo> audioClips;
}
