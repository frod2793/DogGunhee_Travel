using System;
using System.IO;
using BackEnd;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DogGuns_Games
{
    public class LoginManager : MonoBehaviour
    {
        #region 변수 및 필드

        [Header("UI 팝업")]
        [Tooltip("회원가입 UI 패널입니다.")]
        [FormerlySerializedAs("signUpPopUp")]
        [SerializeField] private GameObject m_signUpPopUp;
        [Tooltip("로그인 UI 패널입니다.")]
        [FormerlySerializedAs("loginPopUp")]
        [SerializeField] private GameObject m_loginPopUp;
        [Tooltip("오류 메시지를 표시할 팝업입니다.")]
        [SerializeField] private GameObject m_errorPopup;
        [Tooltip("오류 메시지 텍스트입니다.")]
        [SerializeField] private TMP_Text m_errorMessageText;

        [Header("회원가입 컴포넌트")]
        [Tooltip("회원가입 시 사용할 닉네임 입력 필드입니다.")]
        [FormerlySerializedAs("signUpNickNameInputField")]
        [SerializeField] private TMP_InputField m_signUpNickNameInputField;
        [Tooltip("회원가입 시 사용할 아이디 입력 필드입니다.")]
        [FormerlySerializedAs("signUpIDInputField")]
        [SerializeField] private TMP_InputField m_signUpIDInputField;
        [Tooltip("회원가입 시 사용할 비밀번호 입력 필드입니다.")]
        [FormerlySerializedAs("signUpPwInputField")]
        [SerializeField] private TMP_InputField m_signUpPwInputField;
        [Tooltip("비밀번호 확인을 위한 입력 필드입니다.")]
        [FormerlySerializedAs("signUpPwCheckInputField")]
        [SerializeField] private TMP_InputField m_signUpPwCheckInputField;
        [Tooltip("회원가입을 실행하는 버튼입니다.")]
        [FormerlySerializedAs("signUpBtn")]
        [SerializeField] private Button m_signUpBtn;

        public string NickName
        {
            get => m_signUpNickNameInputField.text;
            set => m_signUpNickNameInputField.text = value;
        }

        [Header("로그인 컴포넌트")]
        [Tooltip("로그인 시 사용할 아이디 입력 필드입니다.")]
        [FormerlySerializedAs("loginIDInputField")]
        [SerializeField] private TMP_InputField m_loginIDInputField;
        [Tooltip("로그인 시 사용할 비밀번호 입력 필드입니다.")]
        [FormerlySerializedAs("loginPwInputField")]
        [SerializeField] private TMP_InputField m_loginPwInputField;
        [Tooltip("로그인을 실행하는 버튼입니다.")]
        [FormerlySerializedAs("loginBtn")]
        [SerializeField] private Button m_loginBtn;
        [Tooltip("회원가입 팝업을 여는 버튼입니다.")]
        [FormerlySerializedAs("openSingUpPopUpBtn")]
        [SerializeField] private Button m_openSignUpPopUpBtn;

        [Header("메인 버튼")]
        [Tooltip("게임 시작 및 자동 로그인을 시도하는 버튼입니다.")]
        [FormerlySerializedAs("startBtn")]
        [SerializeField] private Button m_startBtn;
        [Tooltip("게스트 계정으로 로그인을 시도하는 버튼입니다.")]
        [FormerlySerializedAs("guestLoginBtn")]
        [SerializeField] private Button m_guestLoginBtn;
        [Tooltip("로그인 팝업을 여는 버튼입니다.")]
        [FormerlySerializedAs("openLoginPopUpBtn")]
        [SerializeField] private Button m_openLoginPopUpBtn;

        // Private 멤버 변수
        private ServerManager m_serverManager;
        private PlayerDataManagerDontdesytoy m_playerDataManager;
        private string m_savePath;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_serverManager = ServerManager.Instance;
            m_playerDataManager = PlayerDataManagerDontdesytoy.Instance;
            
            // 버튼 리스너들을 Awake에서 한 번에 설정합니다.
            m_startBtn.onClick.AddListener(OnStartButtonPressed);
            m_guestLoginBtn.onClick.AddListener(OnGuestLoginButtonPressed);
            m_openLoginPopUpBtn.onClick.AddListener(ShowLoginPopup);
            m_openSignUpPopUpBtn.onClick.AddListener(ShowSignUpPopup);
            m_loginBtn.onClick.AddListener(OnLoginButtonPressed);
            m_signUpBtn.onClick.AddListener(OnSignUpButtonPressed);
        }

        void Start()
        {
            m_savePath = Path.Combine(Application.persistentDataPath, "playerData.json");

            SoundManager.PlaySound(Sound.BGM, SoundKeys.Intro, true);

            SoundManager.Instance.LoadSoundSetting();
            SetLoginButtonsActive(false);
        }

        #endregion

        #region 로그인 관련 함수

        /// <summary>
        /// 시작 버튼을 눌렀을 때 토큰으로 자동 로그인을 시도합니다.
        /// </summary>
        private async void OnStartButtonPressed()
        {
            m_startBtn.interactable = false;
            try
            {
                var (success, nickname, uuid) = await m_serverManager.TokenLoginAsync();
                if (success)
                {
                    await ProcessLoginSuccessAsync(nickname, uuid);
                }
                else
                {
                    // 토큰 로그인 실패 시, 다른 로그인 옵션을 보여줍니다.
                    SetLoginButtonsActive(true);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"토큰 로그인 중 오류 발생: {e.Message}");
                SetLoginButtonsActive(true);
            }
            finally
            {
                // 성공 여부와 관계없이 시작 버튼은 다시 활성화 될 수 있습니다 (실패 시).
                if (!m_startBtn.gameObject.activeSelf)
                {
                    m_startBtn.interactable = true;
                }
            }
        }

        /// <summary>
        /// 게스트 로그인 버튼 함수 
        /// </summary>
        private async void OnGuestLoginButtonPressed()
        {
            m_guestLoginBtn.interactable = false;
            try
            {
                var (nickname, uuid) = await m_serverManager.GuestLoginAsync();
                await ProcessLoginSuccessAsync(nickname, uuid);
            }
            catch (Exception e)
            {
                // "bad customId" 오류는通常, 로컬에 저장된 게스트 정보가 손상되었을 때 발생합니다.
                if (e.Message.Contains("bad customId"))
                {
                    Debug.LogWarning("손상된 게스트 계정 정보 감지. 계정 정보 삭제 후 재시도합니다.");
                    // 사용자에게 잠시 후 재시도한다는 UI 피드백을 보여주는 것이 좋습니다.
                    await DeleteGuestInfoAndRetryLoginAsync();
                }
                else
                {
                    Debug.LogError($"게스트 로그인 실패: {e.Message}");
                    ShowErrorPopupAsync("게스트 로그인에 실패했습니다.\n잠시 후 다시 시도해주세요.").Forget();
                }
            }
            finally
            {
                m_guestLoginBtn.interactable = true;
            }
        }

        /// <summary>
        /// 로컬에 저장된 게스트 계정 정보를 삭제하고 다시 로그인을 시도합니다.
        /// </summary>
        private async UniTask DeleteGuestInfoAndRetryLoginAsync()
        {
            try
            {
                // 뒤끝 SDK를 사용하여 로컬 게스트 정보를 삭제합니다.
                Backend.BMember.DeleteGuestInfo();
                Debug.Log("로컬 게스트 정보 삭제 성공. 새로운 계정으로 재로그인합니다.");

                // 새로운 게스트 계정으로 다시 로그인 시도
                var (nickname, uuid) = await m_serverManager.GuestLoginAsync();
                await ProcessLoginSuccessAsync(nickname, uuid);
            }
            catch (Exception e)
            {
                Debug.LogError($"게스트 계정 재로그인 실패: {e.Message}");
                ShowErrorPopupAsync("게스트 계정 복구에 실패했습니다.\n네트워크 상태를 확인해주세요.").Forget();
            }
        }

        /// <summary>
        /// 로그인 버튼 함수
        /// </summary>
        private async void OnLoginButtonPressed()
        {
            if (string.IsNullOrEmpty(m_loginIDInputField.text) || string.IsNullOrEmpty(m_loginPwInputField.text))
            {
                ShowErrorPopupAsync("아이디와 비밀번호를 모두 입력해주세요.").Forget();
                return;
            }

            m_loginBtn.interactable = false;
            try
            {
                var (nickname, uuid) = await m_serverManager.LoginAsync(m_loginIDInputField.text, m_loginPwInputField.text);
                await ProcessLoginSuccessAsync(nickname, uuid, () => m_loginPopUp.SetActive(false));
            }
            catch (Exception e)
            {
                ShowErrorPopupAsync("아이디 또는 비밀번호가 일치하지 않습니다.").Forget();
            }
            finally
            {
                m_loginBtn.interactable = true;
            }
        }

        /// <summary>
        /// 로그인 성공 후 공통 처리 로직
        /// </summary>
        private async UniTask ProcessLoginSuccessAsync(string nickname, string uuid, Action onBeforeLoad = null)
        {
            onBeforeLoad?.Invoke();
            await LoadOrCreatePlayerDataAsync(nickname, uuid);
            SetLoginButtonsActive(false);
            SceneLoader.Instance.LoadScene("LobbyScene");
        }

        #endregion

        #region 회원가입 관련 함수

        private async void OnSignUpButtonPressed()
        {
            if (string.IsNullOrEmpty(m_signUpNickNameInputField.text) || string.IsNullOrEmpty(m_signUpIDInputField.text) ||
                string.IsNullOrEmpty(m_signUpPwInputField.text) || string.IsNullOrEmpty(m_signUpPwCheckInputField.text))
            {
                ShowErrorPopupAsync("모든 항목을 입력해주세요.").Forget();
                return;
            }

            if (m_signUpPwInputField.text != m_signUpPwCheckInputField.text)
            {
                ShowErrorPopupAsync("비밀번호가 일치하지 않습니다.").Forget();
                return;
            }

            m_signUpBtn.interactable = false;
            try
            {
                // 1. 회원가입
                await m_serverManager.SignUpAsync(m_signUpIDInputField.text, m_signUpPwInputField.text, m_signUpNickNameInputField.text);
                
                // 2. 가입 성공 후 바로 로그인
                var (nickname, uuid) = await m_serverManager.LoginAsync(m_signUpIDInputField.text, m_signUpPwInputField.text);

                // 3. 데이터 로드 또는 생성 및 씬 전환
                await ProcessLoginSuccessAsync(nickname, uuid, () => m_signUpPopUp.SetActive(false));
            }
            catch (Exception e)
            {
                ShowErrorPopupAsync("회원가입에 실패했습니다.\n" + e.Message).Forget();
            }
            finally
            {
                m_signUpBtn.interactable = true;
            }
        }

        #endregion

        #region UI 상호작용 함수

        /// <summary>
        /// 로그인 버튼 그룹 활성화/비활성화
        /// </summary>
        private void SetLoginButtonsActive(bool active)
        {
            m_guestLoginBtn.gameObject.SetActive(active);
            m_openSignUpPopUpBtn.gameObject.SetActive(active);
            m_openLoginPopUpBtn.gameObject.SetActive(active);
            m_startBtn.gameObject.SetActive(!active);
        }
        
        /// <summary>
        /// 회원 가입 팝업 열기 함수 
        /// </summary>
        private void ShowSignUpPopup()
        {
            m_signUpPopUp.SetActive(true);
            m_loginPopUp.SetActive(false);
        }

        /// <summary>
        /// 로그인 팝업 열기 함수 
        /// </summary>
        private void ShowLoginPopup()
        {
            m_loginPopUp.SetActive(true);
            m_signUpPopUp.SetActive(false);
        }

        /// <summary>
        /// 지정된 메시지와 함께 오류 팝업을 표시합니다.
        /// </summary>
        private async UniTask ShowErrorPopupAsync(string message)
        {
            if (m_errorPopup == null || m_errorMessageText == null)
            {
                Debug.LogError("오류 팝업 또는 텍스트가 할당되지 않았습니다.");
                return;
            }

            m_errorMessageText.text = message;
            m_errorPopup.SetActive(true);

            // 3초 후에 팝업을 자동으로 닫습니다.
            await UniTask.Delay(TimeSpan.FromSeconds(3), ignoreTimeScale: true);

            if (m_errorPopup != null) // 딜레이 후 객체가 파괴되었을 수 있으므로 확인
            {
                m_errorPopup.SetActive(false);
            }
        }
        #endregion

        #region 데이터 관리 함수

        private async UniTask LoadOrCreatePlayerDataAsync(string nickname, string uuid)
        {
            bool dataExists = await m_playerDataManager.LoadDataFromServerAsync();
            if (!dataExists)
            {
                await CreateNewPlayerDataAsync(nickname, uuid);
            }
        }
        
        /// <summary>
        /// 새로운 플레이어 데이터 생성
        /// </summary>
        private async UniTask CreateNewPlayerDataAsync(string playerName, string uid)
        {
            m_playerDataManager.PlayerData.InitializePlayerData(playerName, uid);
            m_playerDataManager.SavePlayerData(); // 로컬에 저장
            await m_playerDataManager.UploadDataToServerAsync(); // 서버에 업로드
        }

        /// <summary>
        ///     플레이어 데이터 저장
        /// </summary>
        public async UniTask InsertPlayerDataAsync()
        {
            m_playerDataManager.SavePlayerData();
            await m_playerDataManager.UploadDataToServerAsync();
        }

        private void LoadPlayerData()
        {
            m_playerDataManager.LoadPlayerData();
        }

        /// <summary>
        ///     플레이어 데이터 삭제 (주로 디버깅용)
        /// </summary>
        private void DeletePlayerData()
        {
            if (File.Exists(m_savePath))
            {
                File.Delete(m_savePath);
                Debug.Log("PlayerData deleted from: " + m_savePath);
            }
            else
            {
                Debug.LogWarning("No PlayerData file found at: " + m_savePath);
            }
        }

        #endregion
    }
}
