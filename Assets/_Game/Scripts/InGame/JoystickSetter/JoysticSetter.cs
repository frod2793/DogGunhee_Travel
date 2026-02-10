using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InGame.UI.Settings
{
    /// <summary>
    /// 조이스틱의 크기, 타입, 위치 등 시각적 설정을 관리하고 저장하는 UI 클래스입니다.
    /// <br/> ScriptableObject(SettingsData)와 연동하여 데이터를 로드/저장합니다.
    /// </summary>
    public class JoystickSetter : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("데이터 참조")]
        [Tooltip("게임의 조이스틱 설정을 관리하는 ScriptableObject입니다.")]
        [SerializeField] private SettingsData m_settingsData;

        [Header("UI 컨트롤")]
        [Tooltip("조이스틱의 크기를 조절하는 슬라이더")]
        [SerializeField] private Slider m_joystickSizeSlider;

        [Tooltip("조이스틱의 타입을 선택하는 드롭다운 메뉴")]
        [SerializeField] private TMP_Dropdown m_joystickTypeDropdown;

        [Tooltip("설정을 저장하고 팝업을 닫는 버튼")]
        [SerializeField] private Button m_saveAndExitButton;

        [Tooltip("조이스틱 위치를 기본값으로 초기화하는 버튼")]
        [SerializeField] private Button m_defaultPositionButton;

        [Header("타겟 오브젝트")]
        [Tooltip("실제 화면에 표시되는 조이스틱의 RectTransform")]
        [SerializeField] private RectTransform m_joystickTransform;

        #endregion

        #region 2. 내부 변수 및 상수

        /// <summary>
        /// 조이스틱의 기본 위치 값 (초기화용)
        /// </summary>
        private static readonly Vector2 k_DefaultJoystickPosition = new Vector2(262, 0);

        // R3 구독 관리자
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        #endregion

        #region 3. 유니티 생명주기

        private void Awake()
        {
            // 필수 데이터 검증
            if (m_settingsData == null)
            {
                Debug.LogError("[JoystickSetter] SettingsData가 할당되지 않았습니다. 기능을 비활성화합니다.", this);
                enabled = false;
                return;
            }
            
            BindUIEvents();
        }

        private void OnEnable()
        {
            // 팝업이 열릴 때 최신 설정을 로드하여 UI에 반영
            LoadSettings();
        }

        private void OnDestroy()
        {
            // 메모리 누수 방지를 위해 모든 구독 해제
            m_disposables.Dispose();
        }

        #endregion

        #region 4. 초기화 및 이벤트 바인딩

        /// <summary>
        /// UI 컴포넌트의 이벤트를 R3로 구독합니다.
        /// </summary>
        private void BindUIEvents()
        {
            // 1. 슬라이더 (크기 조절)
            if (m_joystickSizeSlider != null)
            {
                m_joystickSizeSlider.OnValueChangedAsObservable()
                    .Subscribe(OnJoystickSizeChanged)
                    .AddTo(m_disposables);
            }

            // 2. 드롭다운 (타입 변경 - 현재는 저장 시 반영되지만 로그용으로 구독)
            if (m_joystickTypeDropdown != null)
            {
                m_joystickTypeDropdown.OnValueChangedAsObservable()
                    .Subscribe(OnJoystickTypeChanged)
                    .AddTo(m_disposables);
            }

            // 3. 저장 및 종료 버튼
            if (m_saveAndExitButton != null)
            {
                m_saveAndExitButton.OnClickAsObservable()
                    .Subscribe(_ => SaveAndExit())
                    .AddTo(m_disposables);
            }

            // 4. 위치 초기화 버튼
            if (m_defaultPositionButton != null)
            {
                m_defaultPositionButton.OnClickAsObservable()
                    .Subscribe(_ => ResetJoystickPosition())
                    .AddTo(m_disposables);
            }
        }

        #endregion

        #region 5. 설정 로드 및 저장 로직

        /// <summary>
        /// ScriptableObject에서 설정을 불러와 UI와 조이스틱 오브젝트에 적용합니다.
        /// </summary>
        private void LoadSettings()
        {
            if (m_settingsData == null) return;

            // SO 데이터 갱신
            m_settingsData.LoadSettings();

            // UI 반영 (이벤트 트리거 방지를 위해 SetValueWithoutNotify 사용 권장)
            if (m_joystickSizeSlider != null)
            {
                m_joystickSizeSlider.SetValueWithoutNotify(m_settingsData.JoystickSize);
            }

            if (m_joystickTypeDropdown != null)
            {
                m_joystickTypeDropdown.SetValueWithoutNotify(m_settingsData.JoystickType);
            }

            // 실제 조이스틱 오브젝트 반영
            if (m_joystickTransform != null)
            {
                m_joystickTransform.localScale = Vector3.one * m_settingsData.JoystickSize;
                m_joystickTransform.anchoredPosition = m_settingsData.JoystickPos;
            }
        }

        /// <summary>
        /// 현재 UI 상태를 ScriptableObject에 저장하고 팝업을 닫습니다.
        /// </summary>
        private void SaveAndExit()
        {
            if (m_settingsData == null) return;

            // 현재 상태 캡처
            if (m_joystickTransform != null)
            {
                m_settingsData.JoystickPos = m_joystickTransform.anchoredPosition;
            }

            if (m_joystickTypeDropdown != null)
            {
                m_settingsData.JoystickType = m_joystickTypeDropdown.value;
            }

            if (m_joystickSizeSlider != null)
            {
                m_settingsData.JoystickSize = m_joystickSizeSlider.value;
            }

            // 디스크에 저장
            m_settingsData.SaveSettings();
            
            // 팝업 종료
            Destroy(gameObject);
        }

        #endregion

        #region 6. 내부 이벤트 핸들러

        /// <summary>
        /// 슬라이더 값 변경 시 실시간으로 조이스틱 크기를 조절합니다.
        /// </summary>
        private void OnJoystickSizeChanged(float value)
        {
            if (m_joystickTransform != null)
            {
                m_joystickTransform.localScale = Vector3.one * value;
            }
        }

        /// <summary>
        /// 드롭다운 값 변경 시 호출됩니다.
        /// </summary>
        private void OnJoystickTypeChanged(int value)
        {
            // 필요 시 실시간 타입 변경 로직 추가 가능
            // Debug.Log($"조이스틱 타입 변경됨: {value}");
        }

        /// <summary>
        /// 조이스틱 위치를 초기값으로 되돌립니다.
        /// </summary>
        private void ResetJoystickPosition()
        {
            if (m_joystickTransform != null)
            {
                m_joystickTransform.anchoredPosition = k_DefaultJoystickPosition;
            }
        }

        #endregion

        #region 7. 에디터 검증 (Editor Only)
#if UNITY_EDITOR
        private void OnValidate()
        {
            // 필수 참조가 누락되었는지 확인하여 경고 메시지 출력
            if (m_settingsData == null) Debug.LogWarning("[JoystickSetter] SettingsData 미할당", this);
            if (m_joystickTransform == null) Debug.LogWarning("[JoystickSetter] Joystick Transform 미할당", this);
        }
#endif
        #endregion
    }
}