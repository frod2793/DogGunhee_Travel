using System;
using System.Collections.Generic;
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

        [Header("<color=green>팝업 UI</color>")]
        [SerializeField] private GameObject optionPopUp;
        [SerializeField] private GameObject cgamePopUp;

        [Header("<color=green>캐릭터 선택 시스템</color>")]
        [SerializeField] private CharacterSelectUIManager characterSelectUIManager;
        [SerializeField] private Button openCharacterSelectButton;

        [Header("<color=green>우편 시스템</color>")]
        [SerializeField] private PostManager postManager;
        [SerializeField] private Button openMessingerButton;
        [SerializeField] private Button getPostReiwordButton;
        [SerializeField] private Button closeMessingerButton;
        [SerializeField] private Button getPostExpensionReiwordButton;
        [SerializeField] private Button closeMessingerExpensionButton;

        [Header("<color=green>퀘스트 시스템</color>")]
        [SerializeField] private QuestPanelManager questPanelManager;
        [SerializeField] private Button openQuestPanelButton;
        [SerializeField] private Button closeQuestPanelButton;
        [SerializeField] private Button closeQuestExpensionButton;

        [Header("<color=green>재화 시스템</color>")]
        [SerializeField] private TMP_Text gold;
        [SerializeField] private TMP_Text dia;
        
        [Header("<color=green>상점 시스템</color>")]
        [SerializeField] private StoreManager storeManager;
        [SerializeField] private Button openStoreButton;
        [SerializeField] private Button closeStoreButton;
        [SerializeField] private Button closeStoreExpendPopUp;

        [Header("<color=green>아이템 팝업</color>")]
        [SerializeField] private ItemSelectManager itemSelectManager;
        [SerializeField] private Button openItemSelectButton;
        [SerializeField] private Button closeItemSelectButton;
        [SerializeField] private Button closeItemSelectExpensionButton;

        [Header("<color=green>플레이어 정보</color>")]
        [SerializeField] private PlayerDataManagerDontdesytoy playerDataManagerDontdesytoy;

        private static List<Action> closePopUpActionList = new List<Action>();

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
            if (playerDataManagerDontdesytoy == null)
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

        /// <summary>
        /// 모바일 뒤로가기 버튼 입력 감지
        /// </summary>
        private void Update()
        {
            ClickmobileBackButton();
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
            InitQuestManager();
            InitPostManager();
            InitStoreManager();
            InitItemSelectManager();
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
            if (characterSelectUIManager == null)
            {
                Debug.LogError(string.Format(ErrorNullReference, "캐릭터 선택 매니저"));
                return;
            }

            if (openCharacterSelectButton != null)
                openCharacterSelectButton.onClick.AddListener(characterSelectUIManager.OpenCharacterSelectPanel);
            else
                Debug.LogError(string.Format(ErrorNullReference, "캐릭터 선택창 열기 버튼"));
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

        /// <summary>
        /// 아이템 선택 시스템 초기화
        /// </summary>
        private void InitItemSelectManager()
        {
            if (openItemSelectButton != null && itemSelectManager != null)
                openItemSelectButton.onClick.AddListener(() => itemSelectManager.OpenItemSelectPanel());
            else
                Debug.LogError(string.Format(ErrorNullReference, "아이템 선택 버튼 또는 매니저"));
        
            // 아이템 선택 닫기 버튼 초기화
            if (closeItemSelectButton != null && itemSelectManager != null)
            {
                closeItemSelectButton.onClick.AddListener(CloseButtonClick);
                Debug.Log("아이템 선택 닫기 버튼 이벤트 등록 완료");
            }
            else
                Debug.LogError(string.Format(ErrorNullReference, "아이템 선택 닫기 버튼 또는 매니저"));
        
            // 아이템 확장 패널 닫기 버튼 초기화
            if (closeItemSelectExpensionButton != null && itemSelectManager != null)
            {
                closeItemSelectExpensionButton.onClick.AddListener(CloseButtonClick);
                Debug.Log("아이템 확장 닫기 버튼 이벤트 등록 완료");
            }
            else
                Debug.LogError(string.Format(ErrorNullReference, "아이템 확장 닫기 버튼 또는 매니저"));
        }

        /// <summary>
        /// 상점 시스템 초기화
        /// </summary>
        private void InitStoreManager()
        {
            if (openStoreButton != null && storeManager != null)
                openStoreButton.onClick.AddListener(() => storeManager.OpenStorePanel());
            else
                Debug.LogError(string.Format(ErrorNullReference, "상점 버튼 또는 매니저"));

            if (closeStoreButton != null && storeManager != null)
                closeStoreButton.onClick.AddListener(() => storeManager.CloseStoreItemPopUp());
            else
                Debug.LogError(string.Format(ErrorNullReference, "상점 닫기 버튼 또는 매니저"));
        
            // 상점 확장 팝업 닫기 버튼 초기화
            if (closeStoreExpendPopUp != null && storeManager != null)
            {
                closeStoreExpendPopUp.onClick.AddListener(CloseButtonClick);
                Debug.Log("상점 확장 닫기 버튼 이벤트 등록 완료");
            }
            else
                Debug.LogError(string.Format(ErrorNullReference, "상점 확장 닫기 버튼 또는 매니저"));
        }

        /// <summary>
        /// 퀘스트 관련 버튼 이벤트 초기화
        /// </summary>
        private void InitQuestManager()
        {
            if (closeQuestPanelButton != null && questPanelManager != null)
                closeQuestPanelButton.onClick.AddListener(CloseButtonClick);
            else
                Debug.LogError(string.Format(ErrorNullReference, "퀘스트 닫기 버튼 또는 매니저"));

            if (closeQuestExpensionButton != null && questPanelManager != null)
                closeQuestExpensionButton.onClick.AddListener(CloseButtonClick);
            else
                Debug.LogError(string.Format(ErrorNullReference, "퀘스트 확장 닫기 버튼 또는 매니저"));
        }

        /// <summary>
        /// 우편 시스템 관련 버튼 이벤트 초기화
        /// </summary>
        private void InitPostManager()
        {
            // 우편함 닫기 버튼 초기화
            if (closeMessingerButton != null && postManager != null)
            {
                closeMessingerButton.onClick.AddListener(CloseButtonClick);
                Debug.Log("우편함 닫기 버튼 이벤트 등록 완료");
            }
            else
            {
                Debug.LogError(string.Format(ErrorNullReference, "우편함 닫기 버튼 또는 매니저"));
            }

            // 우편함 확장 패널 닫기 버튼 초기화
            if (closeMessingerExpensionButton != null && postManager != null)
            {
                closeMessingerExpensionButton.onClick.AddListener(CloseButtonClick);
                Debug.Log("우편함 확장 닫기 버튼 이벤트 등록 완료");
            }
            else
            {
                Debug.LogError(string.Format(ErrorNullReference, "우편함 확장 닫기 버튼 또는 매니저"));
            }

            // 우편 보상 수령 버튼 초기화
            if (getPostExpensionReiwordButton != null && postManager != null)
            {
                getPostExpensionReiwordButton.onClick.AddListener(() =>
                {
                    postManager.Getreward();
                    CloseButtonClick();
                });

                // 우편 목록 보상 버튼 초기화
                if (getPostReiwordButton != null)
                {
                    getPostReiwordButton.onClick.AddListener(() => postManager.Getreward());
                    Debug.Log("우편 보상 수령 버튼 이벤트 등록 완료");
                }
            }
            else
            {
                Debug.LogError(string.Format(ErrorNullReference, "우편 보상 수령 버튼 또는 매니저"));
            }
        }

        /// <summary>
        /// 모바일 뒤로가기 버튼 처리
        /// </summary>
        private void ClickmobileBackButton()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseButtonClick();
            }
        }

        /// <summary>
        /// 팝업 닫기 액션 추가
        /// </summary>
        public static void AddClosePopUpAction(Action action)
        {
            if (action != null)
            {
                closePopUpActionList.Add(action);
                Debug.Log($"팝업 닫기 액션 등록됨 (현재 {closePopUpActionList.Count}개)");
            }
            else
            {
                Debug.LogError(string.Format(ErrorNullReference, "액션"));
            }
        }

        /// <summary>
        /// 팝업 닫기 버튼 클릭 처리
        /// </summary>
        private void CloseButtonClick()
        {
            if (closePopUpActionList.Count > 0)
            {
                int lastIndex = closePopUpActionList.Count - 1;
                Action lastAction = closePopUpActionList[lastIndex];
                
                closePopUpActionList.RemoveAt(lastIndex);
                lastAction?.Invoke();
                
                Debug.Log($"팝업 닫기 실행 (남은 팝업: {closePopUpActionList.Count}개)");
            }
            else
            {
                Debug.Log("닫을 팝업이 없습니다.");
            }
        }

        #endregion

        #region UI 업데이트 메서드

        /// <summary>
        /// 화면에 재화 정보 업데이트
        /// </summary>
        public void UpdateCurrencyDisplay()
        {
            if (playerDataManagerDontdesytoy?.scritpableobjPlayerData == null)
            {
                Debug.LogError(string.Format(ErrorNullReference, "플레이어 데이터"));
                return;
            }

            if (gold != null)
                gold.text = playerDataManagerDontdesytoy.scritpableobjPlayerData.currency1.ToString("N0");

            if (dia != null)
                dia.text = playerDataManagerDontdesytoy.scritpableobjPlayerData.currency2.ToString("N0");
            
            Debug.Log("재화 정보 업데이트 완료");
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
            {
                cgamePopUp.SetActive(true);
                AddClosePopUpAction(() => cgamePopUp.SetActive(false));
            }
            else
                Debug.LogError(string.Format(ErrorNullReference, "게임 선택 팝업"));
        }

        /// <summary>
        /// 튜토리얼 버튼 콜백 - 기본 튜토리얼 시작
        /// </summary>
        private void func_tutorialBtn()
        {
            Debug.Log("튜토리얼 시작");
            // TODO: 튜토리얼 씬으로 이동하거나 가이드 표시
            if (SceneLoader.Instace != null)
            {
                SceneLoader.Instace.LoadScene("Tutorial");
            }
        }

        /// <summary>
        /// 옵션 버튼 콜백 - 옵션 팝업 표시
        /// </summary>
        private void func_optionBtn()
        {
            if (optionPopUp != null)
            {
                optionPopUp.SetActive(true);
                AddClosePopUpAction(() => optionPopUp.SetActive(false));
                Debug.Log("옵션 팝업 표시");
            }
            else
                Debug.LogError(string.Format(ErrorNullReference, "옵션 팝업"));
        }

        #endregion

        #region 게임 진행 함수

        /// <summary>
        /// 게임 실행 - 씬 전환
        /// </summary>
        public void runGame()
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