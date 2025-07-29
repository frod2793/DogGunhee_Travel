using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SoundManager : MonoBehaviour
{
    #region 싱글톤

    private static SoundManager _instance;

    public static SoundManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // 씬에서 SoundManager를 찾아봅니다.
                _instance = FindFirstObjectByType<SoundManager>();
                // 씬에 SoundManager가 없다면, 새로 생성하지 않고 경고를 출력합니다.
                // SoundManager는 씬에 미리 배치하고 SoundData를 할당해야 합니다.
                if (_instance == null)
                {
                    Debug.LogError(
                        "SoundManager instance not found in the scene. Please add SoundManager to your scene and assign SoundData.");
                }
            }

            return _instance;
        }
    }

    #endregion

    #region 변수 및 필드

    [SerializeField] private SoundData soundData; // 인스펙터에서 할당할 SoundData

    [SerializeField] private SettingsData_oBJ settingsData;
    
    AudioSource[] _audioSources = new AudioSource[(int)Sound.Max];
    Dictionary<string, AudioClip> _audioClips = new Dictionary<string, AudioClip>();
    private float _effectsoundVolum = 1.0f;
    private float _bgmSoundVolum = 1.0f;

    #endregion

    #region 정적 메서드

    // 정적 호출 지원
    public static void PlaySound(Sound type, string clipName, bool loop = false, float pitch = 1.0f)
    {
        Instance.Play(clipName, type, pitch, loop);
    }

    public static void PlaySound(Sound type, SoundKeys clipKey, bool loop = false, float pitch = 1.0f)
    {
        Instance.Play(clipKey.ToString(), type, pitch, loop);
    }

    #endregion

    #region 초기화 메서드

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        Init();
        DontDestroyOnLoad(gameObject);
    }

    public void Init()
    {
        if (_audioSources[0] != null) return; // 이미 초기화된 경우 중복 방지

        GameObject root = new GameObject { name = "Sound" };
        root.transform.parent = transform; // SoundManager의 자식으로 관리

        string[] SoundNames = System.Enum.GetNames(typeof(Sound));
        for (int i = 0; i < SoundNames.Length - 1; i++)
        {
            GameObject go = new GameObject { name = SoundNames[i] };
            _audioSources[i] = go.AddComponent<AudioSource>();
            go.transform.parent = root.transform;
        }

        _audioSources[(int)Sound.BGM].loop = true;

        // 할당된 SoundData에서 오디오 클립 초기화
        if (soundData != null)
        {
            foreach (var audioInfo in soundData.audioClips)
            {
                if (!_audioClips.ContainsKey(audioInfo.key))
                {
                    _audioClips.Add(audioInfo.key, audioInfo.clip);
                }
            }
        }
        else
        {
            Debug.LogError("SoundData가 SoundManager에 할당되지 않았습니다. 인스펙터에서 할당해주세요.");
        }

        LoadSoundSetting();
    }


    public void LoadSoundSetting()
    {
        
        settingsData.LoadSettings();
        
        if (settingsData == null)
        {
            Debug.LogError("SettingsData_oBJ가 SoundManager에 할당되지 않았습니다. 인스펙터에서 할당해주세요.");
            return;
        }
        // 배경음과 효과음 볼륨 설정
        _bgmSoundVolum = settingsData.backgroundSoundVolume;
        _effectsoundVolum = settingsData.effectSoundVolume;

        // 초기 볼륨 설정
        VolumSet(Sound.BGM, _bgmSoundVolum);
        VolumSet(Sound.SFX, _effectsoundVolum);
    }
    
    public void Clear()
    {
        foreach (AudioSource audioSource in _audioSources)
        {
            if (audioSource == null) continue;
            audioSource.clip = null;
            audioSource.Stop();
        }

        _audioClips.Clear();
    }

    #endregion

    #region 오디오 재생 메서드

    public void Play(AudioClip audioClip, Sound type = Sound.SFX, float pitch = 1.0f, bool loop = false)
    {
        if (audioClip == null)
            return;

        AudioSource audioSource = _audioSources[(int)type];
        if (audioSource == null) return;

        audioSource.pitch = pitch;

        if (type == Sound.BGM)
        {
            if (audioSource.isPlaying)
                audioSource.Stop();
            audioSource.volume = _bgmSoundVolum;
            audioSource.loop = loop;
            audioSource.clip = audioClip;
            audioSource.Play();
            //성공 로그 출력 
            Debug.Log($"Playing BGM: {audioClip.name} with volume: {_bgmSoundVolum}");
        }
        else // Effect
        {
            audioSource.volume = _effectsoundVolum;
            audioSource.loop = false;
            audioSource.PlayOneShot(audioClip);
        }
    }

    public void Play(string path, Sound type = Sound.SFX, float pitch = 1.0f, bool loop = false)
    {
        AudioClip audioClip = GetAudioClip(path);
        Play(audioClip, type, pitch, loop);
    }

    #endregion

    #region 유틸리티 메서드

    public void VolumSet(Sound type = Sound.SFX, float volum = 1.0f)
    {
        if (type == Sound.BGM)
        {
            _bgmSoundVolum = volum;
            // 현재 재생 중인 BGM의 볼륨을 즉시 업데이트
            if (_audioSources[(int)Sound.BGM] != null)
            {
                _audioSources[(int)Sound.BGM].volume = _bgmSoundVolum;
            }
        }
        else if (type == Sound.SFX)
        {
            _effectsoundVolum = volum;
            // SFX는 PlayOneShot을 사용하므로 다음 재생부터 적용됨

            if (_audioSources[(int)Sound.SFX] != null)
            {
                _audioSources[(int)Sound.SFX].volume = _effectsoundVolum;
            }

            //Debug.Log($"SFX volume updated to: {_effectsoundVolum}");
        }
    }

    AudioClip GetAudioClip(string key)
    {
        if (_audioClips.TryGetValue(key, out AudioClip audioClip))
        {
            return audioClip;
        }

        Debug.LogWarning($"AudioClip not found: {key}");
        return null;
    }

    #endregion
}