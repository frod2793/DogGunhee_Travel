using System;
using System.IO; // 파일 처리가 필요 없다면 제거 가능
using BackEnd;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DogGuns_Games
{
    public class LoginManager : MonoBehaviour
    {
        #region UI 컴포넌트 (직렬화 필드)

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
        
        // 씬이 파괴될 때 비동기 작업을 취소하기 위한 토큰 소스
        private System.Threading.CancellationTokenSource m_cts;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            // 싱글톤 캐싱
            m_serverManager = ServerManager.Instance;
            m_playerDataManager = PlayerDataManagerDontdesytoy.Instance;
            m_cts = new System.Threading.CancellationTokenSource();

            // 버튼 리스너 등록 (UniTask.Action 또는 람다 + Forget 사용)
            m_startBtn.onClick.AddListener(() => OnStartButtonPressed().Forget());
            m_guestLoginBtn.onClick.AddListener(() => OnGuestLoginButtonPressed().Forget());
            m_loginBtn.onClick.AddListener(() => OnLoginButtonPressed().Forget());
            m_signUpBtn.onClick.AddListener(() => OnSignUpButtonPressed().Forget());
            
            // 단순 UI 전환 리스너
            m_openLoginPopUpBtn.onClick.AddListener(ShowLoginPopup);
            m_openSignUpPopUpBtn.onClick.AddListener(ShowSignUpPopup);
            m_showHashKeyButton?.onClick.AddListener(OnShowHashKeyButtonPressed);
        }

        private void Start()
        {
            // BGM 재생
            SoundManager.PlaySound(Sound.BGM, SoundKeys.Intro, true);
            SoundManager.Instance.LoadSoundSetting();

            // 버전 정보 표시
            if (m_versionText != null)
            {
                m_versionText.text = $"Ver. {Application.version}";
                
            }

            // 초기 UI 상태 설정 (자동 로그인 시도 전에는 버튼 숨김)
            SetLoginButtonsActive(false);
            
            // 시작 시 자동 로그인 시도 (Start 버튼을 누를 필요 없이 바로 하려면 여기서 호출)
            // 여기서는 기존 로직대로 Start 버튼 대기 상태로 둡니다.
             m_startBtn.gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            // 씬 전환/파괴 시 진행 중인 비동기 작업 취소
            m_cts.Cancel();
            m_cts.Dispose();
        }

        #endregion

        #region 로그인 로직

        /// <summary>
        /// 시작 버튼: 토큰 로그인 시도
        /// </summary>
        private async UniTaskVoid OnStartButtonPressed()
        {
            Debug.Log("clike");
            SetInteractable(false); // 중복 클릭 방지

            // ServerManager 반환 타입 (bool success, string error) 대응
            var (success, error) = await m_serverManager.TokenLoginAsync();

            if (success)
            {
                await ProcessLoginSuccessAsync();
            }
            else
            {
                Debug.LogWarning($"토큰 로그인 실패 또는 만료: {error}");
                SetLoginButtonsActive(true); // 실패 시 수동 로그인 버튼들 표시
            }
            
            // 토큰 로그인은 실패해도 굳이 팝업을 띄우지 않고 로그인 버튼만 보여주면 됩니다.
        }

        /// <summary>
        /// 게스트 로그인
        /// </summary>
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
                // 에러 처리 로직
                if (error != null && error.Contains("bad customId"))
                {
                    Debug.LogWarning("게스트 정보 불일치 감지. 정보 초기화 후 재시도합니다.");
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

        /// <summary>
        /// 일반 로그인
        /// </summary>
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

        /// <summary>
        /// 게스트 정보 삭제 후 재시도
        /// </summary>
        private async UniTask DeleteGuestInfoAndRetryLoginAsync()
        {
            Backend.BMember.DeleteGuestInfo();
            
            // 잠시 대기 (안전성 확보)
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

        /// <summary>
        /// [공통] 로그인 성공 처리
        /// </summary>
        private async UniTask ProcessLoginSuccessAsync()
        {
            // ServerManager에 캐싱된 정보 사용
            string nickname = m_serverManager.NickName;
            string uuid = m_serverManager.Uuid;

            Debug.Log($"로그인 처리 시작: {nickname} ({uuid})");

            // 데이터 로드/생성
            await LoadOrCreatePlayerDataAsync(nickname, uuid);

            // 버튼 숨김 처리 (씬 이동 전 깜빡임 방지)
            SetLoginButtonsActive(false);
            m_startBtn.gameObject.SetActive(false);

            // 씬 이동
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

            if (string.IsNullOrEmpty(nick) || string.IsNullOrEmpty(id) || 
                string.IsNullOrEmpty(pw) || string.IsNullOrEmpty(pwCheck))
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

            // 1. 회원가입 시도
            var (signUpSuccess, signUpError) = await m_serverManager.SignUpAsync(id, pw, nick);

            if (!signUpSuccess)
            {
                ShowErrorPopupAsync($"회원가입 실패\n{signUpError}").Forget();
                SetInteractable(true);
                return;
            }

            // 2. 가입 성공 시 바로 로그인 시도
            var (loginSuccess, loginError) = await m_serverManager.LoginAsync(id, pw);

            if (loginSuccess)
            {
                m_signUpPopUp.SetActive(false);
                await ProcessLoginSuccessAsync();
            }
            else
            {
                // 가입은 됐는데 로그인이 안 된 특이 케이스
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
                Debug.Log("신규 유저 데이터 생성 중...");
                m_playerDataManager.PlayerData.InitializePlayerData(nickname, uuid);
                m_playerDataManager.SavePlayerData(); // 로컬 저장
                await m_playerDataManager.UploadDataToServerAsync(); // 서버 저장
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

        // 모든 버튼의 인터랙션 제어 (중복 클릭 방지)
        private void SetInteractable(bool interactable)
        {
            m_startBtn.interactable = interactable;
            m_guestLoginBtn.interactable = interactable;
            m_loginBtn.interactable = interactable;
            m_signUpBtn.interactable = interactable;
        }

        private async UniTaskVoid ShowErrorPopupAsync(string message)
        {
            if (m_errorPopup == null) return;

            m_errorMessageText.text = message;
            m_errorPopup.SetActive(true);

            // CancellationToken을 사용하여 씬이 바뀌거나 파괴되면 딜레이 취소
            await UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: m_cts.Token)
                         .SuppressCancellationThrow(); // 취소 시 예외 던지지 않음

            if (m_errorPopup != null)
            {
                m_errorPopup.SetActive(false);
            }
        }

        /// <summary>
        /// 구글 해시 키를 가져와 UI에 표시합니다.
        /// </summary>
        private void OnShowHashKeyButtonPressed()
        {
            if (m_hashKeyText == null) return;
            
            m_hashKeyText.gameObject.SetActive(true);

            // [중요] 에디터 환경에서는 GoogleHash를 가져올 수 없으므로 예외 처리
#if UNITY_EDITOR
            m_hashKeyText.text = "<color=yellow>에디터에서는 해시 키를 확인할 수 없습니다.\n(APK 빌드 후 모바일에서 확인하세요)</color>";
            Debug.Log("Google Hash Key는 Android 환경에서만 추출 가능합니다.");
            return;
#endif

            // Android 환경이라도 혹시 모를 에러에 대비해 try-catch 사용
            try
            {
                string googleHash = Backend.Utils.GetGoogleHash();

                if (!string.IsNullOrEmpty(googleHash))
                {
                    m_hashKeyText.text = $"<color=green>Google Hash Key: {googleHash}</color>";
                    // PC에서 로그를 보기 위해 복사하기 쉽도록 로그 출력
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