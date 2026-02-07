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
    /// 로그인 화면의 UI 바인딩 및 시각화를 담당하는 View 클래스입니다. (MVVM)
    /// </summary>
    public class LoginViewMVVM : MonoBehaviour
    {
        #region UI 컴포넌트

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

        #region 내부 필드
        private LoginViewModel m_viewModel;
        private System.Threading.CancellationTokenSource m_cts;
        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_cts = new System.Threading.CancellationTokenSource();
            
            // ViewModel 초기화
            m_viewModel = new LoginViewModel(ServerManager.Instance.Auth);
            
            BindViewModel();
            SetupButtonListeners();
            
            if (AppUpdateManager.Instance == null)
            {
                new GameObject(nameof(AppUpdateManager)).AddComponent<AppUpdateManager>();
            }
        }

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

        private void OnDestroy()
        {
            UnbindViewModel();
            m_cts?.Cancel();
            m_cts?.Dispose();
        }

        #endregion

        #region MVVM 바인딩
        
        private void BindViewModel()
        {
            m_viewModel.OnBusyStateChanged += SetBusyState;
            m_viewModel.OnErrorMessage += message => ShowErrorPopupAsync(message).Forget();
            m_viewModel.OnLoginSuccess += OnLoginSuccess;
        }

        private void UnbindViewModel()
        {
            if (m_viewModel != null)
            {
                m_viewModel.OnBusyStateChanged -= SetBusyState;
                m_viewModel.OnErrorMessage -= message => ShowErrorPopupAsync(message).Forget();
                m_viewModel.OnLoginSuccess -= OnLoginSuccess;
            }
        }

        private void SetBusyState(bool isBusy)
        {
            SetInteractable(!isBusy);
        }

        private void OnLoginSuccess()
        {
            ProcessLoginSuccessAsync().Forget();
        }

        #endregion

        #region UI 이벤트 핸들러

        private void SetupButtonListeners()
        {
            m_startBtn.onClick.AddListener(() => HandleTokenLogin().Forget());
            m_guestLoginBtn.onClick.AddListener(() => m_viewModel.GuestLoginAsync(m_cts.Token).Forget());
            m_loginBtn.onClick.AddListener(() => m_viewModel.LoginAsync(m_loginIDInputField.text, m_loginPwInputField.text, m_cts.Token).Forget());
            m_signUpBtn.onClick.AddListener(() => m_viewModel.SignUpAsync(
                m_signUpNickNameInputField.text, 
                m_signUpIDInputField.text, 
                m_signUpPwInputField.text, 
                m_signUpPwCheckInputField.text, 
                m_cts.Token).Forget());
            
            m_openLoginPopUpBtn.onClick.AddListener(ShowLoginPopup);
            m_openSignUpPopUpBtn.onClick.AddListener(ShowSignUpPopup);
            m_showHashKeyButton?.onClick.AddListener(OnShowHashKeyButtonPressed);
        }

        private async UniTask HandleTokenLogin()
        {
            bool success = await m_viewModel.TokenLoginAsync();
            if (!success)
            {
                SetLoginButtonsActive(true);
            }
        }

        #endregion

        #region 시각화 로직

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

        private void SetLoginButtonsActive(bool active)
        {
            m_guestLoginBtn.gameObject.SetActive(active);
            m_openSignUpPopUpBtn.gameObject.SetActive(active);
            m_openLoginPopUpBtn.gameObject.SetActive(active);
            m_startBtn.gameObject.SetActive(!active);
        }

        private void ShowSignUpPopup()
        {
            m_signUpPopUp.SetActive(true);
            m_loginPopUp.SetActive(false);
        }

        private void ShowLoginPopup()
        {
            m_loginPopUp.SetActive(true);
            m_signUpPopUp.SetActive(false);
        }

        private void SetInteractable(bool interactable)
        {
            m_startBtn.interactable = interactable;
            m_loginBtn.interactable = interactable;
            m_guestLoginBtn.interactable = interactable;
            m_signUpBtn.interactable = interactable;
        }

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

        private void OnShowHashKeyButtonPressed()
        {
            m_hashKeyText.text = "해시 키 기능은 모바일 빌드에서만 지원됩니다.";
        }

        #endregion
    }
}
