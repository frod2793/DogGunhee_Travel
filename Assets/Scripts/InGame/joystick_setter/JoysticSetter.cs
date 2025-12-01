using R3;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 조이스틱의 크기, 타입, 위치 등 시각적 설정을 관리하고 저장하는 클래스입니다.
/// </summary>
public class JoystickSetter : MonoBehaviour
{
    #region 상수

    /// <summary>
    /// 조이스틱의 기본 위치 값입니다.
    /// </summary>
    private static readonly Vector2 k_DefaultJoystickPosition = new Vector2(262, 0);

    #endregion

    #region 필드 및 프로퍼티

    [Header("참조 데이터")]
    [Tooltip("게임의 조이스틱 설정을 관리하는 ScriptableObject입니다.")]
    [SerializeField] private SettingsData m_settingsData;

    [Header("UI 컴포넌트")]
    [Tooltip("조이스틱의 크기를 조절하는 슬라이더입니다.")]
    [SerializeField] private Slider m_joystickSizeSlider;

    [Tooltip("조이스틱의 타입을 선택하는 드롭다운 메뉴입니다.")]
    [SerializeField] private TMP_Dropdown m_joystickTypeDropdown;

    [Tooltip("실제 화면에 표시되는 조이스틱의 Transform입니다.")]
    [SerializeField] private RectTransform m_joystickTransform;

    [Tooltip("설정을 저장하고 팝업을 닫는 버튼입니다.")]
    [SerializeField] private Button m_saveAndExitButton;

    [Tooltip("조이스틱 위치를 기본값으로 초기화하는 버튼입니다.")]
    [SerializeField] private Button m_defaultPositionButton;

    private readonly CompositeDisposable m_disposables = new();

    #endregion

    #region Unity 라이프사이클

    private void Awake()
    {
        if (m_settingsData == null)
        {
            Debug.LogError("SettingsData가 할당되지 않았습니다. 인스펙터에서 참조를 연결해주세요.", this);
            // 설정 데이터가 없으면 아무것도 할 수 없으므로 비활성화합니다.
            enabled = false; 
            return;
        }
        
        BindUIEvents();
    }

    private void OnEnable()
    {
        LoadSettings();
    }

    private void OnDestroy()
    {
        m_disposables.Dispose();
    }

    #endregion

    #region 초기화 및 UI 이벤트 바인딩

    /// <summary>
    /// UI 컨트롤의 이벤트를 구독하고 핸들러를 연결합니다.
    /// </summary>
    private void BindUIEvents()
    {
        // 슬라이더 값 변경 시 크기 즉시 적용
        m_joystickSizeSlider.OnValueChangedAsObservable()
            .Subscribe(OnJoystickSizeChanged)
            .AddTo(m_disposables);

        // 드롭다운 값 변경 시 타입 즉시 적용 (현재는 저장 시에만 반영)
        m_joystickTypeDropdown.OnValueChangedAsObservable()
            .Subscribe(OnJoystickTypeChanged)
            .AddTo(m_disposables);

        // 저장 및 나가기 버튼
        m_saveAndExitButton.OnClickAsObservable()
            .Subscribe(_ => SaveAndExit())
            .AddTo(m_disposables);

        // 기본 위치 버튼
        m_defaultPositionButton.OnClickAsObservable()
            .Subscribe(_ => ResetJoystickPosition())
            .AddTo(m_disposables);
    }

    #endregion

    #region 설정 로드 및 적용

    /// <summary>
    /// ScriptableObject에서 설정을 불러와 UI와 조이스틱에 적용합니다.
    /// </summary>
    private void LoadSettings()
    {
        // 설정창을 열 때, 파일에 저장된 최신 데이터를 명시적으로 불러옵니다.
        m_settingsData.LoadSettings();

        // UI 컨트롤에 값 적용 (이벤트 발생 방지)
        m_joystickSizeSlider.SetValueWithoutNotify(m_settingsData.JoystickSize);
        m_joystickTypeDropdown.SetValueWithoutNotify(m_settingsData.JoystickType);

        // 조이스틱 Transform에 값 적용
        m_joystickTransform.localScale = Vector3.one * m_settingsData.JoystickSize;
        m_joystickTransform.anchoredPosition = m_settingsData.JoystickPos;
    }

    #endregion

    #region UI 이벤트 핸들러

    /// <summary>
    /// 조이스틱 크기 슬라이더 값이 변경될 때 호출됩니다.
    /// </summary>
    private void OnJoystickSizeChanged(float value)
    {
        m_joystickTransform.localScale = Vector3.one * value;
    }

    /// <summary>
    /// 조이스틱 타입 드롭다운 값이 변경될 때 호출됩니다.
    /// </summary>
    private void OnJoystickTypeChanged(int value)
    {
        // 이 값은 저장 시에만 반영되므로 즉시 처리할 로직은 현재 없습니다.
        // 필요 시 여기에 로직을 추가할 수 있습니다.
    }

    /// <summary>
    /// 조이스틱의 위치를 미리 정의된 기본값으로 초기화합니다.
    /// </summary>
    private void ResetJoystickPosition()
    {
        m_joystickTransform.anchoredPosition = k_DefaultJoystickPosition;
    }

    #endregion

    #region 저장 및 종료

    /// <summary>
    /// 현재 UI에 설정된 값들을 ScriptableObject에 저장하고 팝업을 닫습니다.
    /// </summary>
    private void SaveAndExit()
    {
        // 저장하기 직전에 현재 UI의 값을 settingsData에 반영합니다.
        m_settingsData.JoystickPos = m_joystickTransform.anchoredPosition;
        m_settingsData.JoystickType = m_joystickTypeDropdown.value;
        m_settingsData.JoystickSize = m_joystickSizeSlider.value;

        m_settingsData.SaveSettings();
        
        // 이 컴포넌트가 포함된 게임 오브젝트를 파괴하여 팝업을 닫습니다.
        Destroy(gameObject);
    }

    #endregion

    #region 에디터 전용
#if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 값이 변경될 때 호출되어 참조 누락을 검사합니다.
    /// </summary>
    private void OnValidate()
    {
        if (m_settingsData == null) Debug.LogWarning("SettingsData가 할당되지 않았습니다.", this);
        if (m_joystickSizeSlider == null) Debug.LogWarning("Joystick Size Slider가 할당되지 않았습니다.", this);
        if (m_joystickTypeDropdown == null) Debug.LogWarning("Joystick Type Dropdown이 할당되지 않았습니다.", this);
        if (m_joystickTransform == null) Debug.LogWarning("Joystick Transform이 할당되지 않았습니다.", this);
        if (m_saveAndExitButton == null) Debug.LogWarning("Save And Exit Button이 할당되지 않았습니다.", this);
        if (m_defaultPositionButton == null) Debug.LogWarning("Default Position Button이 할당되지 않았습니다.", this);
    }
#endif
    #endregion
}
