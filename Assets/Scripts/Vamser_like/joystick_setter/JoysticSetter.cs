using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JoysticSetter : MonoBehaviour
{
    
    [Header("<color=green>조이스틱 데이터")] [SerializeField]
    private SettingsData_oBJ settingsData; // ScriptableObject 참조

    [Header("<color=green>조이스틱 설정UI")] [SerializeField]
    private Slider joystickSizeSlider;

    [SerializeField] private TMP_Dropdown joystickTypeDropdown;
    [SerializeField] private Transform joystickTransform;
    [SerializeField] JoyStickPosDragandDrop joyStickPosDragandDrop;
    [SerializeField] private Button saveandExitBtn;
    [SerializeField] private Button defaultposBtn;
    
    [SerializeField] private Vector3 defaultJoystickPos = new Vector3(363, -173, 0);
    
    private void OnEnable()
    {
        joystickSizeSlider.onValueChanged.AddListener(delegate { JoystickSizeSliderChanged(); });
        joystickTypeDropdown.onValueChanged.AddListener(delegate { JoystickTypeDropdownChanged(); });
        saveandExitBtn.onClick.AddListener(SaveAndExit);
        defaultposBtn.onClick.AddListener(ResetJoystickPosition); // 미구현 기능 구현
        LoadSettings();
    }

    // defaultposBtn 클릭 시 조이스틱 위치를 초기화하는 기능 구현
    private void ResetJoystickPosition()
    {
        var rectTransform = joystickTransform as RectTransform;
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = defaultJoystickPos;
            settingsData.joystickPos = defaultJoystickPos;
        }
    }

    private void JoystickSizeSliderChanged()
    {
        settingsData.joystickSize = joystickSizeSlider.value;
        joystickTransform.localScale = new Vector3(settingsData.joystickSize, settingsData.joystickSize, 1);
    }

    private void JoystickTypeDropdownChanged()
    {
        settingsData.joystickType = joystickTypeDropdown.value;
    }

    private void LoadSettings()
    {
        // 설정창을 열 때, 파일에 저장된 최신 데이터를 명시적으로 불러옵니다.
        settingsData.LoadSettings();

        joystickSizeSlider.value = settingsData.joystickSize;
        joystickTypeDropdown.value = settingsData.joystickType;
        joystickTransform.localScale = new Vector3(settingsData.joystickSize, settingsData.joystickSize, 1);

        var rectTransform = joystickTransform as RectTransform;
        if (rectTransform != null)
            rectTransform.anchoredPosition = settingsData.joystickPos;
    }

    private void SetJoystickPos()
    {
        var rectTransform = joystickTransform as RectTransform;
        if (rectTransform != null)
            settingsData.joystickPos = rectTransform.anchoredPosition;
    }


    private void SaveAndExit()
    {
        // UI 위치는 anchoredPosition으로 저장해야 정확합니다.
        var rectTransform = joystickTransform as RectTransform;
        if (rectTransform != null)
            settingsData.joystickPos = rectTransform.anchoredPosition;

        settingsData.joystickType = joystickTypeDropdown.value;
        settingsData.joystickSize = joystickSizeSlider.value;
        settingsData.SaveSettings();
        // 리스너 해제 및 최적화
        joystickSizeSlider.onValueChanged.RemoveAllListeners();
        joystickTypeDropdown.onValueChanged.RemoveAllListeners();
        saveandExitBtn.onClick.RemoveAllListeners();
        defaultposBtn.onClick.RemoveAllListeners();
        Destroy(gameObject);
    }
}