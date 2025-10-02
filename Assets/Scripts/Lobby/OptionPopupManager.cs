using System;
using System.Collections.Generic;
using DogGuns_Games.vamsir;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionPopupManager : MonoBehaviour
{
    #region 필드 및 프로퍼티

    [Header("참조 데이터 및 프리팹")]
    [Tooltip("게임의 전반적인 설정을 관리하는 ScriptableObject입니다.")]
    [SerializeField] private SettingsData_oBJ settingsData;
    [Tooltip("동적으로 생성할 조이스틱 설정 팝업 프리팹입니다.")]
    [SerializeField] private JoysticSetter joysticSetterPopUpPrefb;

    [Header("UI 컴포넌트")]
    [Tooltip("효과음 볼륨을 조절하는 슬라이더입니다.")]
    [SerializeField] private Slider effectSoundVolum;
    [Tooltip("배경음 볼륨을 조절하는 슬라이더입니다.")]
    [SerializeField] private Slider bgMsoundVolum;
    [Tooltip("설정 창을 닫고 변경사항을 저장하는 버튼입니다.")]
    [SerializeField] private Button exitBtn;
    [Tooltip("조이스틱 설정 팝업을 여는 버튼입니다.")]
    [SerializeField] private Button joystickSizeBtn;
    
    // --- 내부 상태 변수 ---
    /// <summary>
    /// R3 구독을 관리하여 메모리 누수를 방지합니다.
    /// </summary>
    private readonly CompositeDisposable _disposables = new();
    /// <summary>
    /// 사운드 재생 및 볼륨 조절을 위한 SoundManager 인스턴스입니다.
    /// </summary>
    private SoundManager _soundManager;
    /// <summary>
    /// 이 팝업이 속한 최상위 Canvas입니다.
    /// </summary>
    private Canvas _canvas;

    #endregion

    #region Unity 라이프사이클

    private void Awake()
    {
        // Awake에서 모든 초기화를 한 번만 수행합니다.
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
        _disposables.Dispose();
    }

    #endregion

    #region 초기화 및 설정

    /// <summary>
    /// 컴포넌트 초기화 및 이벤트 리스너 등록
    /// </summary>
    private void InitializeComponents()
    {
        _soundManager = SoundManager.Instance;
        if (_soundManager == null)
        {
            Debug.LogError("SoundManager를 찾을 수 없습니다. OptionPopupManager가 정상적으로 작동하지 않을 수 있습니다.");
        }

        // 설정 데이터 검증
        if (settingsData == null)
        {
            Debug.LogError("SettingsData가 할당되지 않았습니다!");
            return;
        }

        // 팝업이 속한 Canvas를 찾아서 참조합니다.
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null)
        {
            Debug.LogError("상위 오브젝트에서 Canvas를 찾을 수 없습니다!", this);
            return; // Canvas가 없으면 더 이상 진행하지 않습니다.
        }
        
        // Canvas의 렌더 모드를 ScreenSpaceCamera로 설정하고, 렌더 카메라를 Main Camera로 지정합니다.
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.worldCamera = Camera.main;

        // Camera.main이 null일 경우 (씬에 MainCamera 태그가 없는 경우) 경고를 출력합니다.
        if (_canvas.worldCamera == null)
        {
            Debug.LogWarning("메인 카메라(Tag: MainCamera)를 찾을 수 없습니다. Canvas의 렌더 카메라가 설정되지 않았습니다.", this);
        }
    }

    /// <summary>
    /// 저장된 설정값 불러오기 및 UI에 적용
    /// </summary>
    private void LoadAndApplySettings()
    {
        if (settingsData == null) return;

        // UI를 업데이트하기 직전에, 파일로부터 항상 최신 설정 데이터를 불러옵니다.
        // 이를 통해 데이터 로딩 시점의 일관성을 보장합니다.
        settingsData.LoadSettings();

        // UI에 설정값 적용 (R3는 Subscribe에서 값을 발행하므로 SetValueWithoutNotify가 필요합니다)
        effectSoundVolum.SetValueWithoutNotify(settingsData.effectSoundVolume);
        bgMsoundVolum.SetValueWithoutNotify(settingsData.backgroundSoundVolume);

        // SoundManager에도 즉시 적용
        SetSoundVolume(Sound.SFX, settingsData.effectSoundVolume);
        SetSoundVolume(Sound.BGM, settingsData.backgroundSoundVolume);
    }

    #endregion

    #region UI 이벤트 바인딩 (R3)
    /// <summary>
    /// R3를 사용하여 UI 이벤트를 구독합니다.
    /// </summary>
    private void BindUIEvents()
    {
        // 효과음 슬라이더 값이 변경될 때마다 settingsData 업데이트 및 SoundManager에 적용
        effectSoundVolum.OnValueChangedAsObservable()
            .Subscribe(value =>
            {
                settingsData.effectSoundVolume = value;
                // 슬라이더를 빠르게 조작할 때 과도한 호출을 방지 (0.1초 간격)
                Observable.Return(value)
                    .ThrottleFirst(TimeSpan.FromSeconds(0.1))
                    .Subscribe(v => SetSoundVolume(Sound.SFX, v))
                    .AddTo(_disposables);
            })
            .AddTo(_disposables);

        // 배경음 슬라이더 값이 변경될 때마다 settingsData 업데이트 및 SoundManager에 적용
        bgMsoundVolum.OnValueChangedAsObservable()
            .Subscribe(value =>
            {
                settingsData.backgroundSoundVolume = value;
                Observable.Return(value)
                    .ThrottleFirst(TimeSpan.FromSeconds(0.1))
                    .Subscribe(v => SetSoundVolume(Sound.BGM, v))
                    .AddTo(_disposables);
            })
            .AddTo(_disposables);

        // 나가기 버튼 클릭 시 설정 저장 및 팝업 닫기
        exitBtn.OnClickAsObservable().Subscribe(_ => SaveAndExit()).AddTo(_disposables);

        // 조이스틱 설정 버튼 클릭 시 팝업 열기
        joystickSizeBtn.OnClickAsObservable().Subscribe(_ => OpenJoystickSettings()).AddTo(_disposables);
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
        if (_soundManager != null)
        {
            _soundManager.VolumSet(soundType, volume);
        }
        else
        {
            Debug.LogWarning("SoundManager를 찾을 수 없습니다.");
        }
    }

    #endregion

    #region UI 동작 및 팝업 관리

    /// <summary>
    /// 설정 저장 및 창 닫기
    /// </summary>
    private void SaveAndExit()
    {
        settingsData.SaveSettings();
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
        if (joysticSetterPopUpPrefb == null)
        {
            Debug.LogError("조이스틱 설정 프리팹이 할당되지 않았습니다!");
            return;
        }

        // 조이스틱 설정 팝업 생성
        var joystickSettingPopup = Instantiate(joysticSetterPopUpPrefb.gameObject, transform);
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
        if (settingsData == null)
        {
            Debug.LogWarning($"{gameObject.name}: SettingsData가 할당되지 않았습니다.");
        }
    }
#endif
    #endregion
}