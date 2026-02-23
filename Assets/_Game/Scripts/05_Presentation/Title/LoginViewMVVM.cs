using System;
using Cysharp.Threading.Tasks;
using InGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BackEnd;
using InGame.Data;
using InGame.Managers;

namespace Title
{
    /// <summary>
    /// [설명]: 로그인 화면의 UI 바인딩 및 시각화를 담당하는 View 클래스입니다. (MVVM 패턴의 View)
    /// 사용자의 입력을 ViewModel에 전달하고, ViewModel의 상태 변화를 UI에 반영합니다.
    /// </summary>
    public class LoginViewMVVM : MonoBehaviour
    {
        #region 에디터 설정

        [Header("서비스 및 데이터")]
        [SerializeField, Tooltip("서버 통신 매니저 (DI)")]
        private ServerManager m_serverManager;

        [SerializeField, Tooltip("사운드 매니저 (DI)")]
        private SoundManager m_soundManager;

        [SerializeField, Tooltip("씬 로더 (DI)")]
        private SceneLoader m_sceneLoader;

        [SerializeField, Tooltip("앱 업데이트 매니저 (DI)")]
        private AppUpdateManager m_appUpdateManager;

        [SerializeField] private PlayerDataDTO m_playerData;

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

        /// <summary> [설명]: 로그인 비즈니스 로직을 처리하는 뷰모델 </summary>
        private LoginViewModel m_viewModel;

        /// <summary> [설명]: 비동기 작업 취소를 위한 토큰 소스 </summary>
        private System.Threading.CancellationTokenSource m_cts;

        #endregion

        #region 유니티 생명주기

        /// <summary>
        /// [설명]: ViewModel 초기화 및 기본 UI 설정을 수행합니다.
        /// </summary>
        private void Awake()
        {
            m_cts = new System.Threading.CancellationTokenSource();

            // ViewModel 초기화 (의존성 주입)
            if (m_serverManager != null && m_serverManager.Auth != null)
            {
                m_viewModel = new LoginViewModel(m_serverManager.Auth);
            }

            BindViewModel();
            SetupButtonListeners();

            if (m_appUpdateManager == null)
            {
                m_appUpdateManager = FindFirstObjectByType<AppUpdateManager>();
                if (m_appUpdateManager == null)
                {
                    m_appUpdateManager = new GameObject(nameof(AppUpdateManager)).AddComponent<AppUpdateManager>();
                }
            }
        }

        /// <summary>
        /// [설명]: 업데이트 확인 및 오리는 오디오 설정을 로드합니다.
        /// </summary>
        private async UniTaskVoid Start()
        {
            if (m_appUpdateManager != null)
            {
                await m_appUpdateManager.CheckForUpdateAsync();
            }

            if (m_soundManager != null)
            {
                // 저장된 볼륨 설정을 먼저 로드한 후 BGM 재생
                m_soundManager.LoadSoundSetting();
                m_soundManager.Play(SoundKeys.Intro.ToString(), Sound.BGM, 1.0f, true);
            }

            // 리모트 데이터(구글 시트) 업데이트 체크 로직은 인게임 진입(GameManager) 쪽으로 이전되었습니다.

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

        /// <summary>
        /// [설명]: 바인딩 해제 및 비동기 작업을 취소합니다.
        /// </summary>
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

        #region MVVM 바인딩 로직

        /// <summary>
        /// [설명]: ViewModel의 이벤트를 View의 로직에 연결합니다.
        /// </summary>
        private void BindViewModel()
        {
            if (m_viewModel == null)
            {
                return;
            }

            m_viewModel.OnBusyStateChanged += SetBusyState;
            m_viewModel.OnErrorMessage += message => ShowErrorPopupAsync(message).Forget();
            m_viewModel.OnLoginSuccess += OnLoginSuccess;
        }

        /// <summary>
        /// [설명]: 구독했던 이벤트를 모두 해제합니다.
        /// </summary>
        private void UnbindViewModel()
        {
            if (m_viewModel != null)
            {
                m_viewModel.OnBusyStateChanged -= SetBusyState;
                m_viewModel.OnErrorMessage -= message => ShowErrorPopupAsync(message).Forget();
                m_viewModel.OnLoginSuccess -= OnLoginSuccess;
            }
        }

        /// <summary>
        /// [설명]: 작업 중 상태에 따라 UI의 상호작용 가능 여부를 제어합니다.
        /// </summary>
        /// <param name="isBusy">작업 중 여부</param>
        private void SetBusyState(bool isBusy)
        {
            SetInteractable(!isBusy);
        }

        /// <summary>
        /// [설명]: 로그인 성공 시 후처리 로직을 실행합니다.
        /// </summary>
        private void OnLoginSuccess()
        {
            ProcessLoginSuccessAsync().Forget();
        }

        #endregion

        #region UI 이벤트 핸들러

        /// <summary>
        /// [설명]: 버튼 클릭 리스너를 등록합니다.
        /// </summary>
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

        /// <summary>
        /// [설명]: 저장된 토큰을 이용한 자동 로그인을 시도합니다.
        /// </summary>
        private async UniTask HandleTokenLogin()
        {
            if (m_viewModel == null)
            {
                return;
            }

            bool success = await m_viewModel.TokenLoginAsync();
            if (!success)
            {
                SetLoginButtonsActive(true);
            }
        }

        #endregion

        #region 비즈니스 로직 및 보조 메서드

        /// <summary>
        /// [설명]: 로그인 성공 후 데이터 로드 및 씬 전환을 처리합니다.
        /// </summary>
        private async UniTask ProcessLoginSuccessAsync()
        {
            // DTO 직접 생성 또는 서비스 활용
            var playerDto = new PlayerDataDTO();
            
            ServerSessionDTO sessionDto = null;
            if (m_serverManager != null)
            {
                playerDto.Initialize(m_serverManager.NickName, m_serverManager.Uuid);
                sessionDto = m_serverManager.GetSession();
            }

            var payload = new ScenePayloadDTO(playerDto, sessionDto, m_soundManager)
            {
                IsFirstLogin = true
            };

            if (m_loginPopUp != null)
            {
                m_loginPopUp.SetActive(false);
            }

            if (m_signUpPopUp != null)
            {
                m_signUpPopUp.SetActive(false);
            }

            SetLoginButtonsActive(false);

            if (m_startBtn != null)
            {
                m_startBtn.gameObject.SetActive(false);
            }

            if (m_sceneLoader != null)
            {
                // DTO를 페이로드로 전달하며 비동기 씬 로드
                await m_sceneLoader.LoadSceneAsync(SceneNames.Lobby, payload);
            }
        }

        /// <summary>
        /// [설명]: 로그인 관련 버튼들의 활성화 상태를 한꺼번에 설정합니다.
        /// </summary>
        private void SetLoginButtonsActive(bool active)
        {
            if (m_guestLoginBtn != null) m_guestLoginBtn.gameObject.SetActive(active);
            if (m_openSignUpPopUpBtn != null) m_openSignUpPopUpBtn.gameObject.SetActive(active);
            if (m_openLoginPopUpBtn != null) m_openLoginPopUpBtn.gameObject.SetActive(active);
            if (m_startBtn != null) m_startBtn.gameObject.SetActive(!active);
        }

        /// <summary>
        /// [설명]: 회원가입 팝업을 표시합니다.
        /// </summary>
        private void ShowSignUpPopup()
        {
            if (m_signUpPopUp != null) m_signUpPopUp.SetActive(true);
            if (m_loginPopUp != null) m_loginPopUp.SetActive(false);
        }

        /// <summary>
        /// [설명]: 로그인 팝업을 표시합니다.
        /// </summary>
        private void ShowLoginPopup()
        {
            if (m_loginPopUp != null) m_loginPopUp.SetActive(true);
            if (m_signUpPopUp != null) m_signUpPopUp.SetActive(false);
        }

        /// <summary>
        /// [설명]: 주요 버튼들의 상호작용 가능 상태를 설정합니다.
        /// </summary>
        private void SetInteractable(bool interactable)
        {
            if (m_startBtn != null) m_startBtn.interactable = interactable;
            if (m_loginBtn != null) m_loginBtn.interactable = interactable;
            if (m_guestLoginBtn != null) m_guestLoginBtn.interactable = interactable;
            if (m_signUpBtn != null) m_signUpBtn.interactable = interactable;
        }

        /// <summary>
        /// [설명]: 에러 팝업을 메시지와 함께 표시합니다.
        /// </summary>
        private async UniTaskVoid ShowErrorPopupAsync(string message)
        {
            if (m_errorPopup == null || m_errorMessageText == null)
            {
                return;
            }

            m_errorMessageText.text = message;
            m_errorPopup.SetActive(true);

            await UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: m_cts.Token).SuppressCancellationThrow();

            if (m_errorPopup != null)
            {
                m_errorPopup.SetActive(false);
            }
        }

        /// <summary>
        /// [설명]: 해시 키 확인 버튼 클릭 시 안내 메시지를 출력합니다.
        /// </summary>
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
