using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using InGame.Services;

/// <summary>
/// 오디오를 총괄하는 매니저 클래스입니다.
/// AudioMixer 연동 및 SFX 풀링을 지원합니다.
/// </summary>
public class SoundManager : MonoBehaviour, ISoundManager
{
    #region 싱글톤

    private static SoundManager s_instance;

    public static SoundManager Instance
    {
        get
        {
            if (s_instance == null)
            {
                s_instance = FindFirstObjectByType<SoundManager>();
                if (s_instance == null)
                {
                    LogManager.LogError("SoundManager instance not found in the scene.");
                }
            }
            return s_instance;
        }
    }

    #endregion

    #region 상수 및 설정

    private const string k_MasterVolumeParam = "Master_Volume_Exposed";
    private const string k_BgmVolumeParam = "BGM_Volume_Exposed";
    private const string k_SfxVolumeParam = "SFX_Volume_Exposed";
    private const int k_DefaultSfxPoolSize = 10;
    private const float k_MinSoundInterval = 0.05f;

    #endregion

    #region 변수 및 필드

    [Header("Data")]
    [SerializeField] private SoundData m_soundData;
    [SerializeField] private SettingsData m_settingsData;
    
    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer m_audioMixer;
    [SerializeField] private AudioMixerGroup m_bgmGroup;
    [SerializeField] private AudioMixerGroup m_sfxGroup;

    private AudioSource m_bgmSource;
    private readonly List<AudioSource> m_sfxPool = new List<AudioSource>();
    private readonly Dictionary<string, AudioClip> m_audioClips = new Dictionary<string, AudioClip>();
    private readonly Dictionary<AudioClip, float> m_soundTimers = new Dictionary<AudioClip, float>();

    public float EffectSoundVolume { get; private set; } = 1.0f;
    public float BgmSoundVolume { get; private set; } = 1.0f;

    #endregion

    #region 정적 메서드 (상위 호환성 유지)

    public static void PlaySound(Sound type, string clipName, bool loop = false, float pitch = 1.0f)
    {
        Instance.Play(clipName, type, pitch, loop);
    }

    public static void PlaySound(Sound type, SoundKeys clipKey, bool loop = false, float pitch = 1.0f)
    {
        Instance.Play(clipKey.ToString(), type, pitch, loop);
    }

    #endregion

    #region Unity 라이프사이클

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        DontDestroyOnLoad(gameObject);
        
        Init();
    }
    
    private void OnEnable()
    {
        SettingsData.OnSettingsChanged += LoadSoundSetting;
    }

    private void OnDisable()
    {
        SettingsData.OnSettingsChanged -= LoadSoundSetting;
    }

    #endregion

    #region 초기화

    private void Init()
    {
        // BGM 소스 초기화
        GameObject bgmGo = new GameObject("BGM_Source");
        bgmGo.transform.SetParent(transform);
        m_bgmSource = bgmGo.AddComponent<AudioSource>();
        m_bgmSource.outputAudioMixerGroup = m_bgmGroup;
        m_bgmSource.loop = true;
        m_bgmSource.playOnAwake = false;

        // SFX 풀 초기화
        for (int i = 0; i < k_DefaultSfxPoolSize; i++)
        {
            CreateSfxSource();
        }

        // 클립 데이터 캐싱
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

        LoadSoundSetting();
    }

    private AudioSource CreateSfxSource()
    {
        GameObject sfxGo = new GameObject($"SFX_Source_{m_sfxPool.Count}");
        sfxGo.transform.SetParent(transform);
        AudioSource source = sfxGo.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = m_sfxGroup;
        source.playOnAwake = false;
        m_sfxPool.Add(source);
        return source;
    }

    public void LoadSoundSetting()
    {
        if (m_settingsData != null)
        {
            m_settingsData.LoadSettings();
            SetVolume(Sound.BGM, m_settingsData.BackgroundSoundVolume);
            SetVolume(Sound.SFX, m_settingsData.EffectSoundVolume);
        }
    }

    #endregion

    #region ISoundManager 구현

    public void Play(string clipName, Sound type = Sound.SFX, float pitch = 1.0f, bool loop = false)
    {
        AudioClip clip = GetAudioClip(clipName);
        if (clip == null) return;

        if (type == Sound.BGM)
        {
            PlayBGM(clip, pitch);
        }
        else
        {
            PlaySFX(clip, pitch, Vector3.zero, false);
        }
    }

    public void Play(string clipName, Vector3 position, float pitch = 1.0f)
    {
        AudioClip clip = GetAudioClip(clipName);
        if (clip == null) return;

        PlaySFX(clip, pitch, position, true);
    }

    public void SetVolume(Sound type, float volume)
    {
        // 0일 경우 명시적으로 -144dB(완벽 무음) 처리, 그 외에는 로그 수식 적용
        float db = (volume <= 0.0001f) ? -144.0f : Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f;
        string param = type == Sound.BGM ? k_BgmVolumeParam : k_SfxVolumeParam;

        if (m_audioMixer != null)
        {
            m_audioMixer.SetFloat(param, db);
        }

        if (type == Sound.BGM) BgmSoundVolume = volume;
        else EffectSoundVolume = volume;
    }

    public void Clear()
    {
        m_bgmSource.Stop();
        m_bgmSource.clip = null;

        foreach (var source in m_sfxPool)
        {
            source.Stop();
            source.clip = null;
        }
    }

    #endregion

    #region 내부 재생 로직

    private void PlayBGM(AudioClip clip, float pitch)
    {
        if (m_bgmSource.isPlaying && m_bgmSource.clip == clip) return;

        m_bgmSource.Stop();
        m_bgmSource.clip = clip;
        m_bgmSource.pitch = pitch;
        m_bgmSource.Play();
    }

    private void PlaySFX(AudioClip clip, float pitch, Vector3 position, bool is3D)
    {
        // Throttling
        if (m_soundTimers.TryGetValue(clip, out float lastTime))
        {
            if (Time.time - lastTime < k_MinSoundInterval) return;
        }
        m_soundTimers[clip] = Time.time;

        AudioSource source = GetAvailableSfxSource();
        source.clip = clip;
        source.pitch = pitch;
        
        if (is3D)
        {
            source.transform.position = position;
            source.spatialBlend = 1.0f; // 3D
        }
        else
        {
            source.spatialBlend = 0.0f; // 2D
        }

        source.Play();
    }

    private AudioSource GetAvailableSfxSource()
    {
        foreach (var source in m_sfxPool)
        {
            if (!source.isPlaying) return source;
        }

        return CreateSfxSource(); // 풀이 부족하면 새로 생성
    }

    private AudioClip GetAudioClip(string key)
    {
        if (m_audioClips.TryGetValue(key, out AudioClip clip)) return clip;
        LogManager.LogWarning($"AudioClip not found: {key}", LogManager.LogCategory.SoundManager);
        return null;
    }

    #endregion
}