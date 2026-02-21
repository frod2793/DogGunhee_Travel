#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using InGame;

namespace Tests
{
    /// <summary>
    /// [설명]: 인트로 씬의 로그인/회원가입 흐름을 자동화하여 테스트하는 컴포넌트입니다.
    /// 에디터에서 PlayMode 중 특정 키 입력을 통해 테스트를 트리거하거나 자동 실행할 수 있습니다.
    /// </summary>
    public class AutoLoginTester : MonoBehaviour
    {
        #region 에디터 설정
        [Header("테스트 설정")]
        [SerializeField] private bool m_autoStartOnLoad = false;
        [SerializeField] private float m_actionDelay = 0.5f;

        [Header("테스트 계정")]
        [SerializeField] private string m_testId = "test_user_001";
        [SerializeField] private string m_testPw = "password123!";
        #endregion

        #region 초기화
        private async void Start()
        {
            if (m_autoStartOnLoad)
            {
                await RunAllTests();
            }
        }

        private void Update()
        {
            // F5 키를 누르면 게스트 로그인 테스트 실행
            if (Input.GetKeyDown(KeyCode.F5))
            {
                RunGuestLoginTest().Forget();
            }

            // F6 키를 누르면 일반 로그인 테스트 실행
            if (Input.GetKeyDown(KeyCode.F6))
            {
                RunNormalLoginTest().Forget();
            }

            // F7 키를 누르면 회원가입 테스트 실행
            if (Input.GetKeyDown(KeyCode.F7))
            {
                RunSignUpTest().Forget();
            }
        }
        #endregion

        #region 테스트 로직 (비즈니스 로직 시뮬레이션)

        /// <summary>
        /// [설명]: 모든 테스트 시나리오를 순차적으로 실행합니다. (성공 시 로비로 전환되므로 개별 실행 권장)
        /// </summary>
        public async UniTask RunAllTests()
        {
            Debug.Log("[AutoLoginTester] 테스트 시작...");
            await RunGuestLoginTest();
        }

        /// <summary>
        /// [설명]: 게스트 로그인 흐름을 시뮬레이션합니다.
        /// </summary>
        public async UniTask RunGuestLoginTest()
        {
            Debug.Log("[AutoLoginTester] 게스트 로그인 테스트 시작");
            
            var guestBtn = FindObjectByPath<Button>("Title_UI/Logins_BtnGroup/GuestLogin_Button");
            if (guestBtn == null) return;

            guestBtn.onClick.Invoke();
            await VerifyLobbyTransition();
        }

        /// <summary>
        /// [설명]: 아이디/비밀번호 로그인 흐름을 시뮬레이션합니다.
        /// </summary>
        public async UniTask RunNormalLoginTest()
        {
            Debug.Log("[AutoLoginTester] 일반 로그인 테스트 시작");

            // 1. 로그인 팝업 열기
            var openBtn = FindObjectByPath<Button>("Title_UI/Logins_BtnGroup/Login_Button");
            openBtn?.onClick.Invoke();
            await UniTask.Delay(System.TimeSpan.FromSeconds(m_actionDelay));

            // 2. 필드 입력
            var idField = FindObjectByPath<TMP_InputField>("Title_UI/PopUp_Group/LoginPopUP/LoginPopUp_Back/ID_InputField ");
            var pwField = FindObjectByPath<TMP_InputField>("Title_UI/PopUp_Group/LoginPopUP/LoginPopUp_Back/PW_InputField");

            if (idField != null) idField.text = m_testId;
            if (pwField != null) pwField.text = m_testPw;

            // 3. 제출
            var submitBtn = FindObjectByPath<Button>("Title_UI/PopUp_Group/LoginPopUP/LoginPopUp_Back/Login_Button");
            submitBtn?.onClick.Invoke();

            await VerifyLobbyTransition();
        }

        /// <summary>
        /// [설명]: 회원가입 흐름을 시뮬레이션합니다.
        /// </summary>
        public async UniTask RunSignUpTest()
        {
            Debug.Log("[AutoLoginTester] 회원가입 테스트 시작");

            // 1. 로그인 팝업 -> 회원가입 팝업
            FindObjectByPath<Button>("Title_UI/Logins_BtnGroup/Login_Button")?.onClick.Invoke();
            await UniTask.Delay(System.TimeSpan.FromSeconds(m_actionDelay));

            FindObjectByPath<Button>("Title_UI/PopUp_Group/LoginPopUP/LoginPopUp_Back/Singup_Button (Open_SingUpPopUp)")?.onClick.Invoke();
            await UniTask.Delay(System.TimeSpan.FromSeconds(m_actionDelay));

            // 2. 필드 입력
            string randId = "user_" + Random.Range(1000, 9999);
            string randNick = "Tester_" + Random.Range(100, 999);

            var nickField = FindObjectByPath<TMP_InputField>("Title_UI/PopUp_Group/SingUP_PopUP/SingUP/NickName_InputField");
            var idField = FindObjectByPath<TMP_InputField>("Title_UI/PopUp_Group/SingUP_PopUP/SingUP/ID_InputField (1)");
            var pwField = FindObjectByPath<TMP_InputField>("Title_UI/PopUp_Group/SingUP_PopUP/SingUP/PW_InputField ");
            var pwCheckField = FindObjectByPath<TMP_InputField>("Title_UI/PopUp_Group/SingUP_PopUP/SingUP/PWCheck_InputField  ");

            if (nickField != null) nickField.text = randNick;
            if (idField != null) idField.text = randId;
            if (pwField != null) pwField.text = m_testPw;
            if (pwCheckField != null) pwCheckField.text = m_testPw;

            // 3. 제출
            FindObjectByPath<Button>("Title_UI/PopUp_Group/SingUP_PopUP/SingUP/SingUP_Button")?.onClick.Invoke();

            await VerifyLobbyTransition();
        }

        #endregion

        #region 유틸리티
        private T FindObjectByPath<T>(string path) where T : Component
        {
            var obj = GameObject.Find(path);
            if (obj == null)
            {
                Debug.LogWarning($"[AutoLoginTester] 대상을 찾을 수 없습니다: {path}");
                return null;
            }
            return obj.GetComponent<T>();
        }

        private async UniTask VerifyLobbyTransition()
        {
            Debug.Log("[AutoLoginTester] 로비 전환 대기 중...");
            float timeout = 15f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                if (SceneManager.GetActiveScene().name == SceneNames.Lobby)
                {
                    Debug.Log("[AutoLoginTester] 테스트 성공: 로비 진입 확인됨");
                    return;
                }
                await UniTask.Yield();
                elapsed += Time.deltaTime;
            }

            Debug.LogError("[AutoLoginTester] 테스트 실패: 로비 진입 타임아웃");
        }
        #endregion
    }
}
#endif
