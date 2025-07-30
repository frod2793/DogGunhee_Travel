using System;
using System.Collections.Generic;
using DogGuns_Games.vamsir;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionPopupManager : MonoBehaviour
{
    [Header("설정 데이터")] public SettingsData_oBJ settingsData; // ScriptableObject 참조

    [Header("사운드 조절")] [SerializeField] private Slider effectSoundVolum;
    [SerializeField] private Slider bgMsoundVolum;

    [Header("<color=green> 나가기 버튼")] [SerializeField]
    private Button exitBtn;

    [Header("조이스틱 사이즈및 타입 조절 버튼")] [SerializeField]
    private Button joystickSizeBtn;

    [SerializeField] private JoysticSetter joysticSetterPopUpPrefb;

    private bool _isInitialized = false;
    private SoundManager _soundManager => SoundManager.Instance;

    private void Start()
    {
        // SoundManager.instance를 사용하므로 별도 초기화 불필요
    }

    private void OnEnable()
    {
        if (_soundManager == null)
        {
            Debug.LogError("SoundManager를 찾을 수 없습니다. OptionPopupManager가 정상적으로 작동하지 않을 수 있습니다.");
        }

        if (!_isInitialized)
        {
            InitializeComponents();
            _isInitialized = true;
        }

        LoadAndApplySettings();
    }

    private void OnDisable()
    {
        // OnDisable에서 리스너 해제 (더 안전함)
        RemoveAllListeners();
    }

    private void OnDestroy()
    {
        RemoveAllListeners();
    }

    /// <summary>
    /// 컴포넌트 초기화 및 이벤트 리스너 등록
    /// </summary>
    private void InitializeComponents()
    {
        // 설정 데이터 검증
        if (settingsData == null)
        {
            Debug.LogError("SettingsData가 할당되지 않았습니다!");
            return;
        }

        settingsData.LoadSettings();

        // 이벤트 리스너 등록
        RegisterEventListeners();

        // 드롭다운 초기화 (현재는 사용되지 않지만 확장성을 위해 유지)
        InitializeDropdown();
    }

    /// <summary>
    /// 이벤트 리스너 등록
    /// </summary>
    private void RegisterEventListeners()
    {
        effectSoundVolum?.onValueChanged.AddListener(OnEffectVolumeChanged);
        bgMsoundVolum?.onValueChanged.AddListener(OnBgmVolumeChanged);
        exitBtn?.onClick.AddListener(SaveAndExit);
        joystickSizeBtn?.onClick.AddListener(OpenJoystickSettings);
    }

    /// <summary>
    /// 모든 이벤트 리스너 해제
    /// </summary>
    private void RemoveAllListeners()
    {
        effectSoundVolum?.onValueChanged.RemoveAllListeners();
        bgMsoundVolum?.onValueChanged.RemoveAllListeners();
        exitBtn?.onClick.RemoveAllListeners();
        joystickSizeBtn?.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// 효과음 볼륨 변경 처리
    /// </summary>
    /// <param name="value">볼륨 값</param>
    private void OnEffectVolumeChanged(float value)
    {
        SetSoundVolume(Sound.SFX, value);
    }

    /// <summary>
    /// 배경음 볼륨 변경 처리
    /// </summary>
    /// <param name="value">볼륨 값</param>
    private void OnBgmVolumeChanged(float value)
    {
        SetSoundVolume(Sound.BGM, value);
    }

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

    /// <summary>
    /// 설정 저장 및 창 닫기
    /// </summary>
    private void SaveAndExit()
    {
        SaveCurrentSettings();
        CloseOptionPopup();
    }

    /// <summary>
    /// 현재 설정값 저장
    /// </summary>
    private void SaveCurrentSettings()
    {
        if (settingsData == null) return;

        // 현재 UI 값을 설정 데이터에 저장
        settingsData.effectSoundVolume = effectSoundVolum.value;
        settingsData.backgroundSoundVolume = bgMsoundVolum.value;

        // 설정 저장
        settingsData.SaveSettings();
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
    /// 저장된 설정값 불러오기 및 UI에 적용
    /// </summary>
    private void LoadAndApplySettings()
    {
        if (settingsData == null) return;

        // UI에 설정값 적용 (이벤트 트리거 방지를 위해 일시적으로 리스너 해제)
        effectSoundVolum.SetValueWithoutNotify(settingsData.effectSoundVolume);
        bgMsoundVolum.SetValueWithoutNotify(settingsData.backgroundSoundVolume);

        // SoundManager에도 즉시 적용
        SetSoundVolume(Sound.SFX, settingsData.effectSoundVolume);
        SetSoundVolume(Sound.BGM, settingsData.backgroundSoundVolume);
    }

    /// <summary>
    /// 드롭다운 초기화 (현재 미사용이지만 확장성을 위해 유지)
    /// </summary>
    private void InitializeDropdown()
    {
        // 향후 조이스틱 타입 선택 기능 확장을 위한 메서드
        var joystickTypes = new List<string> { "Fixed", "Floating", "Dynamic" };
        // 실제 드롭다운 컴포넌트가 추가되면 여기서 설정
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
}