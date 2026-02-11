using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;
using InGame.Services;

/// <summary>
/// 게임의 전체적인 오디오 시스템을 총괄하는 매니저 클래스입니다.
/// <br/>AudioMixer와의 연동, SFX 오디오 소스 풀링, 볼륨 설정 저장 및 로드를 담당합니다.
/// </summary>
public class SoundManager : MonoBehaviour, ISoundManager
{
    #region 1. 싱글톤 패턴

    private static SoundManager s_instance;

    /// <summary>
    /// SoundManager의 전역 접근 인스턴스입니다.
    /// </summary>
    public static SoundManager Instance
    {
        get
        {
            if (s_instance == null)
            {
                s_instance = FindFirstObjectByType<SoundManager>();
                if (s_instance == null)
                {
                    LogManager.LogError("[SoundManager] 씬 내에서 SoundManager 인스턴스를 찾을 수 없습니다.");
                }
            }

            return s_instance;
        }
    }

    #endregion

    #region 2. 상수 및 설정값

    private const string k_MasterVolumeParam = "Master_Volume_Exposed";
    private const string k_BgmVolumeParam = "BGM_Volume_Exposed";
    private const string k_SfxVolumeParam = "SFX_Volume_Exposed";
    private const int k_DefaultSfxPoolSize = 10;
    private const float k_MinSoundInterval = 0.05f;

    #endregion

    #region 3. 에디터 설정 (Inspector)

    [Header("<color=green>데이터 참조</color>")] [SerializeField, Tooltip("오디오 클립 데이터 ScriptableObject")]
    private SoundData m_soundData;

    [SerializeField, Tooltip("사운드 설정을 저장하는 SettingsData")]
    private SettingsData m_settingsData;

    [Header("<color=green>오디오 믹서 설정</color>")] [SerializeField, Tooltip("사운드 제어용 메인 믹서")]
    private AudioMixer m_audioMixer;

    [SerializeField, Tooltip("BGM 출력을 담당하는 믹서 그룹")]
    private AudioMixerGroup m_bgmGroup;

    [SerializeField, Tooltip("SFX 출력을 담당하는 믹서 그룹")]
    private AudioMixerGroup m_sfxGroup;

    #endregion

    #region 4. 내부 변수 및 상태

    private AudioSource m_bgmSource;
    private readonly List<AudioSource> m_sfxPool = new List<AudioSource>();
    private readonly Dictionary<string, AudioClip> m_audioClips = new Dictionary<string, AudioClip>();
    private readonly Dictionary<AudioClip, float> m_soundTimers = new Dictionary<AudioClip, float>();

    /// <summary> 현재 반영된 효과음 볼륨 </summary>
    public float EffectSoundVolume { get; private set; } = 1.0f;

    /// <summary> 현재 반영된 배경음 볼륨 </summary>
    public float BgmSoundVolume { get; private set; } = 1.0f;

    #endregion

    #region 5. 공개 정적 메서드 (상위 호환성)

    /// <summary>
    /// 간단하게 클립 이름으로 사운드를 재생합니다.
    /// </summary>
    public static void PlaySound(Sound type, string clipName, bool loop = false, float pitch = 1.0f)
    {
        Instance.Play(clipName, type, pitch, loop);
    }

    /// <summary>
    /// SoundKeys 열거형을 사용하여 사운드를 재생합니다.
    /// </summary>
    public static void PlaySound(Sound type, SoundKeys clipKey, bool loop = false, float pitch = 1.0f)
    {
        Instance.Play(clipKey.ToString(), type, pitch, loop);
    }

    #endregion

    #region 6. 유니티 생명주기

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeSystems();
    }

    private void OnEnable()
    {
        // 설정 데이터 변경 시 사운드 설정 실시간 로드
        SettingsData.OnSettingsChanged += LoadSoundSetting;
    }

    private void OnDisable()
    {
        SettingsData.OnSettingsChanged -= LoadSoundSetting;
    }

    #endregion

    #region 7. 초기화 및 내부 생성 로직

    /// <summary>
    /// 믹서 소스 생성 및 클립 데이터 캐싱을 실행합니다.
    /// </summary>
    private void InitializeSystems()
    {
        // 1. BGM 전용 오디오 소스 생성
        GameObject bgmGo = new GameObject("BGM_Source");
        bgmGo.transform.SetParent(transform);
        m_bgmSource = bgmGo.AddComponent<AudioSource>();
        m_bgmSource.outputAudioMixerGroup = m_bgmGroup;
        m_bgmSource.loop = true;
        m_bgmSource.playOnAwake = false;

        // 2. SFX 풀 초기 생성
        for (int i = 0; i < k_DefaultSfxPoolSize; i++)
        {
            CreateSfxSource();
        }

        // 3. SoundData 클립을 효율적인 조회를 위해 딕셔너리에 캐싱
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

    /// <summary>
    /// SFX 오디오 소스 오브젝트를 생성하여 풀에 추가합니다.
    /// </summary>
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

    /// <summary>
    /// 저장된 설정 데이터로부터 볼륨 정보를 불러와 시스템에 반영합니다.
    /// </summary>
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

    #region 8. ISoundManager 인터페이스 구현

    /// <summary>
    /// 타입(BGM/SFX)에 맞춰 사운드를 재생합니다.
    /// </summary>
    public void Play(string clipName, Sound type = Sound.SFX, float pitch = 1.0f, bool loop = false)
    {
        AudioClip clip = FindAudioClip(clipName);
        if (clip == null)
        {
            return;
        }

        if (type == Sound.BGM)
        {
            PlayBGMInternal(clip, pitch);
        }
        else
        {
            PlaySFXInternal(clip, pitch, Vector3.zero, false);
        }
    }

    /// <summary>
    /// 지정된 월드 좌표에서 SFX를 재생합니다.
    /// </summary>
    public void Play(string clipName, Vector3 position, float pitch = 1.0f)
    {
        AudioClip clip = FindAudioClip(clipName);
        if (clip == null)
        {
            return;
        }

        PlaySFXInternal(clip, pitch, position, true);
    }

    /// <summary>
    /// 오디오 믹서를 통해 특정 타입의 볼륨(dB)을 조절합니다.
    /// </summary>
    public void SetVolume(Sound type, float volume)
    {
        // 선형 볼륨(0~1)을 믹서 데시벨(-144~0)로 변환
        float db = (volume <= 0.0001f) ? -144.0f : Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f;
        string param = type == Sound.BGM ? k_BgmVolumeParam : k_SfxVolumeParam;

        if (m_audioMixer != null)
        {
            m_audioMixer.SetFloat(param, db);
        }

        if (type == Sound.BGM)
        {
            BgmSoundVolume = volume;
        }
        else
        {
            EffectSoundVolume = volume;
        }
    }

    /// <summary>
    /// 현재 재생 중인 모든 사운드를 정지하고 정리합니다.
    /// </summary>
    public void Clear()
    {
        if (m_bgmSource != null)
        {
            m_bgmSource.Stop();
            m_bgmSource.clip = null;
        }

        foreach (var source in m_sfxPool)
        {
            if (source != null)
            {
                source.Stop();
                source.clip = null;
            }
        }
    }

    #endregion

    #region 9. 내부 재생 구현

    private void PlayBGMInternal(AudioClip clip, float pitch)
    {
        if (m_bgmSource.isPlaying && m_bgmSource.clip == clip)
        {
            return;
        }

        m_bgmSource.Stop();
        m_bgmSource.clip = clip;
        m_bgmSource.pitch = pitch;
        m_bgmSource.Play();
    }

    private void PlaySFXInternal(AudioClip clip, float pitch, Vector3 position, bool is3D)
    {
        // 동일 사운드 중첩 방지 (Throttling)
        if (m_soundTimers.TryGetValue(clip, out float lastTime))
        {
            if (Time.time - lastTime < k_MinSoundInterval)
            {
                return;
            }
        }

        m_soundTimers[clip] = Time.time;

        AudioSource source = GetAvailableSfxSource();
        source.clip = clip;
        source.pitch = pitch;

        if (is3D)
        {
            source.transform.position = position;
            source.spatialBlend = 1.0f;
        }
        else
        {
            source.spatialBlend = 0.0f;
        }

        source.Play();
    }

    private AudioSource GetAvailableSfxSource()
    {
        foreach (var source in m_sfxPool)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }

        return CreateSfxSource(); // 여유분이 없으면 동적으로 풀 확장
    }

    private AudioClip FindAudioClip(string key)
    {
        if (m_audioClips.TryGetValue(key, out AudioClip clip))
        {
            return clip;
        }

        LogManager.LogWarning($"[SoundManager] 오디오 클립을 찾을 수 없습니다: {key}", LogManager.LogCategory.SoundManager);
        return null;
    }

    #endregion
}