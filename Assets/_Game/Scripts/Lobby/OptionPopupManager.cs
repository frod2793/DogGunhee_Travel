using System;
using InGame;
using InGame.vamsir;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class OptionPopupManager : MonoBehaviour
{
    #region 필드 및 프로퍼티

    [Header("참조 데이터 및 프리팹")]
    [Tooltip("게임의 전반적인 설정을 관리하는 ScriptableObject입니다.")]
    [FormerlySerializedAs("settingsData")]
    [SerializeField] private SettingsData m_settingsData;
    [Tooltip("동적으로 생성할 조이스틱 설정 팝업 프리팹입니다.")]
    [FormerlySerializedAs("joysticSetterPopUpPrefb")]
    [SerializeField] private JoystickSetter m_joystickSetterPopupPrefab;

    [Header("UI 컴포넌트")]
    [Tooltip("효과음 볼륨을 조절하는 슬라이더입니다.")]
    [FormerlySerializedAs("effectSoundVolum")]
    [SerializeField] private Slider m_effectSoundVolumeSlider;
    [Tooltip("배경음 볼륨을 조절하는 슬라이더입니다.")]
    [FormerlySerializedAs("bgMsoundVolum")]
    [SerializeField] private Slider m_bgmSoundVolumeSlider;
    [Tooltip("설정 창을 닫고 변경사항을 저장하는 버튼입니다.")]
    [FormerlySerializedAs("exitBtn")]
    [SerializeField] private Button m_exitButton;
    [Tooltip("조이스틱 설정 팝업을 여는 버튼입니다.")]
    [FormerlySerializedAs("joystickSizeBtn")]
    [SerializeField] private Button m_joystickSizeButton;
    [Tooltip("게임의 목표 프레임(FPS)을 설정하는 슬라이더입니다.")]
    [SerializeField] private Slider m_frameRateSlider;
    [Tooltip("현재 설정된 FPS 값을 표시하는 텍스트입니다.")]
    [SerializeField] private TMP_Text m_frameRateValueText;
    
    // --- 내부 상태 변수 ---
    /// <summary>
    /// R3 구독을 관리하여 메모리 누수를 방지합니다.
    /// </summary>
    private readonly CompositeDisposable m_disposables = new();
    /// <summary>
    /// 사운드 재생 및 볼륨 조절을 위한 SoundManager 인스턴스입니다.
    /// </summary>
    private SoundManager m_soundManager;
    /// <summary>
    /// 전역 데이터 및 설정을 관리하는 PlayerDataManager 인스턴스입니다.
    /// </summary>
    private PlayerDataManager m_playerDataManager;
    /// <summary>
    /// 이 팝업이 속한 최상위 Canvas입니다.
    /// </summary>
    private Canvas m_canvas;

    // 프레임 설정 관련 상수
    private readonly int[] m_frameRateOptions = { 30, 60, 120 };

    #endregion

    #region Unity 라이프사이클

    private void Awake()
    {
        InitializeComponents();
        BindUIEvents();
    }

    private void OnEnable()
    {
        // 컴포넌트가 활성화될 때마다 최신 설정값을 UI에 적용합니다.
        LoadAndApplySettings();
    }

    private void OnDestroy()
    {
        // CompositeDisposable을 사용하여 모든 구독을 한 번에 정리합니다.
        m_disposables.Dispose();
    }

    #endregion

    #region 초기화 및 설정

    /// <summary>
    /// 컴포넌트 초기화 및 이벤트 리스너 등록
    /// </summary>
    private void InitializeComponents()
    {
        m_soundManager = SoundManager.Instance;
        m_playerDataManager = PlayerDataManager.Instance;
        if (m_soundManager == null)
        {
            Debug.LogError("SoundManager를 찾을 수 없습니다. OptionPopupManager가 정상적으로 작동하지 않을 수 있습니다.");
        }

        // 설정 데이터 검증
        if (m_settingsData == null)
        {
            Debug.LogError("SettingsData가 할당되지 않았습니다!");
            return;
        }

        // 팝업이 속한 Canvas를 찾아서 참조합니다.
        m_canvas = GetComponentInParent<Canvas>();
        if (m_canvas == null)
        {
            Debug.LogError("상위 오브젝트에서 Canvas를 찾을 수 없습니다!", this);
        }
        
        // 프레임 레이트 슬라이더 범위 설정 (0 ~ 옵션 개수 - 1)
        if (m_frameRateSlider != null)
        {
            m_frameRateSlider.minValue = 0;
            m_frameRateSlider.maxValue = m_frameRateOptions.Length - 1;
            m_frameRateSlider.wholeNumbers = true;
        }
    }

    /// <summary>
    /// 저장된 설정값 불러오기 및 UI에 적용
    /// </summary>
    private void LoadAndApplySettings()
    {
        if (m_settingsData == null) return;

        // UI를 업데이트하기 직전에, 파일로부터 항상 최신 설정 데이터를 불러옵니다.
        // 이를 통해 데이터 로딩 시점의 일관성을 보장합니다.
        m_settingsData.LoadSettings();

        // UI에 설정값 적용 (R3는 Subscribe에서 값을 발행하므로 SetValueWithoutNotify가 필요합니다)
        m_effectSoundVolumeSlider.SetValueWithoutNotify(m_settingsData.EffectSoundVolume);
        m_bgmSoundVolumeSlider.SetValueWithoutNotify(m_settingsData.BackgroundSoundVolume);
        
        // 저장된 프레임 값으로 슬라이더 설정
        int savedFrameRate = m_settingsData.TargetFrameRate;
        int sliderValue = Array.IndexOf(m_frameRateOptions, savedFrameRate);
        if (sliderValue < 0)
        {
            // 저장된 값이 리스트에 없으면 기본값(120 FPS)으로 설정
            sliderValue = 2; // 120 FPS는 배열의 인덱스 2에 해당합니다.
            m_settingsData.TargetFrameRate = m_frameRateOptions[sliderValue];
        }
        m_frameRateSlider.SetValueWithoutNotify(sliderValue);
        UpdateFrameRateText(m_settingsData.TargetFrameRate);

        // SoundManager에도 즉시 적용
        SetSoundVolume(Sound.SFX, m_settingsData.EffectSoundVolume);
        SetSoundVolume(Sound.BGM, m_settingsData.BackgroundSoundVolume);
        
        SetFrameRate(m_settingsData.TargetFrameRate);
    }

    private void UpdateFrameRateText(int frameRate)
    {
        if (m_frameRateValueText != null) m_frameRateValueText.text = $"{frameRate} FPS";
    }

    #endregion

    #region UI 이벤트 바인딩 (R3)
    /// <summary>
    /// R3를 사용하여 UI 이벤트를 구독합니다.
    /// </summary>
    private void BindUIEvents()
    {
        // 효과음 슬라이더 값이 변경될 때마다 settingsData 업데이트 및 SoundManager에 적용
        m_effectSoundVolumeSlider.OnValueChangedAsObservable()
            .Subscribe(value =>
            {
                m_settingsData.EffectSoundVolume = value;
                SetSoundVolume(Sound.SFX, value);
            })
            .AddTo(m_disposables);

        // 배경음 슬라이더 값이 변경될 때마다 settingsData 업데이트 및 SoundManager에 적용
        m_bgmSoundVolumeSlider.OnValueChangedAsObservable()
            .Subscribe(value =>
            {
                m_settingsData.BackgroundSoundVolume = value;
                SetSoundVolume(Sound.BGM, value);
            })
            .AddTo(m_disposables);

        // 나가기 버튼 클릭 시 설정 저장 및 팝업 닫기
        m_exitButton.OnClickAsObservable()
            .Subscribe(_ => SaveAndExit())
            .AddTo(m_disposables);
        
        // 조이스틱 설정 버튼 클릭 시 팝업 열기
        m_joystickSizeButton.OnClickAsObservable()
            .Subscribe(_ => OpenJoystickSettings())
            .AddTo(m_disposables);

        // 프레임 슬라이더 값이 변경될 때마다 설정 업데이트 및 적용
        m_frameRateSlider.OnValueChangedAsObservable()
            .Subscribe(value =>
            {
                int selectedFrameRate = m_frameRateOptions[(int)value];
                m_settingsData.TargetFrameRate = selectedFrameRate;
                SetFrameRate(selectedFrameRate);
                UpdateFrameRateText(selectedFrameRate);
            })
            .AddTo(m_disposables);
    }

    #endregion

    #region 사운드 관리

    /// <summary>
    /// 사운드 볼륨 설정 (통합 메서드)
    /// </summary>
    /// <param name="soundType">사운드 타입</param>
    /// <param name="volume">볼륨 값</param>
    private void SetSoundVolume(Sound soundType, float volume)
    {
        if (m_soundManager != null)
        {
            m_soundManager.SetVolume(soundType, volume);
        }
        else
        {
            Debug.LogWarning("SoundManager를 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// 목표 프레임 레이트를 설정합니다.
    /// </summary>
    /// <param name="frameRate">목표 FPS</param>
    private void SetFrameRate(int frameRate)
    {
        // 엔진에 직접 프레임 적용
        Application.targetFrameRate = frameRate;
        QualitySettings.vSyncCount = 0; // VSync 비활성화하여 targetFrameRate가 적용되도록 함
    }
    #endregion

    #region UI 동작 및 팝업 관리

    /// <summary>
    /// 설정 저장 및 창 닫기
    /// </summary>
    private void SaveAndExit()
    {
        m_settingsData.SaveSettings();
        CloseOptionPopup();
    }

    /// <summary>
    /// 옵션 팝업 닫기
    /// </summary>
    private void CloseOptionPopup()
    {
        // 오브젝트 제거
        Destroy(gameObject);
    }

    /// <summary>
    /// 조이스틱 설정 팝업 열기
    /// </summary>
    private void OpenJoystickSettings()
    {
        if (m_joystickSetterPopupPrefab == null)
        {
            Debug.LogError("조이스틱 설정 프리팹이 할당되지 않았습니다!");
            return;
        }

        // 조이스틱 설정 팝업을 최상위 Canvas의 자식으로 생성하여 렌더링 문제를 방지합니다.
        var joystickSettingPopup = Instantiate(m_joystickSetterPopupPrefab.gameObject, m_canvas.transform);
        joystickSettingPopup.SetActive(true);
    }

    #endregion

    #region 에디터 전용
#if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 설정 검증 (디버그용)
    /// </summary>
    private void OnValidate()
    {
        if (m_settingsData == null)
        {
            Debug.LogError($"{gameObject.name}: SettingsData가 할당되지 않았습니다. 인스펙터에서 참조를 연결해주세요.");
        }
    }
#endif
    #endregion
}