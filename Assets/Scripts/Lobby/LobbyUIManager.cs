using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DogGuns_Games.Lobby
{
    /// <summary>
    /// 로비 UI를 관리하는 클래스
    /// </summary>
    public class LobbyUIManager : MonoBehaviour
    {
        #region 변수 및 필드
        
        [Header("<color=green>플레이어 프로필")]
        [Tooltip("플레이어 프로필 이미지입니다.")]
        [FormerlySerializedAs("playerProfileImage")] [SerializeField] private Image m_playerProfileImage;
        [Tooltip("플레이어 이름 텍스트입니다.")]
        [FormerlySerializedAs("playerNameText")] [SerializeField] private TMP_Text m_playerNameText;
        [Tooltip("플레이어 레벨 텍스트입니다.")]
        [FormerlySerializedAs("playerLevelText")] [SerializeField] private TMP_Text m_playerLevelText;
        [Tooltip("플레이어 경험치 슬라이더입니다.")]
        [FormerlySerializedAs("playerLevelSlider")] [SerializeField] private Slider m_playerLevelSlider;

        [Header("<color=green>플레이 및 설정 버튼 UI 목록</color>")]
        [Tooltip("게임 시작 팝업을 여는 버튼입니다.")]
        [FormerlySerializedAs("startBtn")] [SerializeField] private Button m_startBtn;
        [Tooltip("튜토리얼 씬으로 이동하는 버튼입니다.")]
        [FormerlySerializedAs("tutorialBtn")] [SerializeField] private Button m_tutorialBtn;
        [Tooltip("옵션 팝업을 여는 버튼입니다.")]
        [FormerlySerializedAs("optionBtn")] [SerializeField] private Button m_optionBtn;

        [Header("<color=green>팝업 UI</color>")]
        [Tooltip("인스턴스화할 옵션 팝업 프리팹입니다.")]
        [FormerlySerializedAs("optionPopupPrefab")] [SerializeField] private OptionPopupManager m_optionPopupPrefab;
        [Tooltip("게임 시작 선택 팝업입니다.")]
        [FormerlySerializedAs("cgamePopUp")] [SerializeField] private GameObject m_gameSelectPopUp;
        [Tooltip("게임 시작 선택 팝업을 닫는 버튼입니다.")]
        [FormerlySerializedAs("closeBtn")] [SerializeField] private Button m_closeGameSelectPopUpBtn;
        [Tooltip("Vamser-like 게임을 시작하는 버튼입니다.")]
        [FormerlySerializedAs("GameStartButton")] [SerializeField] private Button m_gameStartButton;

        [Header("<color=green>캐릭터 선택 시스템</color>")]
        [Tooltip("캐릭터 선택 UI 매니저입니다.")]
        [FormerlySerializedAs("characterSelectUIManager")] [SerializeField] private CharacterSelectUIManager m_characterSelectUIManager;
        [Tooltip("캐릭터 선택 패널을 여는 버튼입니다.")]
        [FormerlySerializedAs("openCharacterSelectButton")] [SerializeField] private Button m_openCharacterSelectButton;

        [Header("<color=green>우편 시스템</color>")]
        [Tooltip("우편 UI 매니저입니다.")]
        [FormerlySerializedAs("postManager")] [SerializeField] private PostManager m_postManager;
        [Tooltip("우편함 패널을 여는 버튼입니다.")]
        [FormerlySerializedAs("openMessingerButton")] [SerializeField] private Button m_openPostButton;
        [Tooltip("우편 목록에서 보상을 수령하는 버튼입니다.")]
        [FormerlySerializedAs("getPostReiwordButton")] [SerializeField] private Button m_getPostRewardButton;
        [Tooltip("우편함 패널을 닫는 버튼입니다.")]
        [FormerlySerializedAs("closeMessingerButton")] [SerializeField] private Button m_closePostButton;
        [Tooltip("우편 상세 보기에서 보상을 수령하는 버튼입니다.")]
        [FormerlySerializedAs("getPostExpensionReiwordButton")] [SerializeField] private Button m_getPostExpansionRewardButton;
        [Tooltip("우편 상세 보기 패널을 닫는 버튼입니다.")]
        [FormerlySerializedAs("closeMessingerExpensionButton")] [SerializeField] private Button m_closePostExpansionButton;

        [Header("<color=green>퀘스트 시스템</color>")]
        [Tooltip("퀘스트 UI 매니저입니다.")]
        [FormerlySerializedAs("questPanelManager")] [SerializeField] private QuestPanelManager m_questPanelManager;
        [Tooltip("퀘스트 패널을 여는 버튼입니다.")]
        [FormerlySerializedAs("openQuestPanelButton")] [SerializeField] private Button m_openQuestPanelButton;
        [Tooltip("퀘스트 패널을 닫는 버튼입니다.")]
        [FormerlySerializedAs("closeQuestPanelButton")] [SerializeField] private Button m_closeQuestPanelButton;
        [Tooltip("퀘스트 상세 보기 패널을 닫는 버튼입니다.")]
        [FormerlySerializedAs("closeQuestExpensionButton")] [SerializeField] private Button m_closeQuestExpansionButton;

        [Header("<color=green>재화 시스템</color>")]
        [Tooltip("골드 재화 텍스트입니다.")]
        [FormerlySerializedAs("gold")] [SerializeField] private TMP_Text m_goldText;
        [Tooltip("다이아 재화 텍스트입니다.")]
        [FormerlySerializedAs("dia")] [SerializeField] private TMP_Text m_diaText;
        
        [Header("<color=green>상점 시스템</color>")]
        [Tooltip("상점 UI 매니저입니다.")]
        [FormerlySerializedAs("storeManager")] [SerializeField] private StoreManager m_storeManager;
        [Tooltip("상점 패널을 여는 버튼입니다.")]
        [FormerlySerializedAs("openStoreButton")] [SerializeField] private Button m_openStoreButton;
        [Tooltip("상점 패널을 닫는 버튼입니다.")]
        [FormerlySerializedAs("closeStoreButton")] [SerializeField] private Button m_closeStoreButton;
        [Tooltip("상점 상세 보기 팝업을 닫는 버튼입니다.")]
        [FormerlySerializedAs("closeStoreExpendPopUp")] [SerializeField] private Button m_closeStoreExpendPopUp;

        [Header("<color=green>아이템 팝업</color>")]
        [Tooltip("아이템 선택 UI 매니저입니다.")]
        [FormerlySerializedAs("itemSelectManager")] [SerializeField] private ItemSelectManager m_itemSelectManager;
        [Tooltip("아이템 선택 패널을 여는 버튼입니다.")]
        [FormerlySerializedAs("openItemSelectButton")] [SerializeField] private Button m_openItemSelectButton;
        [Tooltip("아이템 선택 패널을 닫는 버튼입니다.")]
        [FormerlySerializedAs("closeItemSelectButton")] [SerializeField] private Button m_closeItemSelectButton;
        [Tooltip("아이템 상세 보기 패널을 닫는 버튼입니다.")]
        [FormerlySerializedAs("closeItemSelectExpensionButton")] [SerializeField] private Button m_closeItemSelectExpansionButton;

        [Header("<color=green>플레이어 정보</color>")]
        [Tooltip("플레이어 데이터 매니저입니다.")]
        [FormerlySerializedAs("playerDataManagerDontdesytoy")] [SerializeField] private PlayerDataManagerDontdesytoy m_playerDataManager;

        private static readonly Stack<Action> s_closePopUpActions = new Stack<Action>();

        // 상수
        private const string ErrorNullReference = "참조가 없습니다: {0}";

        [Header("Debug")]
        [Tooltip("활성화 시, 참조가 누락되었을 때 에러 로그를 출력합니다.")]
        [SerializeField] private bool m_isDebugMode = false;

        private OptionPopupManager m_currentOptionPopup; // 현재 활성화된 옵션 팝업 인스턴스

        #endregion

        #region Unity 라이프사이클

        /// <summary>
        /// 초기화 작업 수행
        /// </summary>
        private void Awake()
        {
            // 필수 참조 확인 및 초기화
            CheckRequiredReferences();
    
            // 버튼 초기화
            InitializeButtons();
        }

        /// <summary>
        /// 화면에 재화 정보 표시
        /// </summary>
        private void Start()
        {
            // 싱글톤 패턴 활용 - FindAnyObjectByType 사용 최소화
            if (m_playerDataManager == null)
                m_playerDataManager = PlayerDataManagerDontdesytoy.Instance;
    
            UpdateCurrencyDisplay();
            SetPlayerData ();
            SoundManager.PlaySound(Sound.BGM, SoundKeys.Lobby, true);
            SoundManager.Instance.LoadSoundSetting();

        }

        private void SetPlayerData()
        {
            if (m_playerDataManager == null || m_playerDataManager.PlayerData == null)
            {
                if (m_isDebugMode) Debug.LogError(string.Format(ErrorNullReference, "플레이어 데이터"));
                return;
            }

            m_playerNameText.text = m_playerDataManager.PlayerData.nickname;
            m_playerLevelText.text = $"Lv. {m_playerDataManager.PlayerData.level}";
            m_playerLevelSlider.value = m_playerDataManager.PlayerData.experience / 100f; // 예시로 100을 최대 경험치로 설정
            
        }
        
        /// <summary>
        /// 필수 참조 확인
        /// </summary>
        private void CheckRequiredReferences()
        {
            // 중요 매니저 참조 확인
            if (m_characterSelectUIManager == null)
                if (m_isDebugMode) Debug.LogWarning(string.Format(ErrorNullReference, "캐릭터 선택 매니저"));
    
            if (m_postManager == null)
                if (m_isDebugMode) Debug.LogWarning(string.Format(ErrorNullReference, "우편 매니저"));
    
            if (m_questPanelManager == null)
                if (m_isDebugMode) Debug.LogWarning(string.Format(ErrorNullReference, "퀘스트 매니저"));
    
            if (m_storeManager == null)
                if (m_isDebugMode) Debug.LogWarning(string.Format(ErrorNullReference, "상점 매니저"));
    
            if (m_itemSelectManager == null)
                if (m_isDebugMode) Debug.LogWarning(string.Format(ErrorNullReference, "아이템 선택 매니저"));
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
            InitializePlayButtons();
            InitializeCharacterSelect();
            InitializePostManager();
            InitializeQuestManager();
            InitializeStoreManager();
            InitializeItemSelectManager();
        }

        /// <summary>
        /// 게임 관련 버튼 이벤트 등록
        /// </summary>
        private void InitializePlayButtons()
        {
            RegisterButtonOnClick(m_startBtn, OnStartButtonClicked, "시작 버튼");
            RegisterButtonOnClick(m_tutorialBtn, OnTutorialButtonClicked, "튜토리얼 버튼");
            RegisterButtonOnClick(m_optionBtn, OnOptionButtonClicked, "옵션 버튼");
            RegisterButtonOnClick(m_closeGameSelectPopUpBtn, CloseButtonClick, "게임 선택 팝업 닫기 버튼");
            RegisterButtonOnClick(m_gameStartButton, VamSerlikeStart, "Vamser-like 시작 버튼");
        }

        /// <summary>
        /// 캐릭터 선택창 버튼 이벤트 등록
        /// </summary>
        private void InitializeCharacterSelect()
        {
            RegisterButtonOnClick(m_openCharacterSelectButton, m_characterSelectUIManager.OpenCharacterSelectPanel, "캐릭터 선택창 열기 버튼", m_characterSelectUIManager);
        }

        /// <summary>
        /// 아이템 선택 시스템 초기화
        /// </summary>
        private void InitializeItemSelectManager()
        {
            RegisterButtonOnClick(m_openItemSelectButton, m_itemSelectManager.OpenItemSelectPanel, "아이템 선택 버튼", m_itemSelectManager);
            RegisterButtonOnClick(m_closeItemSelectButton, CloseButtonClick, "아이템 선택 닫기 버튼", m_itemSelectManager);
            RegisterButtonOnClick(m_closeItemSelectExpansionButton, CloseButtonClick, "아이템 확장 닫기 버튼", m_itemSelectManager);
        }

        /// <summary>
        /// 상점 시스템 초기화
        /// </summary>
        private void InitializeStoreManager()
        {
            RegisterButtonOnClick(m_openStoreButton, m_storeManager.OpenStorePanel, "상점 버튼", m_storeManager);
            RegisterButtonOnClick(m_closeStoreButton, m_storeManager.CloseStorePanel, "상점 닫기 버튼", m_storeManager);
            RegisterButtonOnClick(m_closeStoreExpendPopUp, CloseButtonClick, "상점 확장 닫기 버튼", m_storeManager);
        }

        /// <summary>
        /// 퀘스트 관련 버튼 이벤트 초기화
        /// </summary>
        private void InitializeQuestManager()
        {
            RegisterButtonOnClick(m_openQuestPanelButton, m_questPanelManager.OpenQuestPanel, "퀘스트 버튼", m_questPanelManager);
            RegisterButtonOnClick(m_closeQuestPanelButton, CloseButtonClick, "퀘스트 닫기 버튼", m_questPanelManager);
            RegisterButtonOnClick(m_closeQuestExpansionButton, CloseButtonClick, "퀘스트 확장 닫기 버튼", m_questPanelManager);
        }

        /// <summary>
        /// 우편 시스템 관련 버튼 이벤트 초기화
        /// </summary>
        private void InitializePostManager()
        {
            RegisterButtonOnClick(m_openPostButton, m_postManager.OpenPostBoxPanel, "우편함 버튼", m_postManager);
            RegisterButtonOnClick(m_closePostButton, CloseButtonClick, "우편함 닫기 버튼", m_postManager);
            RegisterButtonOnClick(m_closePostExpansionButton, CloseButtonClick, "우편함 확장 닫기 버튼", m_postManager);
            RegisterButtonOnClick(m_getPostRewardButton, m_postManager.Getreward, "우편 보상 수령 버튼", m_postManager);
            RegisterButtonOnClick(m_getPostExpansionRewardButton, () =>
            {
                m_postManager.Getreward();
                CloseButtonClick();
            }, "우편 확장 보상 수령 버튼", m_postManager);
        }

        /// <summary>
        /// 버튼 클릭 이벤트를 안전하게 등록하는 헬퍼 메서드입니다.
        /// </summary>
        private void RegisterButtonOnClick(Button button, Action action, string buttonName, UnityEngine.Object dependency = null)
        {
            if (button != null && (dependency == null || dependency != null))
            {
                button.onClick.AddListener(() => action?.Invoke());
            }
            else if (m_isDebugMode)
            {
                Debug.LogError(string.Format(ErrorNullReference, buttonName));
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
            if (action == null)
            {
                // if (m_isDebugMode) Debug.LogError(string.Format(ErrorNullReference, "액션"));
                return;
            }
            
            s_closePopUpActions.Push(action);
            // if (m_isDebugMode) Debug.Log($"팝업 닫기 액션 등록됨 (현재 {s_closePopUpActions.Count}개)");
            
        }

        /// <summary>
        /// 팝업 닫기 버튼 클릭 처리
        /// </summary>
        private void CloseButtonClick()
        {
            if (s_closePopUpActions.Any())
            {
                Action lastAction = s_closePopUpActions.Pop();
                lastAction.Invoke();
                
                if (m_isDebugMode) Debug.Log($"팝업 닫기 실행 (남은 팝업: {s_closePopUpActions.Count}개)");
            }
            else
            {
                if (m_isDebugMode) Debug.Log("닫을 팝업이 없습니다.");
            }
        }

        #endregion

        #region UI 업데이트 메서드

        /// <summary>
        /// 화면에 재화 정보 업데이트
        /// </summary>
        private void UpdateCurrencyDisplay()
        {
            if (m_playerDataManager?.PlayerData == null)
            {
                if (m_isDebugMode) Debug.LogError(string.Format(ErrorNullReference, "플레이어 데이터"));
                return;
            }

            if (m_goldText != null)
                m_goldText.text = m_playerDataManager.PlayerData.currency1.ToString("N0");

            if (m_diaText != null)
                m_diaText.text = m_playerDataManager.PlayerData.currency2.ToString("N0");
            
            if (m_isDebugMode) Debug.Log("재화 정보 업데이트 완료");
        }

        #endregion

        #region 버튼 콜백 함수

        /// <summary>
        /// 시작 버튼 콜백 - 게임 선택 팝업 표시
        /// </summary>
        private void OnStartButtonClicked()
        {
            if (m_isDebugMode) Debug.Log("게임 선택 팝업");

            if (m_gameSelectPopUp != null)
            {
                m_gameSelectPopUp.SetActive(true);
                AddClosePopUpAction(() => m_gameSelectPopUp.SetActive(false));
            }
            else
                if (m_isDebugMode) Debug.LogError(string.Format(ErrorNullReference, "게임 선택 팝업"));
        }

        /// <summary>
        /// 튜토리얼 버튼 콜백 - 기본 튜토리얼 시작
        /// </summary>
        private void OnTutorialButtonClicked()
        {
            if (m_isDebugMode) Debug.Log("튜토리얼 시작");
            // TODO: 튜토리얼 씬으로 이동하거나 가이드 표시
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene("Tutorial");
            }
        }
        
        /// <summary>
        /// 옵션 버튼 콜백 - 옵션 팝업 표시
        /// </summary>
        private void OnOptionButtonClicked()
        {
            if (m_optionPopupPrefab != null)
            {
                // 이미 열린 팝업이 있으면 닫기
                if (m_currentOptionPopup != null)
                {
                    Destroy(m_currentOptionPopup.gameObject);
                    m_currentOptionPopup = null;
                }

                // 새 옵션 팝업 인스턴스 생성
                m_currentOptionPopup = Instantiate(m_optionPopupPrefab, transform.parent);
                
                // 팝업 닫기 액션 등록
                AddClosePopUpAction(() => {
                    if (m_currentOptionPopup != null)
                    {
                        Destroy(m_currentOptionPopup.gameObject);
                        m_currentOptionPopup = null;
                    }
                });
                
                if (m_isDebugMode) Debug.Log("옵션 팝업 표시");
            }
            else
                if (m_isDebugMode) Debug.LogError(string.Format(ErrorNullReference, "옵션 팝업 프리팹"));
        }

        #endregion

        #region 게임 진행 함수

        /// <summary>
        /// 게임 실행 - 씬 전환
        /// </summary>
        private void VamSerlikeStart()
        {
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene("VamSerlike");
                if (m_isDebugMode) Debug.Log("런게임 씬으로 전환");
            }
            else
            {
                if (m_isDebugMode) Debug.LogError(string.Format(ErrorNullReference, "씬 로더"));
            }
        }

        #endregion
    }
}
