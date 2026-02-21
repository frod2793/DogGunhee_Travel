using InGame.Core.Interfaces;
using System;
using InGame.UI.Settings;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace Lobby
{
    /// <summary>
    /// [설명]: 게임 설정 옵션 팝업의 시각적 요소와 사용자 입력을 담당하는 View 클래스입니다.
    /// MVVM 패턴을 사용하여 실제 비즈니스 로직(OptionPopupViewModel)과 데이터를 동기화합니다.
    /// </summary>
    public class OptionPopupView : MonoBehaviour
    {
        #region 에디터 설정

        [Header("<color=green>참조 데이터 및 프리팹</color>")]
        [SerializeField, Tooltip("게임 설정 저장 및 관리 ScriptableObject"), FormerlySerializedAs("settingsData")]
        private SettingsData m_settingsData;

        [SerializeField, Tooltip("조이스틱 설정 레이어 프리팹"), FormerlySerializedAs("joysticSetterPopUpPrefb")]
        private JoystickSetter m_joystickSetterPopupPrefab;

        [Header("<color=green>UI 컴포넌트</color>")]
        [SerializeField, Tooltip("효과음(SFX) 볼륨 조절 슬라이더"), FormerlySerializedAs("effectSoundVolum")]
        private Slider m_effectSoundVolumeSlider;

        [SerializeField, Tooltip("배경음(BGM) 볼륨 조절 슬라이더"), FormerlySerializedAs("bgMsoundVolum")]
        private Slider m_bgmSoundVolumeSlider;

        [SerializeField, Tooltip("설정창 닫기 버튼 (저장 포함)"), FormerlySerializedAs("exitBtn")]
        private Button m_exitButton;

        [SerializeField, Tooltip("조이스틱 크기 설정 열기 버튼"), FormerlySerializedAs("joystickSizeBtn")]
        private Button m_joystickSizeButton;

        [SerializeField, Tooltip("목표 프레임 레이트 설정 슬라이더")]
        private Slider m_frameRateSlider;

        [SerializeField, Tooltip("현재 FPS 설정값 표시 텍스트")]
        private TMP_Text m_frameRateValueText;

        #endregion

        #region 내부 변수 및 상태

        private OptionPopupViewModel m_viewModel;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();
        private Canvas m_canvas;
        private InGame.UI.IPopupService m_popupService;

        #endregion

        #region 유니티 생명주기

        private void Awake()
        {
            InitializeComponents();
        }

        private void OnDestroy()
        {
            m_disposables.Dispose();
            m_viewModel?.Dispose();
        }

        #endregion

        #region 초기화 및 바인딩 로직

        /// <summary>
        /// [설명]: 옵션 조절을 위한 뷰모델을 생성하고 사운드 매니저와 연동합니다.
        /// </summary>
        /// <param name="soundManager">의존성 주입된 사운드 매니저</param>
        public void Initialize(InGame.Services.ISoundManager soundManager, InGame.UI.IPopupService popupService = null)
        {
            if (soundManager == null)
            {
                LogManager.LogError("[OptionPopupView] SoundManager가 주입되지 않았습니다.");
                return;
            }

            m_popupService = popupService;

            // SoundManager 주입을 통해 ViewModel 초기화
            m_viewModel = new OptionPopupViewModel(m_settingsData, soundManager);
            
            // ViewModel 바인딩
            BindViewModel();
        }

        /// <summary>
        /// [설명]: 필요한 컴포넌트를 캐싱하고 UI 컨트롤의 기본값을 설정합니다.
        /// </summary>
        private void InitializeComponents()
        {
            m_canvas = GetComponentInParent<Canvas>();

            // 프레임 레이트 슬라이더 범위 동적 설정
            if (m_frameRateSlider != null && m_viewModel != null)
            {
                m_frameRateSlider.minValue = 0;
                m_frameRateSlider.maxValue = m_viewModel.FrameRateOptions.Length - 1;
                m_frameRateSlider.wholeNumbers = true;
            }
        }

        /// <summary>
        /// [설명]: ViewModel과 UI 요소 간의 양방향 데이터 동기화를 수행합니다.
        /// </summary>
        private void BindViewModel()
        {
            if (m_viewModel == null)
            {
                return;
            }

            #region ViewModel -> View (상태 동기화)

            // 효과음 볼륨 동기화
            m_viewModel.EffectSoundVolume
                .Subscribe(v => m_effectSoundVolumeSlider?.SetValueWithoutNotify(v))
                .AddTo(m_disposables);

            // 배경음 볼륨 동기화
            m_viewModel.BgmSoundVolume
                .Subscribe(v => m_bgmSoundVolumeSlider?.SetValueWithoutNotify(v))
                .AddTo(m_disposables);

            // 목표 프레임 레이트 동기화
            m_viewModel.TargetFrameRate
                .Subscribe(v =>
                {
                    if (m_frameRateValueText != null)
                    {
                        m_frameRateValueText.text = $"{v} FPS";
                    }

                    m_frameRateSlider?.SetValueWithoutNotify(m_viewModel.GetFrameRateIndex());
                })
                .AddTo(m_disposables);

            #endregion

            #region View -> ViewModel (이벤트 핸들링)

            // 사용자의 슬라이더 입력을 뷰모델에 전달
            m_effectSoundVolumeSlider?.OnValueChangedAsObservable()
                .Subscribe(v => m_viewModel.EffectSoundVolume.Value = v)
                .AddTo(m_disposables);

            m_bgmSoundVolumeSlider?.OnValueChangedAsObservable()
                .Subscribe(v => m_viewModel.BgmSoundVolume.Value = v)
                .AddTo(m_disposables);

            m_frameRateSlider?.OnValueChangedAsObservable()
                .Subscribe(v =>
                {
                    int index = (int)v;
                    if (index >= 0 && index < m_viewModel.FrameRateOptions.Length)
                    {
                        m_viewModel.TargetFrameRate.Value = m_viewModel.FrameRateOptions[index];
                    }
                })
                .AddTo(m_disposables);

            #endregion

            #region 버튼 명령 바인딩

            // 종료 시 저장 로직 실행
            m_exitButton?.OnClickAsObservable()
                .Subscribe(_ =>
                {
                    m_viewModel.SaveSettings();

                    // [수정]: IPopupService가 주입되지 않은 경우(에디터 폴백 등) 직접 삭제 처리하여 NRE 방지
                    if (m_popupService != null)
                    {
                        m_popupService.CloseTopPopup();
                    }
                    else
                    {
                        Destroy(gameObject);
                    }
                })
                .AddTo(m_disposables);

            // 조이스틱 설정 창 팝업
            m_joystickSizeButton?.OnClickAsObservable()
                .Subscribe(_ => OpenJoystickSettings())
                .AddTo(m_disposables);

            #endregion
        }

        #endregion

        #region UI 기능 메서드

        /// <summary>
        /// [설명]: 인게임에서 사용되는 조이스틱 크기 및 감도 설정 창을 화면에 생성합니다.
        /// </summary>
        private void OpenJoystickSettings()
        {
            if (m_joystickSetterPopupPrefab == null || m_canvas == null)
            {
                LogManager.LogWarning("뷰모델이 유효하지 않습니다.", LogManager.LogCategory.System);
                return;
            }

            var joystickSettingPopup = Instantiate(m_joystickSetterPopupPrefab.gameObject, m_canvas.transform);
            joystickSettingPopup.SetActive(true);
        }

        #endregion
    }
}
