using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Title
{
    /// <summary>
    /// [설명]: 로그인 화면의 UI 바인딩 및 시각화를 담당하는 View 클래스입니다. (MVVM 패턴의 View)
    /// 서비스 의존성 없이 오직 ViewModel의 상태 변화를 UI에 반영하고 사용자 입력을 전달합니다.
    /// </summary>
    public class LoginViewMVVM : MonoBehaviour
    {
        #region 에디터 설정

        [Header("UI 패널")]
        [SerializeField, Tooltip("[설명]: 회원가입 팝업 오브젝트")]
        private GameObject m_signUpPopUp;

        [SerializeField, Tooltip("[설명]: 로그인 팝업 오브젝트")]
        private GameObject m_loginPopUp;

        [SerializeField, Tooltip("[설명]: 에러 알림 팝업 오브젝트")]
        private GameObject m_errorPopup;

        [SerializeField, Tooltip("[설명]: 에러 메시지 표시 텍스트")]
        private TMP_Text m_errorMessageText;

        [Header("회원가입 입력 필드")]
        [SerializeField, Tooltip("[설명]: 회원가입 닉네임 입력 필드")]
        private TMP_InputField m_signUpNickNameInputField;

        [SerializeField, Tooltip("[설명]: 회원가입 아이디 입력 필드")]
        private TMP_InputField m_signUpIDInputField;

        [SerializeField, Tooltip("[설명]: 회원가입 비밀번호 입력 필드")]
        private TMP_InputField m_signUpPwInputField;

        [SerializeField, Tooltip("[설명]: 회원가입 비밀번호 확인 입력 필드")]
        private TMP_InputField m_signUpPwCheckInputField;

        [SerializeField, Tooltip("[설명]: 회원가입 완료 버튼")]
        private Button m_signUpBtn;

        [Header("로그인 입력 필드")]
        [SerializeField, Tooltip("[설명]: 로그인 아이디 입력 필드")]
        private TMP_InputField m_loginIDInputField;

        [SerializeField, Tooltip("[설명]: 로그인 비밀번호 입력 필드")]
        private TMP_InputField m_loginPwInputField;

        [SerializeField, Tooltip("[설명]: 로그인 시도 버튼")]
        private Button m_loginBtn;

        [SerializeField, Tooltip("[설명]: 회원가입 창 열기 버튼")]
        private Button m_openSignUpPopUpBtn;

        [Header("메인 버튼")]
        [SerializeField, Tooltip("[설명]: 게임 시작 버튼 (자동 로그인 시도)")]
        private Button m_startBtn;

        [SerializeField, Tooltip("[설명]: 게스트 로그인 버튼")]
        private Button m_guestLoginBtn;

        [SerializeField, Tooltip("[설명]: 일반 로그인 창 열기 버튼")]
        private Button m_openLoginPopUpBtn;

        [Header("디버그/버전")]
        [SerializeField, Tooltip("[설명]: 해시 키 표시 텍스트 (안드로이드 전용)")]
        private TMP_Text m_hashKeyText;

        [SerializeField, Tooltip("[설명]: 해시 키 확인 버튼")]
        private Button m_showHashKeyButton;

        [SerializeField, Tooltip("[설명]: 현재 앱 버전 표시 텍스트")]
        private TMP_Text m_versionText;

        #endregion

        #region 내부 변수

        /// <summary>
        /// [설명]: 주입받은 뷰모델 인스턴스
        /// </summary>
        private LoginViewModel m_viewModel;

        /// <summary>
        /// [설명]: 비동기 작업 취소를 위한 토큰 소스
        /// </summary>
        private CancellationTokenSource m_cts;

        #endregion

        #region 초기화 및 바인딩 로직

        /// <summary>
        /// [설명]: VContainer를 통해 ViewModel을 주입받습니다. 주입과 동시에 바인딩을 수행합니다.
        /// </summary>
        /// <param name="viewModel">주입할 뷰모델</param>
        [Inject]
        public void Construct(LoginViewModel viewModel)
        {
            m_viewModel = viewModel;
            BindViewModel();
        }

        /// <summary>
        /// [설명]: ViewModel의 이벤트를 UI 갱신 로직에 바인딩합니다.
        /// </summary>
        private void BindViewModel()
        {
            if (m_viewModel == null) return;

            m_viewModel.OnBusyStateChanged += SetBusyState;
            m_viewModel.OnErrorMessage += message => ShowErrorPopupAsync(message).Forget();
            m_viewModel.OnLoginSuccess += OnLoginSuccess;
            m_viewModel.OnNavigationStarted += OnNavigationStarted;
        }

        /// <summary>
        /// [설명]: 구독된 ViewModel 이벤트를 해제합니다.
        /// </summary>
        private void UnbindViewModel()
        {
            if (m_viewModel == null) return;

            m_viewModel.OnBusyStateChanged -= SetBusyState;
            m_viewModel.OnErrorMessage -= message => ShowErrorPopupAsync(message).Forget();
            m_viewModel.OnLoginSuccess -= OnLoginSuccess;
            m_viewModel.OnNavigationStarted -= OnNavigationStarted;
        }

        #endregion

        #region 유니티 생명주기

        private void Awake()
        {
            m_cts = new CancellationTokenSource();
            SetupButtonListeners();
        }

        private void Start()
        {
            if (m_versionText != null)
            {
                m_versionText.text = $"Ver. {Application.version}";
            }

            SetLoginButtonsActive(false);

            if (m_startBtn != null)
            {
                m_startBtn.gameObject.SetActive(true);
            }
        }

        private void OnDestroy()
        {
            UnbindViewModel();

            if (m_cts != null)
            {
                m_cts.Cancel();
                m_cts.Dispose();
            }
        }

        #endregion

        #region UI 이벤트 핸들러

        private void SetupButtonListeners()
        {
            if (m_startBtn != null)
            {
                m_startBtn.onClick.AddListener(() => HandleTokenLogin().Forget());
            }

            if (m_guestLoginBtn != null)
            {
                m_guestLoginBtn.onClick.AddListener(() => m_viewModel?.GuestLoginAsync(m_cts.Token).Forget());
            }

            if (m_loginBtn != null)
            {
                m_loginBtn.onClick.AddListener(() =>
                {
                    if (m_viewModel != null && m_loginIDInputField != null && m_loginPwInputField != null)
                    {
                        m_viewModel.LoginAsync(m_loginIDInputField.text, m_loginPwInputField.text, m_cts.Token).Forget();
                    }
                });
            }

            if (m_signUpBtn != null)
            {
                m_signUpBtn.onClick.AddListener(() =>
                {
                    if (m_viewModel != null &&
                        m_signUpNickNameInputField != null &&
                        m_signUpIDInputField != null &&
                        m_signUpPwInputField != null &&
                        m_signUpPwCheckInputField != null)
                    {
                        m_viewModel.SignUpAsync(
                            m_signUpNickNameInputField.text,
                            m_signUpIDInputField.text,
                            m_signUpPwInputField.text,
                            m_signUpPwCheckInputField.text,
                            m_cts.Token).Forget();
                    }
                });
            }

            if (m_openLoginPopUpBtn != null)
            {
                m_openLoginPopUpBtn.onClick.AddListener(ShowLoginPopup);
            }

            if (m_openSignUpPopUpBtn != null)
            {
                m_openSignUpPopUpBtn.onClick.AddListener(ShowSignUpPopup);
            }

            if (m_showHashKeyButton != null)
            {
                m_showHashKeyButton.onClick.AddListener(OnShowHashKeyButtonPressed);
            }
        }

        private async UniTask HandleTokenLogin()
        {
            if (m_viewModel == null) return;

            bool success = await m_viewModel.TokenLoginAsync();
            if (!success)
            {
                SetLoginButtonsActive(true);
            }
        }

        #endregion

        #region 내부 로직

        /// <summary>
        /// [설명]: 작업 중 상태에 따른 UI 차단 처리를 수행합니다.
        /// </summary>
        private void SetBusyState(bool isBusy)
        {
            SetInteractable(!isBusy);
        }

        /// <summary>
        /// [설명]: 로그인 성공 시의 시각적 피드백을 처리합니다.
        /// </summary>
        private void OnLoginSuccess()
        {
            // 필요 시 성공 연출 추가 가능
        }

        /// <summary>
        /// [설명]: 네비게이션(씬 전환) 시작 시 UI 요소들을 정리합니다.
        /// </summary>
        private void OnNavigationStarted()
        {
            if (m_loginPopUp != null) m_loginPopUp.SetActive(false);
            if (m_signUpPopUp != null) m_signUpPopUp.SetActive(false);

            SetLoginButtonsActive(false);

            if (m_startBtn != null)
            {
                m_startBtn.gameObject.SetActive(false);
            }
        }

        private void SetLoginButtonsActive(bool active)
        {
            if (m_guestLoginBtn != null) m_guestLoginBtn.gameObject.SetActive(active);
            if (m_openSignUpPopUpBtn != null) m_openSignUpPopUpBtn.gameObject.SetActive(active);
            if (m_openLoginPopUpBtn != null) m_openLoginPopUpBtn.gameObject.SetActive(active);
            if (m_startBtn != null) m_startBtn.gameObject.SetActive(!active);
        }

        private void ShowSignUpPopup()
        {
            if (m_signUpPopUp != null) m_signUpPopUp.SetActive(true);
            if (m_loginPopUp != null) m_loginPopUp.SetActive(false);
        }

        private void ShowLoginPopup()
        {
            if (m_loginPopUp != null) m_loginPopUp.SetActive(true);
            if (m_signUpPopUp != null) m_signUpPopUp.SetActive(false);
        }

        private void SetInteractable(bool interactable)
        {
            if (m_startBtn != null) m_startBtn.interactable = interactable;
            if (m_loginBtn != null) m_loginBtn.interactable = interactable;
            if (m_guestLoginBtn != null) m_guestLoginBtn.interactable = interactable;
            if (m_signUpBtn != null) m_signUpBtn.interactable = interactable;
        }

        private async UniTaskVoid ShowErrorPopupAsync(string message)
        {
            if (m_errorPopup == null || m_errorMessageText == null) return;

            m_errorMessageText.text = message;
            m_errorPopup.SetActive(true);

            await UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: m_cts.Token).SuppressCancellationThrow();

            if (m_errorPopup != null)
            {
                m_errorPopup.SetActive(false);
            }
        }

        private void OnShowHashKeyButtonPressed()
        {
            if (m_hashKeyText != null)
            {
                m_hashKeyText.text = "해시 키 기능은 모바일 빌드에서만 지원됩니다.";
            }
        }

        #endregion
    }
}

