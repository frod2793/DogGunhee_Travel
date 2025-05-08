using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DogGuns_Games.Lobby
{
    /// <summary>
    /// 로비 UI를 관리하는 클래스
    /// </summary>
    public class LobbyUIManager : MonoBehaviour
    {
        #region 변수 및 필드

        [Header("<color=green>플레이 및 설정 버튼 UI 목록</color>")]
        [SerializeField] private Button startBtn;
        [SerializeField] private Button tutorialBtn;
        [SerializeField] private Button optionBtn;

        [Header("팝업 UI")]
        [SerializeField] private GameObject optionPopUp;
        [SerializeField] private GameObject cgamePopUp;

        [Header("<color=green>캐릭터 시스템</color>")]
        [SerializeField] private SelectCharacter_UI_Manager selectCharacterUIManager;
        [SerializeField] private Button openCharacterSelectButton;
        [SerializeField] private Button openCharacterListPanel;
        [SerializeField] private Button openCharacterSkillViewPanel;
        [SerializeField] private Button closeCharacterSelectButton;

        [Header("<color=green>우편 시스템</color>")]
        [SerializeField] private PostManager postManager;
        [SerializeField] private Button openMessingerButton;

        [Header("<color=green>퀘스트 시스템</color>")]
        [SerializeField] private QuestPanelManager questPanelManager;
        [SerializeField] private Button openQuestPanelButton;

        [Header("<color=green>재화 시스템</color>")]
        [SerializeField] private TMP_Text gold;
        [SerializeField] private TMP_Text dia;

        [Header("플레이어 정보")]
        [SerializeField] private PlayerDataManagerDontdesytoy playerDataManagerDontdesytoy;

        // 상수
        private const string ErrorNullReference = "참조가 없습니다: {0}";

        #endregion

        #region Unity 라이프사이클

        /// <summary>
        /// 초기화 작업 수행
        /// </summary>
        private void Awake()
        {
            // 플레이어 데이터 매니저 찾기
            playerDataManagerDontdesytoy = FindAnyObjectByType<PlayerDataManagerDontdesytoy>();

            // 버튼 초기화
            InitializeButtons();
        }

        /// <summary>
        /// 화면에 재화 정보 표시
        /// </summary>
        private void Start()
        {
            UpdateCurrencyDisplay();
        }

        #endregion

        #region 초기화 메서드

        /// <summary>
        /// 모든 버튼 초기화
        /// </summary>
        private void InitializeButtons()
        {
            playButton_Init();
            CharacterSelct_Init();
            InitOtherSystems();
        }

        /// <summary>
        /// 게임 관련 버튼 이벤트 등록
        /// </summary>
        private void playButton_Init()
        {
            if (startBtn != null)
                startBtn.onClick.AddListener(func_startBtn);
            else
                Debug.LogError(string.Format(ErrorNullReference, "시작 버튼"));

            if (tutorialBtn != null)
                tutorialBtn.onClick.AddListener(func_tutorialBtn);
            else
                Debug.LogError(string.Format(ErrorNullReference, "튜토리얼 버튼"));

            if (optionBtn != null)
                optionBtn.onClick.AddListener(func_optionBtn);
            else
                Debug.LogError(string.Format(ErrorNullReference, "옵션 버튼"));
        }

        /// <summary>
        /// 캐릭터 선택창 버튼 이벤트 등록
        /// </summary>
        private void CharacterSelct_Init()
        {
            if (selectCharacterUIManager == null)
            {
                Debug.LogError(string.Format(ErrorNullReference, "캐릭터 선택 매니저"));
                return;
            }

            if (openCharacterSelectButton != null)
                openCharacterSelectButton.onClick.AddListener(selectCharacterUIManager.OpenCharacterSelectPanel);
            else
                Debug.LogError(string.Format(ErrorNullReference, "캐릭터 선택창 열기 버튼"));

            if (openCharacterListPanel != null)
                openCharacterListPanel.onClick.AddListener(selectCharacterUIManager.OpenCharacterListPanel);
            else
                Debug.LogError(string.Format(ErrorNullReference, "캐릭터 리스트 열기 버튼"));

            if (openCharacterSkillViewPanel != null)
                openCharacterSkillViewPanel.onClick.AddListener(selectCharacterUIManager.OpenCharacterSkillViewPanel);
            else
                Debug.LogError(string.Format(ErrorNullReference, "캐릭터 스킬뷰 열기 버튼"));

            if (closeCharacterSelectButton != null)
                closeCharacterSelectButton.onClick.AddListener(selectCharacterUIManager.CloseCharacterSelectPanel);
            else
                Debug.LogError(string.Format(ErrorNullReference, "캐릭터 선택창 닫기 버튼"));
        }

        /// <summary>
        /// 기타 시스템 초기화 (우편함, 퀘스트 등)
        /// </summary>
        private void InitOtherSystems()
        {
            if (openMessingerButton != null && postManager != null)
                openMessingerButton.onClick.AddListener(postManager.OpenPostBoxPanel);
            else
                Debug.LogError(string.Format(ErrorNullReference, "우편함 버튼 또는 매니저"));

            if (openQuestPanelButton != null && questPanelManager != null)
                openQuestPanelButton.onClick.AddListener(questPanelManager.OpenQuestPanel);
            else
                Debug.LogError(string.Format(ErrorNullReference, "퀘스트 버튼 또는 매니저"));
        }

        #endregion

        #region UI 업데이트 메서드

        /// <summary>
        /// 화면에 재화 정보 업데이트
        /// </summary>
        private void UpdateCurrencyDisplay()
        {
            if (playerDataManagerDontdesytoy?.scritpableobjPlayerData == null)
            {
                Debug.LogError(string.Format(ErrorNullReference, "플레이어 데이터"));
                return;
            }

            if (gold != null)
                gold.text = playerDataManagerDontdesytoy.scritpableobjPlayerData.currency1.ToString();

            if (dia != null)
                dia.text = playerDataManagerDontdesytoy.scritpableobjPlayerData.currency2.ToString();
        }

        #endregion

        #region 버튼 콜백 함수

        /// <summary>
        /// 시작 버튼 콜백 - 게임 선택 팝업 표시
        /// </summary>
        private void func_startBtn()
        {
            Debug.Log("게임 선택 팝업");

            if (cgamePopUp != null)
                cgamePopUp.SetActive(true);
            else
                Debug.LogError(string.Format(ErrorNullReference, "게임 선택 팝업"));
        }

        /// <summary>
        /// 튜토리얼 버튼 콜백 - 미구현
        /// </summary>
        private void func_tutorialBtn()
        {
            // 튜토리얼 기능 구현 예정
            Debug.Log("튜토리얼 기능 호출됨 (미구현)");
        }

        /// <summary>
        /// 옵션 버튼 콜백 - 옵션 팝업 표시
        /// </summary>
        private void func_optionBtn()
        {
            if (optionPopUp != null)
            {
                // 새로운 인스턴스 생성 대신 활성화
                optionPopUp.SetActive(true);
            }
            else
                Debug.LogError(string.Format(ErrorNullReference, "옵션 팝업"));
        }

        #endregion

        #region 게임 진행 함수

        /// <summary>
        /// 게임 실행 - 씬 전환
        /// </summary>
        private void runGame()
        {
            if (SceneLoader.Instace != null)
            {
                SceneLoader.Instace.LoadScene("RunGame");
                Debug.Log("런게임 씬으로 전환");
            }
            else
            {
                Debug.LogError(string.Format(ErrorNullReference, "씬 로더"));
            }
        }

        #endregion
    }
}