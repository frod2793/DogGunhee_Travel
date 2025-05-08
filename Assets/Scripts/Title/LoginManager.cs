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
        }

        void Start()
        {
            _serverManager = FindAnyObjectByType<ServerManager>();
            _savePath = Path.Combine(Application.persistentDataPath, "playerData.json");
            _playerDataManagerDontdesytoy = FindAnyObjectByType<PlayerDataManagerDontdesytoy>();

            startBtn.onClick.AddListener(Func_StartBtn);
            openSingUpPopUpBtn.onClick.AddListener(Func_OpenSingUpPopUp_Btn);
            openLoginPopUpBtn.onClick.AddListener(Func_OpenLoginPopUp_Btn);
            guestLoginBtn.onClick.AddListener(() => { Func_GuestLoginBtn(); });

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
                onSuccess: () => { SceneLoader.Instace.LoadScene("LobbyScene"); },
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
                SceneLoader.Instace.LoadScene("LobbyScene");
                LoginButtonGroupACtive(false);
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
                });
                yield return new WaitForSeconds(1f);
                startBtn.interactable = true;
                startBtn.gameObject.SetActive(true);
                SceneLoader.Instace.LoadScene("LobbyScene");
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
                            CreateNewPlayerData(_serverManager.nickName, _serverManager.uuid);
                            SceneLoader.Instace.LoadScene("LobbyScene");
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
            startBtn.interactable = true;
            InsertPlayerData(); // 새로 생성한 데이터를 저장
        }

        /// <summary>
        ///     플레이어 데이터 저장
        /// </summary>
        public void InsertPlayerData()
        {
            // ScriptableObject를 JSON으로 직렬화
            string jsonData = JsonUtility.ToJson(_playerDataManagerDontdesytoy.scritpableobjPlayerData, true);

            // 파일에 저장
            File.WriteAllText(_savePath, jsonData);
            Debug.Log("PlayerData saved to: " + _savePath);

            //저장된 파일을 클라우드에 저장
            Debug.Log(_playerDataManagerDontdesytoy.scritpableobjPlayerData.nickname);
            _serverManager.GameDataInsert(_playerDataManagerDontdesytoy.scritpableobjPlayerData);
        }

        private void LoadPlayerData()
        {
            if (File.Exists(_savePath))
            {
                // JSON 파일을 읽어와 ScriptableObject에 덮어씌움
                string jsonData = File.ReadAllText(_savePath);
                if (_playerDataManagerDontdesytoy == null)
                {
                    _playerDataManagerDontdesytoy = FindFirstObjectByType<PlayerDataManagerDontdesytoy>();
                }

                if (_playerDataManagerDontdesytoy.scritpableobjPlayerData == null)
                {
                    _playerDataManagerDontdesytoy.scritpableobjPlayerData =
                        ScriptableObject.CreateInstance<PlayerData>();
                }

                JsonUtility.FromJsonOverwrite(jsonData, _playerDataManagerDontdesytoy.scritpableobjPlayerData);
                Debug.Log("PlayerData loaded from: " + _savePath);
            }
            else
            {
                Debug.LogWarning("No PlayerData file found at: " + _savePath);
            }
        }

        /// <summary>
        /// 플레이어 데이터 찾기
        /// </summary>
        /// <param name="action">게임 데이터가 존재하지않을떄 실행할 액션</param>
        private void FindPlayerdata(Action action)
        {
            _serverManager.GameDataGet(action);
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