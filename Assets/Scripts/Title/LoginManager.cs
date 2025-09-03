using System;
using System.IO;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DogGuns_Games
{
    public class LoginManager : MonoBehaviour
    {
        #region 변수 및 필드

        [Header("회원 가입")] [SerializeField] private GameObject signUpPopUp;

        [SerializeField] private TMP_InputField signUpNickNameInputField;
        [SerializeField] private TMP_InputField signUpIDInputField;
        [SerializeField] private TMP_InputField signUpPwInputField;
        [SerializeField] private TMP_InputField signUpPwCheckInputField;
        [SerializeField] private Button signUpBtn;

        public string NickName
        {
            get => signUpNickNameInputField.text;
            set => signUpNickNameInputField.text = value;
        }

        [Header("로그인")] [SerializeField] private GameObject loginPopUp;
        [SerializeField] private TMP_InputField loginIDInputField;
        [SerializeField] private TMP_InputField loginPwInputField;
        [SerializeField] private Button loginBtn;
        [SerializeField] private Button openSingUpPopUpBtn;

        [Header("시작버튼")] [SerializeField] private Button startBtn;

        [Header("게스트 로그인")] [SerializeField] private Button guestLoginBtn;

        [Header("일반 로그인")] [SerializeField] private Button openLoginPopUpBtn;

        //서버 매니져 
        private ServerManager _serverManager;
        private PlayerDataManagerDontdesytoy _playerDataManagerDontdesytoy;
        private string _savePath;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            _serverManager = ServerManager.Instance;
            _playerDataManagerDontdesytoy = PlayerDataManagerDontdesytoy.Instance;
            
            // 버튼 리스너들을 Awake에서 한 번에 설정합니다.
            startBtn.onClick.AddListener(OnStartButtonPressed);
            guestLoginBtn.onClick.AddListener(OnGuestLoginButtonPressed);
            openLoginPopUpBtn.onClick.AddListener(ShowLoginPopup);
            openSingUpPopUpBtn.onClick.AddListener(ShowSignUpPopup);
            loginBtn.onClick.AddListener(OnLoginButtonPressed);
            signUpBtn.onClick.AddListener(OnSignUpButtonPressed);
        }

        void Start()
        {
            _savePath = Path.Combine(Application.persistentDataPath, "playerData.json");

            SoundManager.PlaySound(Sound.BGM, SoundKeys.Intro, true);

            SoundManager.Instance.LoadSoundSetting();
            LoginButtonGroupACtive(false);
        }

        #endregion

        #region 로그인 관련 함수

        /// <summary>
        /// 시작 버튼을 눌렀을 때 토큰으로 자동 로그인을 시도합니다.
        /// </summary>
        private async void OnStartButtonPressed()
        {
            startBtn.interactable = false; // 중복 클릭 방지
            try
            {
                // 서버 매니저의 메서드가 UniTask를 반환하도록 수정하여 async/await를 사용합니다.
                // 이는 콜백 지옥을 피하고 코드의 가독성과 유지보수성을 크게 향상시킵니다.
                var (success, nickname, uuid) = await _serverManager.TokenLoginAsync();
                if (success)
                {
                    await LoadOrCreatePlayerData(nickname, uuid);
                    SceneLoader.Instance.LoadScene("LobbyScene");
                }
                else
                {
                    // 토큰 로그인 실패 시, 다른 로그인 옵션을 보여줍니다.
                    LoginButtonGroupACtive(true);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"토큰 로그인 중 오류 발생: {e.Message}");
                LoginButtonGroupACtive(true);
            }
        }

        /// <summary>
        /// 로그인 버튼 그룹 활성화, 비활성화
        /// </summary>
        /// <param name="active"></param>
        private void LoginButtonGroupACtive(bool active)
        {
            guestLoginBtn.gameObject.SetActive(active);
            openSingUpPopUpBtn.gameObject.SetActive(active);
            openLoginPopUpBtn.gameObject.SetActive(active);
            startBtn.gameObject.SetActive(!active);
        }

        /// <summary>
        /// 게스트 로그인 버튼 함수 
        /// </summary>
        private async void OnGuestLoginButtonPressed()
        {
            guestLoginBtn.interactable = false;
            try
            {
                var (nickname, uuid) = await _serverManager.GuestLoginAsync();
                await LoadOrCreatePlayerData(nickname, uuid);
                
                LoginButtonGroupACtive(false);
                SceneLoader.Instance.LoadScene("LobbyScene");
            }
            catch (Exception e)
            {
                Debug.LogError($"게스트 로그인 실패: {e.Message}");
                guestLoginBtn.interactable = true;
            }
        }

        /// <summary>
        /// 로그인 버튼 함수
        /// </summary>
        private async void OnLoginButtonPressed()
        {
            if (string.IsNullOrEmpty(loginIDInputField.text) || string.IsNullOrEmpty(loginPwInputField.text))
            {
                Debug.Log("빈칸을 채워주세요");
                return;
            }

            loginBtn.interactable = false;
            try
            {
                var (nickname, uuid) = await _serverManager.LoginAsync(loginIDInputField.text, loginPwInputField.text);
                
                loginPopUp.SetActive(false);
                await LoadOrCreatePlayerData(nickname, uuid);
                
                SceneLoader.Instance.LoadScene("LobbyScene");
            }
            catch (Exception e)
            {
                Debug.LogError($"로그인 실패: {e.Message}");
                loginBtn.interactable = true;
            }
        }

        #endregion

        #region 회원가입 관련 함수

        private async void OnSignUpButtonPressed()
        {
            if (string.IsNullOrEmpty(signUpNickNameInputField.text) || string.IsNullOrEmpty(signUpIDInputField.text) ||
                string.IsNullOrEmpty(signUpPwInputField.text) || string.IsNullOrEmpty(signUpPwCheckInputField.text))
            {
                Debug.Log("빈칸을 채워주세요");
                return;
            }

            if (signUpPwInputField.text != signUpPwCheckInputField.text)
            {
                Debug.Log("비밀번호가 일치하지 않습니다.");
                return;
            }

            signUpBtn.interactable = false;
            try
            {
                // 1. 회원가입
                await _serverManager.SignUpAsync(signUpIDInputField.text, signUpPwInputField.text, signUpNickNameInputField.text);
                
                // 2. 가입 성공 후 바로 로그인
                var (nickname, uuid) = await _serverManager.LoginAsync(signUpIDInputField.text, signUpPwInputField.text);

                // 3. 데이터 로드 또는 생성
                await LoadOrCreatePlayerData(nickname, uuid);

                // 4. 씬 전환
                signUpPopUp.SetActive(false);
                SceneLoader.Instance.LoadScene("LobbyScene");
            }
            catch (Exception e)
            {
                Debug.LogError($"회원가입 또는 로그인 실패: {e.Message}");
                signUpBtn.interactable = true;
            }
        }

        #endregion

        #region UI 상호작용 함수

        /// <summary>
        /// 회원 가입 팝업 열기 함수 
        /// </summary>
        private void ShowSignUpPopup()
        {
            signUpPopUp.SetActive(true);
            loginPopUp.SetActive(false);
        }

        /// <summary>
        /// 로그인 팝업 열기 함수 
        /// </summary>
        private void ShowLoginPopup()
        {
            loginPopUp.SetActive(true);
            signUpPopUp.SetActive(false);
        }

        #endregion

        #region 데이터 관리 함수

        private async UniTask LoadOrCreatePlayerData(string nickname, string uuid)
        {
            // 데이터 매니저의 메서드 또한 UniTask<bool>을 반환하도록 하여 데이터 존재 여부를 비동기적으로 확인합니다.
            bool dataExists = await _playerDataManagerDontdesytoy.LoadDataFromServerAsync();
            if (!dataExists)
            {
                await CreateNewPlayerData(nickname, uuid);
            }
        }
        
        /// <summary>
        /// 새로운 플레이어 데이터 생성
        /// </summary>
        /// <param name="playerName">  </param>
        /// <param name="uid"></param>
        private async UniTask CreateNewPlayerData(string playerName, string uid)
        {
            _playerDataManagerDontdesytoy.scritpableobjPlayerData.InitializePlayerData(playerName, uid);
            _playerDataManagerDontdesytoy.SavePlayerData(); // 로컬에 저장
            await _playerDataManagerDontdesytoy.UploadDataToServerAsync(); // 서버에 업로드
            startBtn.interactable = true;
        }

        /// <summary>
        ///     플레이어 데이터 저장
        /// </summary>
        public async UniTask InsertPlayerDataAsync()
        {
            _playerDataManagerDontdesytoy.SavePlayerData();
            await _playerDataManagerDontdesytoy.UploadDataToServerAsync();
        }

        private void LoadPlayerData()
        {
            _playerDataManagerDontdesytoy.LoadPlayerData();
        }

        /// <summary>
        ///     플레이어 데이터 삭제
        /// </summary>
        private void DeletePlayerData()
        {
            if (File.Exists(_savePath))
            {
                File.Delete(_savePath);
                Debug.Log("PlayerData deleted from: " + _savePath);
            }
            else
            {
                Debug.LogWarning("No PlayerData file found at: " + _savePath);
            }
        }

        #endregion
    }
}