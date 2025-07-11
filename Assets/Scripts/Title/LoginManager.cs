using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DogGuns_Games
{
    public class LoginManager : MonoBehaviour
    {
        #region 변수 및 필드

        [Header("회원 가입")] 
        [SerializeField] private GameObject signUpPopUp;

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
            signUpBtn.onClick.AddListener(Func_SignUpBtn);
            loginBtn.onClick.AddListener(Func_LoginBtn);
            _serverManager = ServerManager.Instance;
            _playerDataManagerDontdesytoy = PlayerDataManagerDontdesytoy.Instance;
            
        }

        void Start()
        {
            
            _savePath = Path.Combine(Application.persistentDataPath, "playerData.json");

            startBtn.onClick.AddListener(Func_StartBtn);
            openSingUpPopUpBtn.onClick.AddListener(Func_OpenSingUpPopUp_Btn);
            openLoginPopUpBtn.onClick.AddListener(Func_OpenLoginPopUp_Btn);
            guestLoginBtn.onClick.AddListener(() => { Func_GuestLoginBtn(); });

            SoundManager.PlaySound (Sound.BGM, SoundKeys.Intro, true);
            LoginButtonGroupACtive(false);
        }

        #endregion

        #region 로그인 관련 함수

        /// <summary>
        ///   토큰 로그인
        /// </summary>
        private void TokenLogin()
        {
            _serverManager.TokenLogin(
                onSuccess: () => {     
                    FindPlayerdata(() => { CreateNewPlayerData(_serverManager.nickName, _serverManager.uuid); });
                    SceneLoader.Instace.LoadScene("LobbyScene"); 
                },
                onFailure: () => LoginButtonGroupACtive(true)
            );
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
        }

        /// <summary>
        /// 게스트 로그인 버튼 함수 
        /// </summary>
        private void Func_GuestLoginBtn()
        {
            _serverManager.GuestLogin(() =>
            {
                startBtn.interactable = true;
                startBtn.gameObject.SetActive(true);
                FindPlayerdata(() => { CreateNewPlayerData(_serverManager.nickName, _serverManager.uuid); });
                LoginButtonGroupACtive(false);
                SceneLoader.Instace.LoadScene("LobbyScene");
            });
        }

        /// <summary>
        /// 로그인 프로세스 코루틴
        /// </summary>
        /// <returns></returns>
        IEnumerator CO_Login_Process()
        {
            if (loginIDInputField.text != "" && loginPwInputField.text != "")
            {
                //로그인
                //서버에 로그인 요청
                //성공시
                _serverManager.Login(loginIDInputField.text, loginPwInputField.text, () =>
                {
                    loginPopUp.SetActive(false);
                    FindPlayerdata(() => { CreateNewPlayerData(_serverManager.nickName, _serverManager.uuid); });
                    startBtn.interactable = true;
                    startBtn.gameObject.SetActive(true);
                    SceneLoader.Instace.LoadScene("LobbyScene");
                });
                yield return null;
            }
            else
            {
                Debug.Log("빈칸을 채워주세요");
            }
        }

        /// <summary>
        /// 로그인 버튼 함수
        /// </summary>
        private void Func_LoginBtn()
        {
            StartCoroutine(CO_Login_Process());
        }

        /// <summary>
        /// 시작 버튼 함수
        /// </summary>
        private void Func_StartBtn()
        {
            TokenLogin();
        }

        #endregion

        #region 회원가입 관련 함수

        /// <summary>
        /// 회원 가입 버튼 함수
        /// </summary>
        private void Func_SignUpBtn()
        {
            if (signUpNickNameInputField.text != "" && signUpIDInputField.text != "" &&
                signUpPwInputField.text != "" && signUpPwCheckInputField.text != "")
            {
                if (signUpPwInputField.text == signUpPwCheckInputField.text)
                {
                    //회원가입
                    //서버에 회원가입 요청
                    //성공시
                    _serverManager.SignUp(signUpIDInputField.text, signUpPwInputField.text,
                        signUpNickNameInputField.text, () =>
                        {
                            signUpPopUp.SetActive(false);
                            loginPopUp.SetActive(true);
                            CreateNewPlayerData(signUpNickNameInputField.text, ""); // UID는 로그인 후 채워짐
                            // 회원가입 후 바로 로그인 처리
                            _serverManager.Login(signUpIDInputField.text, signUpPwInputField.text, () =>
                            {
                                FindPlayerdata(() => { CreateNewPlayerData(_serverManager.nickName, _serverManager.uuid); });
                                SceneLoader.Instace.LoadScene("LobbyScene");
                            });
                        });
                }
                else
                {
                    Debug.Log("비밀번호가 일치하지 않습니다.");
                }
            }
            else
            {
                Debug.Log("빈칸을 채워주세요");
            }
        }

        #endregion

        #region UI 상호작용 함수

        /// <summary>
        /// 회원 가입 팝업 열기 함수 
        /// </summary>
        private void Func_OpenSingUpPopUp_Btn()
        {
            signUpPopUp.SetActive(true);
            loginPopUp.SetActive(false);
        }

        /// <summary>
        /// 로그인 팝업 열기 함수 
        /// </summary>
        private void Func_OpenLoginPopUp_Btn()
        {
            loginPopUp.SetActive(true);
            signUpPopUp.SetActive(false);
        }

        #endregion

        #region 데이터 관리 함수

        /// <summary>
        /// 새로운 플레이어 데이터 생성
        /// </summary>
        /// <param name="playerName">  </param>
        /// <param name="uid"></param>
        private void CreateNewPlayerData(string playerName, string uid)
        {
            _playerDataManagerDontdesytoy.scritpableobjPlayerData.InitializePlayerData(playerName, uid);
            _playerDataManagerDontdesytoy.SavePlayerData(); // 로컬에 저장
            _playerDataManagerDontdesytoy.UploadDataToServer(); // 서버에 업로드
            startBtn.interactable = true;
        }

        /// <summary>
        ///     플레이어 데이터 저장
        /// </summary>
        public void InsertPlayerData()
        {
            _playerDataManagerDontdesytoy.SavePlayerData();
            _playerDataManagerDontdesytoy.UploadDataToServer();
        }

        private void LoadPlayerData()
        {
            _playerDataManagerDontdesytoy.LoadPlayerData();
        }

        /// <summary>
        /// 플레이어 데이터 찾기
        /// </summary>
        /// <param name="action">게임 데이터가 존재하지않을떄 실행할 액션</param>
        private void FindPlayerdata(Action action)
        {
            _playerDataManagerDontdesytoy.LoadDataFromServer(action);
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