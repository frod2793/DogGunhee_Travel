using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using InGame.Services;

/// <summary>
/// [설명]: 게임의 전체적인 오디오 시스템을 총괄하는 매니저 클래스입니다.
/// AudioMixer와의 연동, SFX 오디오 소스 풀링, 볼륨 설정 저장 및 로드를 담당합니다.
/// 씬 전환 시 SoundData의 SceneBgmMapping을 기반으로 자동 크로스페이드 BGM 전환을 수행합니다.
/// </summary>
public class SoundManager : MonoBehaviour, ISoundManager
{

    #region 상수 및 설정값

    private const string k_MasterVolumeParam = "Master_Volume_Exposed";
    private const string k_BgmVolumeParam = "BGM_Volume_Exposed";
    private const string k_SfxVolumeParam = "SFX_Volume_Exposed";
    private const int k_DefaultSfxPoolSize = 10;
    private const float k_MinSoundInterval = 0.05f;
    private const float k_DefaultCrossfadeDuration = 1.0f;

    #endregion

    #region 에디터 설정

    [Header("<color=green>데이터 참조</color>")]
    [SerializeField, Tooltip("오디오 클립 데이터 ScriptableObject")]
    private SoundData m_soundData;

    [SerializeField, Tooltip("사운드 설정을 저장하는 SettingsData")]
    private SettingsData m_settingsData;

    [Header("<color=green>오디오 믹서 설정</color>")]
    [SerializeField, Tooltip("사운드 제어용 메인 믹서")]
    private AudioMixer m_audioMixer;

    [SerializeField, Tooltip("BGM 출력을 담당하는 믹서 그룹")]
    private AudioMixerGroup m_bgmGroup;

    [SerializeField, Tooltip("SFX 출력을 담당하는 믹서 그룹")]
    private AudioMixerGroup m_sfxGroup;

    #endregion

    #region 내부 필드 및 상태

    /// <summary> [설명]: 현재 활성 BGM 소스 (듀얼 소스 중 하나) </summary>
    private AudioSource m_bgmSourceA;

    /// <summary> [설명]: 크로스페이드용 보조 BGM 소스 </summary>
    private AudioSource m_bgmSourceB;

    /// <summary> [설명]: 현재 메인으로 재생 중인 BGM 소스 참조 </summary>
    private AudioSource m_activeBgmSource;

    private readonly List<AudioSource> m_sfxPool = new List<AudioSource>();
    private readonly Dictionary<string, AudioClip> m_audioClips = new Dictionary<string, AudioClip>();
    private readonly Dictionary<AudioClip, float> m_soundTimers = new Dictionary<AudioClip, float>();

    /// <summary> [설명]: 크로스페이드 진행 중 취소를 위한 토큰 소스 </summary>
    private CancellationTokenSource m_crossfadeCts;

    /// <summary> [설명]: 현재 반영된 효과음 볼륨 </summary>
    public float EffectSoundVolume { get; private set; } = 1.0f;

    /// <summary> [설명]: 현재 반영된 배경음 볼륨 </summary>
    public float BgmSoundVolume { get; private set; } = 1.0f;

    #endregion
    

    #region 유니티 생명주기

    private void Awake()
    {
        if (transform.parent != null)
        {
            transform.SetParent(null);
        }
        DontDestroyOnLoad(gameObject);
        InitializeSystems();
    }

    private void OnEnable()
    {
        // 설정 데이터 변경 시 사운드 설정 실시간 로드
        SettingsData.OnSettingsChanged += LoadSoundSetting;

        // 씬 전환 시 자동 BGM 전환 이벤트 등록
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        SettingsData.OnSettingsChanged -= LoadSoundSetting;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnDestroy()
    {
        CancelCrossfade();
    }

    #endregion

    #region 초기화

    /// <summary>
    /// [설명]: 듀얼 BGM 소스 생성, SFX 풀 초기화, 클립 데이터 캐싱을 실행합니다.
    /// </summary>
    private void InitializeSystems()
    {
        // 1. 듀얼 BGM 오디오 소스 생성 (크로스페이드용)
        m_bgmSourceA = CreateBgmSource("BGM_Source_A");
        m_bgmSourceB = CreateBgmSource("BGM_Source_B");
        m_activeBgmSource = m_bgmSourceA;

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
    /// [설명]: BGM 전용 AudioSource를 생성합니다.
    /// </summary>
    /// <param name="sourceName">생성될 게임오브젝트 이름</param>
    /// <returns>생성된 AudioSource</returns>
    private AudioSource CreateBgmSource(string sourceName)
    {
        GameObject bgmGo = new GameObject(sourceName);
        bgmGo.transform.SetParent(transform);
        AudioSource source = bgmGo.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = m_bgmGroup;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
        return source;
    }

    /// <summary>
    /// [설명]: SFX 오디오 소스 오브젝트를 생성하여 풀에 추가합니다.
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
    /// [설명]: 저장된 설정 데이터로부터 볼륨 정보를 불러와 시스템에 반영합니다.
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

    #region 씬 전환 BGM 관리

    /// <summary>
    /// [설명]: SceneManager.activeSceneChanged 이벤트 핸들러입니다.
    /// 새 씬에 매핑된 BGM이 있으면 크로스페이드로 자연스럽게 전환합니다.
    /// </summary>
    /// <param name="previousScene">이전 활성 씬</param>
    /// <param name="newScene">새로 활성화된 씬</param>
    private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        TransitionBGMForScene(newScene.name);
    }

    /// <summary>
    /// [설명]: 씬 이름에 매핑된 BGM으로 크로스페이드 전환합니다.
    /// SoundData의 SceneBgmMapping에 등록된 클립과 페이드 시간을 사용합니다.
    /// </summary>
    /// <param name="sceneName">전환 대상 씬 이름</param>
    public void TransitionBGMForScene(string sceneName)
    {
        if (m_soundData == null)
        {
            LogManager.LogWarning("[SoundManager] SoundData가 할당되지 않았습니다.",
                LogManager.LogCategory.SoundManager);
            return;
        }

        SceneBgmEntry entry = m_soundData.FindBgmEntryByScene(sceneName);
        if (entry == null)
        {
            LogManager.Log($"[SoundManager] 씬 '{sceneName}'에 매핑된 BGM이 없습니다. BGM을 유지합니다.",
                LogManager.LogCategory.SoundManager);
            return;
        }

        if (entry.BgmClip == null)
        {
            LogManager.LogWarning($"[SoundManager] 씬 '{sceneName}'의 BGM 클립이 null입니다.",
                LogManager.LogCategory.SoundManager);
            return;
        }

        // 현재 재생 중인 BGM과 동일한 클립이면 전환하지 않음
        if (m_activeBgmSource != null &&
            m_activeBgmSource.isPlaying &&
            m_activeBgmSource.clip == entry.BgmClip)
        {
            return;
        }

        float fadeDuration = entry.CrossfadeDuration > 0f
            ? entry.CrossfadeDuration
            : k_DefaultCrossfadeDuration;

        StartBGMCrossfade(entry.BgmClip, fadeDuration);
    }

    /// <summary>
    /// [설명]: 듀얼 AudioSource를 활용하여 현재 BGM에서 새 BGM으로 부드럽게 크로스페이드합니다.
    /// 이전 BGM이 재생 중이지 않으면 즉시 재생합니다.
    /// </summary>
    /// <param name="newClip">새로 재생할 BGM 클립</param>
    /// <param name="duration">크로스페이드 소요 시간 (초)</param>
    private void StartBGMCrossfade(AudioClip newClip, float duration)
    {
        CancelCrossfade();

        // 이전 BGM이 재생 중이지 않으면 즉시 재생 (크로스페이드 불필요)
        bool hasOutgoing = m_activeBgmSource != null && m_activeBgmSource.isPlaying;

        if (!hasOutgoing)
        {
            // 즉시 재생: 활성 소스에 직접 설정
            m_activeBgmSource.clip = newClip;
            m_activeBgmSource.volume = 1f;
            m_activeBgmSource.Play();
            LogManager.Log($"[SoundManager] BGM 즉시 재생: {newClip.name}",
                LogManager.LogCategory.SoundManager);
            return;
        }

        // 이전 BGM이 있으면 크로스페이드 시작
        CrossfadeBGMAsync(newClip, duration).Forget();
    }

    /// <summary>
    /// [설명]: 듀얼 AudioSource를 활용하여 현재 BGM에서 새 BGM으로 부드럽게 크로스페이드합니다.
    /// 이전 크로스페이드가 진행 중이면 취소 후 새 전환을 시작합니다.
    /// </summary>
    /// <param name="newClip">새로 재생할 BGM 클립</param>
    /// <param name="duration">크로스페이드 소요 시간 (초)</param>
    private async UniTaskVoid CrossfadeBGMAsync(AudioClip newClip, float duration)
    {
        m_crossfadeCts = new CancellationTokenSource();
        CancellationToken ct = m_crossfadeCts.Token;

        // 현재 활성 소스와 비활성 소스를 결정
        AudioSource outgoingSource = m_activeBgmSource;
        AudioSource incomingSource = (m_activeBgmSource == m_bgmSourceA)
            ? m_bgmSourceB
            : m_bgmSourceA;

        // 새 소스 준비
        incomingSource.clip = newClip;
        incomingSource.volume = 0f;
        incomingSource.Play();

        float elapsed = 0f;
        float outgoingStartVolume = outgoingSource.volume;

        try
        {
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 부드러운 S커브 보간으로 자연스러운 전환
                float smoothT = t * t * (3f - 2f * t);

                incomingSource.volume = Mathf.Lerp(0f, 1f, smoothT);
                outgoingSource.volume = Mathf.Lerp(outgoingStartVolume, 0f, smoothT);

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }
        catch (System.OperationCanceledException)
        {
            // 크로스페이드가 취소됨 — 별도 처리 불필요
            return;
        }

        // 최종 볼륨 보정
        incomingSource.volume = 1f;
        outgoingSource.Stop();
        outgoingSource.clip = null;
        outgoingSource.volume = 0f;

        // 활성 소스 교체
        m_activeBgmSource = incomingSource;

        LogManager.Log($"[SoundManager] BGM 크로스페이드 완료: {newClip.name}",
            LogManager.LogCategory.SoundManager);
    }

    /// <summary>
    /// [설명]: 진행 중인 크로스페이드를 취소하고 CTS를 정리합니다.
    /// </summary>
    private void CancelCrossfade()
    {
        if (m_crossfadeCts != null)
        {
            m_crossfadeCts.Cancel();
            m_crossfadeCts.Dispose();
            m_crossfadeCts = null;
        }
    }

    #endregion

    #region ISoundManager 구현

    /// <summary>
    /// [설명]: 타입(BGM/SFX)에 맞춰 사운드를 재생합니다.
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
    /// [설명]: 지정된 월드 좌표에서 SFX를 재생합니다.
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
    /// [설명]: 오디오 믹서를 통해 특정 타입의 볼륨(dB)을 조절합니다.
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
    /// [설명]: 현재 재생 중인 모든 사운드를 정지하고 정리합니다.
    /// </summary>
    public void Clear()
    {
        CancelCrossfade();

        if (m_bgmSourceA != null)
        {
            m_bgmSourceA.Stop();
            m_bgmSourceA.clip = null;
            m_bgmSourceA.volume = 0f;
        }

        if (m_bgmSourceB != null)
        {
            m_bgmSourceB.Stop();
            m_bgmSourceB.clip = null;
            m_bgmSourceB.volume = 0f;
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

    #region 내부 비즈니스 로직

    /// <summary>
    /// [설명]: BGM을 전환합니다.
    /// 이전 BGM이 재생 중이면 크로스페이드, 없으면 즉시 재생합니다.
    /// </summary>
    private void PlayBGMInternal(AudioClip clip, float pitch)
    {
        if (m_activeBgmSource != null &&
            m_activeBgmSource.isPlaying &&
            m_activeBgmSource.clip == clip)
        {
            return;
        }

        StartBGMCrossfade(clip, k_DefaultCrossfadeDuration);
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