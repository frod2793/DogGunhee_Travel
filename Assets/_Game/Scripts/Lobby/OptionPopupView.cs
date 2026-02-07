using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace Lobby
{
    /// <summary>
    /// 옵션 팝업의 UI 바인딩 및 시각화를 담당하는 View 클래스입니다.
    /// MVVM 패턴을 따르며, 실제 로직은 OptionPopupViewModel에 위임합니다.
    /// </summary>
    public class OptionPopupView : MonoBehaviour
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

        // --- 내부 상태 ---
        private OptionPopupViewModel m_viewModel;
        private readonly CompositeDisposable m_disposables = new();
        private Canvas m_canvas;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            InitializeViewModel();
            InitializeComponents();
            Bind();
        }

        private void OnDestroy()
        {
            // 모든 구독 정리
            m_disposables.Dispose();
            // ViewModel 해제
            m_viewModel?.Dispose();
        }

        #endregion

        #region 초기화 및 바인딩

        /// <summary>
        /// ViewModel을 초기화하고 의존성을 주입합니다.
        /// </summary>
        private void InitializeViewModel()
        {
            // 의존성 주입 (SoundManager는 싱글톤으로 제공됨)
            m_viewModel = new OptionPopupViewModel(m_settingsData, SoundManager.Instance);
        }

        /// <summary>
        /// 필요한 컴포넌트 참조 및 기본 설정을 수행합니다.
        /// </summary>
        private void InitializeComponents()
        {
            m_canvas = GetComponentInParent<Canvas>();

            // 프레임 레이트 슬라이더 범위 설정
            if (m_frameRateSlider != null && m_viewModel != null)
            {
                m_frameRateSlider.minValue = 0;
                m_frameRateSlider.maxValue = m_viewModel.FrameRateOptions.Length - 1;
                m_frameRateSlider.wholeNumbers = true;
            }
        }

        /// <summary>
        /// ViewModel과 View 사이의 데이터 바인딩을 수행합니다. (MVVM 패턴)
        /// </summary>
        private void Bind()
        {
            if (m_viewModel == null) return;

            #region ViewModel -> View (상태 동기화)

            // 볼륨 상태 동기화
            m_viewModel.EffectSoundVolume
                .Subscribe(v => m_effectSoundVolumeSlider.SetValueWithoutNotify(v))
                .AddTo(m_disposables);

            m_viewModel.BgmSoundVolume
                .Subscribe(v => m_bgmSoundVolumeSlider.SetValueWithoutNotify(v))
                .AddTo(m_disposables);

            // 프레임 레이트 상태 동기화
            m_viewModel.TargetFrameRate
                .Subscribe(v =>
                {
                    if (m_frameRateValueText != null) m_frameRateValueText.text = $"{v} FPS";
                    m_frameRateSlider.SetValueWithoutNotify(m_viewModel.GetFrameRateIndex());
                })
                .AddTo(m_disposables);

            #endregion

            #region View -> ViewModel (이벤트 전달)

            // UI 변경 이벤트를 ViewModel에 전달
            m_effectSoundVolumeSlider.OnValueChangedAsObservable()
                .Subscribe(v => m_viewModel.EffectSoundVolume.Value = v)
                .AddTo(m_disposables);

            m_bgmSoundVolumeSlider.OnValueChangedAsObservable()
                .Subscribe(v => m_viewModel.BgmSoundVolume.Value = v)
                .AddTo(m_disposables);

            m_frameRateSlider.OnValueChangedAsObservable()
                .Subscribe(v =>
                {
                    int index = (int)v;
                    m_viewModel.TargetFrameRate.Value = m_viewModel.FrameRateOptions[index];
                })
                .AddTo(m_disposables);

            #endregion

            #region 명령 바인딩 (Buttons)

            // 설정 저장 및 팝업 닫기
            m_exitButton.OnClickAsObservable()
                .Subscribe(_ =>
                {
                    m_viewModel.SaveSettings();
                    Destroy(gameObject);
                })
                .AddTo(m_disposables);

            // 조이스틱 설정 열기
            m_joystickSizeButton.OnClickAsObservable()
                .Subscribe(_ => OpenJoystickSettings())
                .AddTo(m_disposables);

            #endregion
        }

        #endregion

        #region UI 동작

        /// <summary>
        /// 조이스틱 설정 레이어를 생성하여 표시합니다.
        /// </summary>
        private void OpenJoystickSettings()
        {
            if (m_joystickSetterPopupPrefab == null || m_canvas == null)
            {
                Debug.LogWarning("참조가 누락되어 조이스틱 설정을 열 수 없습니다.");
                return;
            }

            var joystickSettingPopup = Instantiate(m_joystickSetterPopupPrefab.gameObject, m_canvas.transform);
            joystickSettingPopup.SetActive(true);
        }

        #endregion
    }
}
