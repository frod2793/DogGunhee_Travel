using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using InGame;
using InGame.Lobby;

namespace Lobby
{
    /// <summary>
    /// 로비 UI를 관리하는 View 클래스입니다.
    /// LobbyViewModel을 구독하여 데이터를 표시합니다.
    /// </summary>
    public class LobbyUIViewManager : MonoBehaviour
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
        [SerializeField] private InGame.UI.Popups.PostView m_postManager;
        [SerializeField] private Button m_openPostButton;
        [SerializeField] private Button m_getPostRewardButton;
        [SerializeField] private Button m_closePostButton;
        [SerializeField] private Button m_getPostExpansionRewardButton;
        [SerializeField] private Button m_closePostExpansionButton;

        [Header("<color=green>퀘스트 시스템</color>")]
        [SerializeField] private InGame.UI.Popups.QuestInfoView m_questPanelManager;
        [SerializeField] private Button m_openQuestPanelButton;
        [SerializeField] private Button m_closeQuestPanelButton;
        [SerializeField] private Button m_closeQuestExpansionButton;

        [Header("<color=green>재화 시스템</color>")]
        [SerializeField] private TMP_Text m_goldText;
        [SerializeField] private TMP_Text m_diaText;
        
        [Header("<color=green>상점 시스템</color>")]
        [SerializeField] private InGame.UI.Popups.StoreView m_storeManager;
        [SerializeField] private Button m_openStoreButton;
        [SerializeField] private Button m_closeStoreButton;
        [SerializeField] private Button m_closeStoreExpendPopUp;

        [Header("<color=green>아이템 팝업</color>")]
        [SerializeField] private InGame.UI.Popups.ItemSelectView m_itemSelectManager;
        [SerializeField] private Button m_openItemSelectButton;
        [SerializeField] private Button m_closeItemSelectButton;
        [SerializeField] private Button m_closeItemSelectExpansionButton;

        [Header("<color=green>플레이어 정보</color>")]
        [SerializeField] private PlayerDataManager m_playerDataManager;

        [Header("Debug")]
        [SerializeField] private bool m_isDebugMode;

        [Header("<color=green>배경 애니메이션</color>")]
        [SerializeField] private Animator m_backgroundAnimator;
        [Tooltip("배경 애니메이션의 재생 속도를 조절합니다.")]
        [SerializeField] private float m_backgroundAnimationSpeed = 1.7f;

        #endregion

        #region 내부 상태 변수

        // private static readonly Stack<Action> s_closePopUpActions = new Stack<Action>(); // Removed: Use PopupManager
        private OptionPopupManager m_currentOptionPopup;
        private float m_cachedAnimationSpeed = -1f;
        
        // MVVM 연결
        private LobbyViewModel m_viewModel;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            InitializeButtons();
        }

        private void Start()
        {
            if (m_playerDataManager == null) 
                m_playerDataManager = PlayerDataManager.Instance;

            // ViewModel 초기화 및 바인딩
            m_viewModel = new LobbyViewModel(m_playerDataManager);
            BindViewModel();
            
            SoundManager.PlaySound(Sound.BGM, SoundKeys.Lobby, true);
            SoundManager.Instance.LoadSoundSetting();

            if (m_backgroundAnimator != null)
            {
                m_backgroundAnimator.speed = m_backgroundAnimationSpeed;
            }

            PlayBackgroundAnimation("Start");
        }

        private void Update()
        {
            if (m_backgroundAnimator != null && Mathf.Abs(m_cachedAnimationSpeed - m_backgroundAnimationSpeed) > Mathf.Epsilon)
            {
                m_cachedAnimationSpeed = m_backgroundAnimationSpeed;
                m_backgroundAnimator.speed = m_backgroundAnimationSpeed;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                InGame.UI.PopupManager.Instance.CloseTopPopup();
            }
        }

        private void OnDestroy()
        {
            m_disposables.Dispose();
            m_viewModel?.Dispose();
        }

        #endregion

        #region MVVM 바인딩

        private void BindViewModel()
        {
            // 닉네임 바인딩
            m_viewModel.Nickname.Subscribe(nick =>
            {
                if (m_playerNameText) m_playerNameText.SetText(nick);
            }).AddTo(m_disposables);

            // 레벨 바인딩
            m_viewModel.Level.Subscribe(level =>
            {
                if (m_playerLevelText) m_playerLevelText.SetText("Lv. {0}", level);
            }).AddTo(m_disposables);

            // 경험치 바인딩
            m_viewModel.Experience.Subscribe(exp =>
            {
                if (m_playerLevelSlider) m_playerLevelSlider.value = exp;
            }).AddTo(m_disposables);

            // 골드 바인딩
            m_viewModel.Gold.Subscribe(gold =>
            {
                if (m_goldText) m_goldText.SetText("{0}", gold);
            }).AddTo(m_disposables);

            // 다이아 바인딩
            m_viewModel.Diamond.Subscribe(dia =>
            {
                if (m_diaText) m_diaText.SetText("{0}", dia);
            }).AddTo(m_disposables);
        }

        /// <summary>
        /// 외부에서 ViewModel 데이터를 갱신할 때 호출합니다.
        /// </summary>
        public void RefreshPlayerData()
        {
            m_viewModel?.RefreshFromPlayerData();
        }

        #endregion

        #region 초기화

        private void InitializeButtons()
        {
            // 메인 버튼
            m_startBtn.SetOnClick(() => OpenPopup(m_gameSelectPopUp));
            m_tutorialBtn.SetOnClick(() => SceneLoader.Instance.LoadScene("Tutorial"));
            m_optionBtn.SetOnClick(OnOptionButtonClicked);
            
            // 게임 선택 팝업
            m_closeGameSelectPopUpBtn.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);
            m_gameStartButton.SetOnClick(() => SceneLoader.Instance.LoadScene("VamSerlike"));
            m_gametestStartButton.SetOnClick(() => SceneLoader.Instance.LoadScene("VamSerLike_Test"));

            // 캐릭터 선택
            m_openCharacterSelectButton.SetOnClick(() => m_characterSelectUIManager.OpenCharacterSelectPanel());

            // 우편 시스템
            m_openPostButton.SetOnClick(() => m_postManager.OpenPostBoxPanel());
            m_closePostButton.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);
            m_closePostExpansionButton.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);
            m_getPostRewardButton.SetOnClick(() => m_postManager.OnClickDetailRewardBtn());
            m_getPostExpansionRewardButton.SetOnClick(() => m_postManager.OnClickDetailRewardBtn());

            // 퀘스트 시스템
            m_openQuestPanelButton.SetOnClick(() => m_questPanelManager.OpenQuestPanel());
            m_closeQuestPanelButton.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);
            m_closeQuestExpansionButton.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);

            // 상점 시스템
            m_openStoreButton.SetOnClick(() => m_storeManager.OpenStorePanel());
            m_closeStoreButton.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);
            m_closeStoreExpendPopUp.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);

            // 아이템 선택
            m_openItemSelectButton.SetOnClick(() => m_itemSelectManager.OpenItemSelectPanel());
            m_closeItemSelectButton.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);
            m_closeItemSelectExpansionButton.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);
        }

        #endregion

        #region 배경 애니메이션 제어

        public void PlayBackgroundAnimation(string triggerName)
        {
            if (m_backgroundAnimator != null && !string.IsNullOrEmpty(triggerName))
            {
                m_backgroundAnimator.SetTrigger(triggerName);
            }
        }

        public void StopBackgroundAnimation()
        {
            if (m_backgroundAnimator != null)
            {
                m_backgroundAnimator.Rebind();
                m_backgroundAnimator.Update(0f);
            }
        }

        #endregion

        #region 팝업 관리 시스템

        private void OpenPopup(GameObject popup)
        {
            if (popup == null) return;
            
            popup.SetActive(true);
            InGame.UI.PopupManager.Instance.RegisterPopup(() => popup.SetActive(false));
        }

        private void OnOptionButtonClicked()
        {
            if (m_optionPopupPrefab == null) return;

            if (m_currentOptionPopup != null)
            {
                Destroy(m_currentOptionPopup.gameObject);
            }

            m_currentOptionPopup = Instantiate(m_optionPopupPrefab, transform.parent); 
            
            InGame.UI.PopupManager.Instance.RegisterPopup(() => 
            {
                if (m_currentOptionPopup != null)
                {
                    Destroy(m_currentOptionPopup.gameObject);
                    m_currentOptionPopup = null;
                }
            });
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