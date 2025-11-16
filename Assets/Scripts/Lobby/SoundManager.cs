using System.Collections;
using System.Collections.Generic;
using UnityEngine.Serialization;
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
                    LogManager.LogError(
                        "SoundManager instance not found in the scene. Please add SoundManager to your scene and assign SoundData.");
                }
            }

            return _instance;
        }
    }

    #endregion

    #region 변수 및 필드

    [FormerlySerializedAs("soundData")]
    [Tooltip("재생할 오디오 클립들의 데이터입니다.")]
    [SerializeField] private SoundData m_soundData;

    [FormerlySerializedAs("settingsData")]
    [Tooltip("게임의 사운드 설정 데이터입니다.")]
    [SerializeField] private SettingsData_oBJ m_settingsData;
    
    private AudioSource[] m_audioSources = new AudioSource[(int)Sound.Max];
    private readonly Dictionary<string, AudioClip> m_audioClips = new Dictionary<string, AudioClip>();
    public float EffectSoundVolume { get; private set; } = 1.0f;
    public float BgmSoundVolume { get; private set; } = 1.0f;

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

    private void Init()
    {
        if (m_audioSources[0] != null) return; // 이미 초기화된 경우 중복 방지

        GameObject root = new GameObject { name = "Sound" };
        root.transform.parent = transform;

        string[] SoundNames = System.Enum.GetNames(typeof(Sound));
        for (int i = 0; i < SoundNames.Length - 1; i++)
        {
            GameObject go = new GameObject { name = SoundNames[i] };
            m_audioSources[i] = go.AddComponent<AudioSource>();
            go.transform.parent = root.transform;
        }

        m_audioSources[(int)Sound.BGM].loop = true;

        // 할당된 SoundData에서 오디오 클립 초기화
        if (m_soundData != null)
        {
            foreach (var audioInfo in m_soundData.audioClips)
            {
                if (!m_audioClips.ContainsKey(audioInfo.key))
                {
                    m_audioClips.Add(audioInfo.key, audioInfo.clip);
                }
            }
        }
        else
        {
            LogManager.LogError("SoundData가 SoundManager에 할당되지 않았습니다. 인스펙터에서 할당해주세요.");
        }

        LoadSoundSetting();
    }


    public void LoadSoundSetting()
    {
        // 데이터 로딩 책임은 OptionPopupManager 또는 게임 시작 시점으로 일원화합니다.
        // SoundManager는 외부에서 설정된 값을 받아 적용하는 역할만 수행하여 데이터 충돌을 방지합니다.
        // settingsData.LoadSettings();

        if (m_settingsData == null)
        {
            LogManager.LogError("SettingsData_oBJ가 SoundManager에 할당되지 않았습니다. 인스펙터에서 할당해주세요.");
            // settingsData가 없어도 기본 볼륨으로 동작하도록 설정
            BgmSoundVolume = 1.0f;
            EffectSoundVolume = 1.0f;
        }
        else
        {
            // 배경음과 효과음 볼륨 설정
            BgmSoundVolume = m_settingsData.backgroundSoundVolume;
            EffectSoundVolume = m_settingsData.effectSoundVolume;
        }

        // 초기 볼륨 설정
        SetVolume(Sound.BGM, BgmSoundVolume);
        SetVolume(Sound.SFX, EffectSoundVolume);
    }
    
    public void Clear()
    {
        foreach (AudioSource audioSource in m_audioSources)
        {
            if (audioSource == null) continue;
            audioSource.clip = null;
            audioSource.Stop();
        }

        m_audioClips.Clear();
    }

    #endregion

    #region 오디오 재생 메서드

    public void Play(AudioClip audioClip, Sound type = Sound.SFX, float pitch = 1.0f, bool loop = false)
    {
        if (audioClip == null)
            return;

        AudioSource audioSource = m_audioSources[(int)type];
        if (audioSource == null) return;

        audioSource.pitch = pitch;

        if (type == Sound.BGM)
        {
            // 이미 같은 BGM이 재생 중이면 다시 재생하지 않음
            if (audioSource.isPlaying && audioSource.clip == audioClip)
                return;
            
            if(audioSource.isPlaying)
                audioSource.Stop();
            
            audioSource.volume = BgmSoundVolume;
            audioSource.loop = loop;
            audioSource.clip = audioClip;
            audioSource.Play();
            LogManager.Log($"Playing BGM: {audioClip.name} with volume: {BgmSoundVolume}", LogManager.LogCategory.SoundManager);
        }
        else // Effect
        {
            audioSource.volume = EffectSoundVolume;
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

    public void SetVolume(Sound type, float volume)
    {
        if (type == Sound.BGM)
        {
            BgmSoundVolume = volume;
            if (m_audioSources[(int)Sound.BGM] != null)
            {
                m_audioSources[(int)Sound.BGM].volume = BgmSoundVolume;
            }
            LogManager.Log($"BGM volume updated to: {BgmSoundVolume}", LogManager.LogCategory.SoundManager);
        }
        else if (type == Sound.SFX)
        {
            EffectSoundVolume = volume;
            if (m_audioSources[(int)Sound.SFX] != null)
            {
                m_audioSources[(int)Sound.SFX].volume = EffectSoundVolume;
            }
            LogManager.Log($"SFX volume updated to: {EffectSoundVolume}", LogManager.LogCategory.SoundManager);
        }
    }

    AudioClip GetAudioClip(string key)
    {
        if (m_audioClips.TryGetValue(key, out AudioClip audioClip))
        {
            return audioClip;
        }

        LogManager.LogWarning($"AudioClip not found: {key}", LogManager.LogCategory.SoundManager);
        return null;
    }

    #endregion
}