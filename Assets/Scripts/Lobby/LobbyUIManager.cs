using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks; // UniTask 활용
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DogGuns_Games.Lobby
{
    /// <summary>
    /// 로비 UI를 관리하는 클래스 (GetReward 오류 수정됨)
    /// </summary>
    public class LobbyUIManager : MonoBehaviour
    {
        #region 인스펙터 연결 필드

        [Header("<color=green>플레이어 프로필")]
        [SerializeField] private Image m_playerProfileImage;
        [SerializeField] private TMP_Text m_playerNameText;
        [SerializeField] private TMP_Text m_playerLevelText;
        [SerializeField] private Slider m_playerLevelSlider;

        [Header("<color=green>플레이 및 설정 버튼 UI 목록</color>")]
        [SerializeField] private Button m_startBtn;
        [SerializeField] private Button m_tutorialBtn;
        [SerializeField] private Button m_optionBtn;

        [Header("<color=green>게임선택 UI</color>")]
        [SerializeField] private OptionPopupManager m_optionPopupPrefab;
        [SerializeField] private GameObject m_gameSelectPopUp;
        [SerializeField] private Button m_closeGameSelectPopUpBtn;
        [SerializeField] private Button m_gameStartButton;
        [SerializeField] private Button m_gametestStartButton;

        [Header("<color=green>캐릭터 선택 시스템</color>")]
        [SerializeField] private CharacterSelectUIManager m_characterSelectUIManager;
        [SerializeField] private Button m_openCharacterSelectButton;

        [Header("<color=green>우편 시스템</color>")]
        [SerializeField] private PostManager m_postManager;
        [SerializeField] private Button m_openPostButton;
        [SerializeField] private Button m_getPostRewardButton;
        [SerializeField] private Button m_closePostButton;
        [SerializeField] private Button m_getPostExpansionRewardButton;
        [SerializeField] private Button m_closePostExpansionButton;

        [Header("<color=green>퀘스트 시스템</color>")]
        [SerializeField] private QuestPanelManager m_questPanelManager;
        [SerializeField] private Button m_openQuestPanelButton;
        [SerializeField] private Button m_closeQuestPanelButton;
        [SerializeField] private Button m_closeQuestExpansionButton;

        [Header("<color=green>재화 시스템</color>")]
        [SerializeField] private TMP_Text m_goldText;
        [SerializeField] private TMP_Text m_diaText;
        
        [Header("<color=green>상점 시스템</color>")]
        [SerializeField] private StoreManager m_storeManager;
        [SerializeField] private Button m_openStoreButton;
        [SerializeField] private Button m_closeStoreButton;
        [SerializeField] private Button m_closeStoreExpendPopUp;

        [Header("<color=green>아이템 팝업</color>")]
        [SerializeField] private ItemSelectManager m_itemSelectManager;
        [SerializeField] private Button m_openItemSelectButton;
        [SerializeField] private Button m_closeItemSelectButton;
        [SerializeField] private Button m_closeItemSelectExpansionButton;

        [Header("<color=green>플레이어 정보</color>")]
        [SerializeField] private PlayerDataManagerDontdesytoy m_playerDataManager;

        [Header("Debug")]
        [SerializeField] private bool m_isDebugMode = false;

        #endregion

        #region 내부 상태 변수

        private static readonly Stack<Action> s_closePopUpActions = new Stack<Action>();
        private OptionPopupManager m_currentOptionPopup; 

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            InitializeButtons();
        }

        private void Start()
        {
            if (m_playerDataManager == null) 
                m_playerDataManager = PlayerDataManagerDontdesytoy.Instance;

            UpdatePlayerDataUI();
            
            SoundManager.PlaySound(Sound.BGM, SoundKeys.Lobby, true);
            SoundManager.Instance.LoadSoundSetting();

            HandleBackButtonAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        #endregion

        #region 초기화 및 UI 업데이트

        private void InitializeButtons()
        {
            // 1. 메인 버튼
            m_startBtn.SetOnClick(() => OpenPopup(m_gameSelectPopUp));
            m_tutorialBtn.SetOnClick(() => SceneLoader.Instance.LoadScene("Tutorial"));
            m_optionBtn.SetOnClick(OnOptionButtonClicked);
            
            // 2. 게임 선택 팝업
            m_closeGameSelectPopUpBtn.SetOnClick(CloseTopPopup);
            m_gameStartButton.SetOnClick(() => SceneLoader.Instance.LoadScene("VamSerlike"));
            m_gametestStartButton.SetOnClick(() => SceneLoader.Instance.LoadScene("VamSerLike_Test"));

            // 3. 캐릭터 선택
            m_openCharacterSelectButton.SetOnClick(() => m_characterSelectUIManager.OpenCharacterSelectPanel());

            // 4. 우편 시스템
            m_openPostButton.SetOnClick(() => m_postManager.OpenPostBoxPanel());
            m_closePostButton.SetOnClick(CloseTopPopup);
            m_closePostExpansionButton.SetOnClick(CloseTopPopup);
            
            // [수정] GetReward() -> OnClickDetailRewardBtn() 으로 변경
            m_getPostRewardButton.SetOnClick(() => m_postManager.OnClickDetailRewardBtn());
            m_getPostExpansionRewardButton.SetOnClick(() => 
            {
                // [수정] GetReward() -> OnClickDetailRewardBtn() 으로 변경
                m_postManager.OnClickDetailRewardBtn();
            });

            // 5. 퀘스트 시스템
            m_openQuestPanelButton.SetOnClick(() => m_questPanelManager.OpenQuestPanel());
            m_closeQuestPanelButton.SetOnClick(CloseTopPopup);
            m_closeQuestExpansionButton.SetOnClick(CloseTopPopup);

            // 6. 상점 시스템
            m_openStoreButton.SetOnClick(() => m_storeManager.OpenStorePanel());
            m_closeStoreButton.SetOnClick(() => m_storeManager.CloseStorePanel());
            m_closeStoreExpendPopUp.SetOnClick(CloseTopPopup);

            // 7. 아이템 선택
            m_openItemSelectButton.SetOnClick(() => m_itemSelectManager.OpenItemSelectPanel());
            m_closeItemSelectButton.SetOnClick(CloseTopPopup);
            m_closeItemSelectExpansionButton.SetOnClick(CloseTopPopup);
        }

        private void UpdatePlayerDataUI()
        {
            if (m_playerDataManager?.PlayerData == null) return;

            var data = m_playerDataManager.PlayerData;

            if (m_playerNameText) m_playerNameText.text = data.nickname;
            if (m_playerLevelText) m_playerLevelText.text = $"Lv. {data.level}";
            if (m_playerLevelSlider) m_playerLevelSlider.value = data.experience / 100f;

            if (m_goldText) m_goldText.text = data.currency1.ToString("N0");
            if (m_diaText) m_diaText.text = data.currency2.ToString("N0");
        }

        #endregion

        #region 팝업 관리 시스템

        private void OpenPopup(GameObject popup)
        {
            if (popup == null) return;
            
            popup.SetActive(true);
            AddClosePopUpAction(() => popup.SetActive(false));
        }

        private void OnOptionButtonClicked()
        {
            if (m_optionPopupPrefab == null) return;

            if (m_currentOptionPopup != null)
            {
                Destroy(m_currentOptionPopup.gameObject);
            }

            m_currentOptionPopup = Instantiate(m_optionPopupPrefab, transform.parent); 
            
            AddClosePopUpAction(() => 
            {
                if (m_currentOptionPopup != null)
                {
                    Destroy(m_currentOptionPopup.gameObject);
                    m_currentOptionPopup = null;
                }
            });
        }

        public static void AddClosePopUpAction(Action action)
        {
            if (action == null) return;
            s_closePopUpActions.Push(action);
        }

        public static void CloseTopPopup()
        {
            if (s_closePopUpActions.Count > 0)
            {
                s_closePopUpActions.Pop().Invoke();
            }
        }
        
        public static void RemoveLastClosePopUpAction()
        {
            if (s_closePopUpActions.Count > 0)
            {
                s_closePopUpActions.Pop();
            }
        }

        private async UniTaskVoid HandleBackButtonAsync(System.Threading.CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseTopPopup();
                }
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        #endregion
    }

    /// <summary>
    /// [헬퍼] 버튼 이벤트 등록을 위한 확장 메서드
    /// </summary>
    public static class ButtonExtensions
    {
        public static void SetOnClick(this Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(action);
            }
        }
    }
}