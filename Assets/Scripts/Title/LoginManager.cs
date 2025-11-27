using System;
using System.IO;
using BackEnd;
using Cysharp.Threading.Tasks;
using DogGuns_Games.Manager; // [추가] AppUpdateManager 네임스페이스
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DogGuns_Games
{
    public class LoginManager : MonoBehaviour
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

        [Header("디버그")]
        [SerializeField] private TMP_Text m_hashKeyText;
        [SerializeField] private Button m_showHashKeyButton;

        [Header("버전")]
        [SerializeField] private TMP_Text m_versionText;

        #endregion

        #region 내부 필드

        private ServerManager m_serverManager;
        private PlayerDataManagerDontdesytoy m_playerDataManager;
        private System.Threading.CancellationTokenSource m_cts;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_serverManager = ServerManager.Instance;
            m_playerDataManager = PlayerDataManagerDontdesytoy.Instance;
            m_cts = new System.Threading.CancellationTokenSource();

            // [수정] AppUpdateManager 인스턴스 동적 생성
            if (AppUpdateManager.Instance == null)
            {
                new GameObject(nameof(AppUpdateManager)).AddComponent<AppUpdateManager>();
            }

            m_startBtn.onClick.AddListener(() => OnStartButtonPressed().Forget());
            m_guestLoginBtn.onClick.AddListener(() => OnGuestLoginButtonPressed().Forget());
            m_loginBtn.onClick.AddListener(() => OnLoginButtonPressed().Forget());
            m_signUpBtn.onClick.AddListener(() => OnSignUpButtonPressed().Forget());
            
            m_openLoginPopUpBtn.onClick.AddListener(ShowLoginPopup);
            m_openSignUpPopUpBtn.onClick.AddListener(ShowSignUpPopup);
            m_showHashKeyButton?.onClick.AddListener(OnShowHashKeyButtonPressed);
        }

        private async UniTaskVoid Start()
        {
            // [수정] 앱 시작 시 업데이트 확인 먼저 수행
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
            m_cts.Cancel();
            m_cts.Dispose();
        }

        #endregion

        #region 로그인 로직

        private async UniTaskVoid OnStartButtonPressed()
        {
            SetInteractable(false);
            var (success, error) = await m_serverManager.TokenLoginAsync();
            if (success)
            {
                await ProcessLoginSuccessAsync();
            }
            else
            {
                Debug.LogWarning($"토큰 로그인 실패 또는 만료: {error}");
                SetLoginButtonsActive(true);
            }
        }

        private async UniTaskVoid OnGuestLoginButtonPressed()
        {
            SetInteractable(false);
            var (success, error) = await m_serverManager.GuestLoginAsync();
            if (success)
            {
                await ProcessLoginSuccessAsync();
            }
            else
            {
                if (error != null && error.Contains("bad customId"))
                {
                    await DeleteGuestInfoAndRetryLoginAsync();
                }
                else if (error != null && error.Contains("bad packageName"))
                {
                    ShowErrorPopupAsync("잘못된 패키지 이름입니다.\n콘솔 설정을 확인하세요.").Forget();
                    SetInteractable(true);
                }
                else
                {
                    ShowErrorPopupAsync($"게스트 로그인 실패\n{error}").Forget();
                    SetInteractable(true);
                }
            }
        }

        private async UniTaskVoid OnLoginButtonPressed()
        {
            string id = m_loginIDInputField.text;
            string pw = m_loginPwInputField.text;

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
            {
                ShowErrorPopupAsync("아이디와 비밀번호를 입력해주세요.").Forget();
                return;
            }

            SetInteractable(false);
            var (success, error) = await m_serverManager.LoginAsync(id, pw);
            if (success)
            {
                m_loginPopUp.SetActive(false);
                await ProcessLoginSuccessAsync();
            }
            else
            {
                string msg = error.Contains("StatusCode : 401") 
                    ? "아이디 또는 비밀번호가 일치하지 않습니다." 
                    : $"로그인 실패\n{error}";
                ShowErrorPopupAsync(msg).Forget();
                SetInteractable(true);
            }
        }

        private async UniTask DeleteGuestInfoAndRetryLoginAsync()
        {
            Backend.BMember.DeleteGuestInfo();
            await UniTask.Delay(500, cancellationToken: m_cts.Token);
            var (success, error) = await m_serverManager.GuestLoginAsync();
            if (success)
            {
                await ProcessLoginSuccessAsync();
            }
            else
            {
                ShowErrorPopupAsync("게스트 계정 재생성에 실패했습니다.").Forget();
                SetInteractable(true);
            }
        }

        private async UniTask ProcessLoginSuccessAsync()
        {
            string nickname = m_serverManager.NickName;
            string uuid = m_serverManager.Uuid;
            await LoadOrCreatePlayerDataAsync(nickname, uuid);
            SetLoginButtonsActive(false);
            m_startBtn.gameObject.SetActive(false);
            SceneLoader.Instance.LoadScene("LobbyScene");
        }

        #endregion

        #region 회원가입 로직

        private async UniTaskVoid OnSignUpButtonPressed()
        {
            string nick = m_signUpNickNameInputField.text;
            string id = m_signUpIDInputField.text;
            string pw = m_signUpPwInputField.text;
            string pwCheck = m_signUpPwCheckInputField.text;

            if (string.IsNullOrEmpty(nick) || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw) || string.IsNullOrEmpty(pwCheck))
            {
                ShowErrorPopupAsync("모든 항목을 입력해주세요.").Forget();
                return;
            }
            if (pw != pwCheck)
            {
                ShowErrorPopupAsync("비밀번호가 일치하지 않습니다.").Forget();
                return;
            }

            SetInteractable(false);
            var (signUpSuccess, signUpError) = await m_serverManager.SignUpAsync(id, pw, nick);
            if (!signUpSuccess)
            {
                ShowErrorPopupAsync($"회원가입 실패\n{signUpError}").Forget();
                SetInteractable(true);
                return;
            }

            var (loginSuccess, loginError) = await m_serverManager.LoginAsync(id, pw);
            if (loginSuccess)
            {
                m_signUpPopUp.SetActive(false);
                await ProcessLoginSuccessAsync();
            }
            else
            {
                ShowErrorPopupAsync("가입에는 성공했으나 로그인에 실패했습니다.\n다시 로그인해주세요.").Forget();
                m_signUpPopUp.SetActive(false);
                m_loginPopUp.SetActive(true);
                SetInteractable(true);
            }
        }

        #endregion

        #region 데이터 관리

        private async UniTask LoadOrCreatePlayerDataAsync(string nickname, string uuid)
        {
            bool dataExists = await m_playerDataManager.LoadDataFromServerAsync();
            if (!dataExists)
            {
                m_playerDataManager.PlayerData.InitializePlayerData(nickname, uuid);
                m_playerDataManager.SavePlayerData();
                await m_playerDataManager.UploadDataToServerAsync();
            }
        }

        #endregion

        #region UI 및 유틸리티

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
            if (m_hashKeyText == null) return;
            m_hashKeyText.gameObject.SetActive(true);
#if UNITY_EDITOR
            m_hashKeyText.text = "<color=yellow>에디터에서는 해시 키를 확인할 수 없습니다.\n(APK 빌드 후 모바일에서 확인하세요)</color>";
            return;
#endif
            try
            {
                string googleHash = Backend.Utils.GetGoogleHash();
                if (!string.IsNullOrEmpty(googleHash))
                {
                    m_hashKeyText.text = $"<color=green>Google Hash Key: {googleHash}</color>";
                    Debug.Log($"Google Hash Key: {googleHash}");
                }
                else
                {
                    m_hashKeyText.text = "<color=red>해시 키를 가져올 수 없습니다.</color>";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"해시 키 추출 중 에러 발생: {e.Message}");
                m_hashKeyText.text = $"<color=red>오류 발생: {e.Message}</color>";
            }
        }

        #endregion
    }
}