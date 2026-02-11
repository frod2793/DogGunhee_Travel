using System;
using Cysharp.Threading.Tasks;
using InGame;
using InGame.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BackEnd;

namespace Title
{
    /// <summary>
    /// 로그인 화면의 UI 바인딩 및 시각화를 담당하는 View 클래스입니다. (MVVM 패턴의 View)
    /// </summary>
    public class LoginViewMVVM : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("UI 패널")] 
        [SerializeField] private GameObject m_signUpPopUp;
        [SerializeField] private GameObject m_loginPopUp;
        [SerializeField] private GameObject m_errorPopup;
        [SerializeField] private TMP_Text m_errorMessageText;

        [Header("회원가입 입력 필드")] 
        [SerializeField] private TMP_InputField m_signUpNickNameInputField;
        [SerializeField] private TMP_InputField m_signUpIDInputField;
        [SerializeField] private TMP_InputField m_signUpPwInputField;
        [SerializeField] private TMP_InputField m_signUpPwCheckInputField;
        [SerializeField] private Button m_signUpBtn;

        [Header("로그인 입력 필드")] 
        [SerializeField] private TMP_InputField m_loginIDInputField;
        [SerializeField] private TMP_InputField m_loginPwInputField;
        [SerializeField] private Button m_loginBtn;
        [SerializeField] private Button m_openSignUpPopUpBtn;

        [Header("메인 버튼")] 
        [SerializeField] private Button m_startBtn;
        [SerializeField] private Button m_guestLoginBtn;
        [SerializeField] private Button m_openLoginPopUpBtn;

        [Header("디버그/버전")] 
        [SerializeField] private TMP_Text m_hashKeyText;
        [SerializeField] private Button m_showHashKeyButton;
        [SerializeField] private TMP_Text m_versionText;

        #endregion

        #region 2. 내부 필드 및 시스템

        private LoginViewModel m_viewModel;
        private System.Threading.CancellationTokenSource m_cts;

        #endregion

        #region 3. 유니티 생명주기

        /// <summary>
        /// ViewModel 초기화 및 기본 UI 설정을 수행합니다.
        /// </summary>
        private void Awake()
        {
            m_cts = new System.Threading.CancellationTokenSource();

            // ViewModel 초기화 (의존성 주입)
            m_viewModel = new LoginViewModel(ServerManager.Instance.Auth);

            BindViewModel();
            SetupButtonListeners();

            if (AppUpdateManager.Instance == null)
            {
                new GameObject(nameof(AppUpdateManager)).AddComponent<AppUpdateManager>();
            }
        }

        /// <summary>
        /// 업데이트 확인 및 오디오 설정을 로드합니다.
        /// </summary>
        private async UniTaskVoid Start()
        {
            await AppUpdateManager.Instance.CheckForUpdateAsync();

            SoundManager.PlaySound(Sound.BGM, SoundKeys.Intro, true);
            SoundManager.Instance.LoadSoundSetting();

            if (m_versionText != null)
            {
                m_versionText.text = $"Ver. {Application.version}";
            }

            SetLoginButtonsActive(false);
            m_startBtn.gameObject.SetActive(true);
        }

        /// <summary>
        /// 바인딩 해제 및 비동기 작업을 취소합니다.
        /// </summary>
        private void OnDestroy()
        {
            UnbindViewModel();
            m_cts?.Cancel();
            m_cts?.Dispose();
        }

        #endregion

        #region 4. MVVM 바인딩 로직

        /// <summary>
        /// ViewModel의 이벤트를 View의 로직에 연결합니다.
        /// </summary>
        private void BindViewModel()
        {
            m_viewModel.OnBusyStateChanged += SetBusyState;
            m_viewModel.OnErrorMessage += message => ShowErrorPopupAsync(message).Forget();
            m_viewModel.OnLoginSuccess += OnLoginSuccess;
        }

        /// <summary>
        /// 구독했던 이벤트를 모두 해제합니다.
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
        /// 작업 중 상태에 따라 UI의 상호작용 가능 여부를 제어합니다.
        /// </summary>
        /// <param name="isBusy">작업 중 여부</param>
        private void SetBusyState(bool isBusy)
        {
            SetInteractable(!isBusy);
        }

        /// <summary>
        /// 로그인 성공 시 후처리 로직을 실행합니다.
        /// </summary>
        private void OnLoginSuccess()
        {
            ProcessLoginSuccessAsync().Forget();
        }

        #endregion

        #region 5. UI 이벤트 핸들러

        /// <summary>
        /// 버튼 클릭 리스너를 등록합니다.
        /// </summary>
        private void SetupButtonListeners()
        {
            m_startBtn.onClick.AddListener(() => HandleTokenLogin().Forget());
            m_guestLoginBtn.onClick.AddListener(() => m_viewModel.GuestLoginAsync(m_cts.Token).Forget());
            m_loginBtn.onClick.AddListener(() =>
                m_viewModel.LoginAsync(m_loginIDInputField.text, m_loginPwInputField.text, m_cts.Token).Forget());
            m_signUpBtn.onClick.AddListener(() => m_viewModel.SignUpAsync(
                m_signUpNickNameInputField.text,
                m_signUpIDInputField.text,
                m_signUpPwInputField.text,
                m_signUpPwCheckInputField.text,
                m_cts.Token).Forget());

            m_openLoginPopUpBtn.onClick.AddListener(ShowLoginPopup);
            m_openSignUpPopUpBtn.onClick.AddListener(ShowSignUpPopup);
            if (m_showHashKeyButton != null)
            {
                m_showHashKeyButton.onClick.AddListener(OnShowHashKeyButtonPressed);
            }
        }

        /// <summary>
        /// 저장된 토큰을 이용한 자동 로그인을 시도합니다.
        /// </summary>
        private async UniTask HandleTokenLogin()
        {
            bool success = await m_viewModel.TokenLoginAsync();
            if (!success)
            {
                SetLoginButtonsActive(true);
            }
        }

        #endregion

        #region 6. 내부 시각화 및 보조 로직

        /// <summary>
        /// 로그인 성공 후 데이터 로드 및 씬 전환을 처리합니다.
        /// </summary>
        private async UniTask ProcessLoginSuccessAsync()
        {
            bool dataExists = await PlayerDataManager.Instance.LoadDataFromServerAsync();
            if (!dataExists)
            {
                PlayerDataManager.Instance.PlayerData.InitializePlayerData(
                    ServerManager.Instance.NickName,
                    ServerManager.Instance.Uuid);
                PlayerDataManager.Instance.SavePlayerData();
                await PlayerDataManager.Instance.UploadDataToServerAsync();
            }

            m_loginPopUp.SetActive(false);
            m_signUpPopUp.SetActive(false);
            SetLoginButtonsActive(false);
            m_startBtn.gameObject.SetActive(false);

            SceneLoader.Instance.LoadScene("LobbyScene");
        }

        /// <summary>
        /// 로그인 관련 버튼들의 활성화 상태를 한꺼번에 설정합니다.
        /// </summary>
        private void SetLoginButtonsActive(bool active)
        {
            m_guestLoginBtn.gameObject.SetActive(active);
            m_openSignUpPopUpBtn.gameObject.SetActive(active);
            m_openLoginPopUpBtn.gameObject.SetActive(active);
            m_startBtn.gameObject.SetActive(!active);
        }

        /// <summary>
        /// 회원가입 팝업을 표시합니다.
        /// </summary>
        private void ShowSignUpPopup()
        {
            m_signUpPopUp.SetActive(true);
            m_loginPopUp.SetActive(false);
        }

        /// <summary>
        /// 로그인 팝업을 표시합니다.
        /// </summary>
        private void ShowLoginPopup()
        {
            m_loginPopUp.SetActive(true);
            m_signUpPopUp.SetActive(false);
        }

        /// <summary>
        /// 주요 버튼들의 상호작용 가능 상태를 설정합니다.
        /// </summary>
        private void SetInteractable(bool interactable)
        {
            m_startBtn.interactable = interactable;
            m_loginBtn.interactable = interactable;
            m_guestLoginBtn.interactable = interactable;
            m_signUpBtn.interactable = interactable;
        }

        /// <summary>
        /// 에러 팝업을 메시지와 함께 표시합니다.
        /// </summary>
        private async UniTaskVoid ShowErrorPopupAsync(string message)
        {
            if (m_errorPopup == null) return;
            m_errorMessageText.text = message;
            m_errorPopup.SetActive(true);
            await UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: m_cts.Token).SuppressCancellationThrow();
            if (m_errorPopup != null)
            {
                m_errorPopup.SetActive(false);
            }
        }

        /// <summary>
        /// 해시 키 확인 버튼 클릭 시 안내 메시지를 출력합니다.
        /// </summary>
        private void OnShowHashKeyButtonPressed()
        {
            m_hashKeyText.text = "해시 키 기능은 모바일 빌드에서만 지원됩니다.";
        }

        #endregion
    }
}
