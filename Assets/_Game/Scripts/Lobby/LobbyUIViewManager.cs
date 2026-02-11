using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using InGame;
using InGame.Lobby;

namespace Lobby
{
    /// <summary>
    /// 로비의 메인 UI 요소들을 제어하고 ViewModel로부터 데이터를 전달받아 화면에 표시하는 View 클래스입니다.
    /// <br/>각 서브 시스템(우편, 퀘스트, 상점 등)의 진입점 역할을 수행합니다.
    /// </summary>
    public class LobbyUIViewManager : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("<color=green>플레이어 프로필</color>")] [SerializeField, Tooltip("플레이어 썸네일 이미지")]
        private Image m_playerProfileImage;

        [SerializeField, Tooltip("플레이어 닉네임 텍스트")]
        private TMP_Text m_playerNameText;

        [SerializeField, Tooltip("플레이어 현재 레벨 텍스트")]
        private TMP_Text m_playerLevelText;

        [SerializeField, Tooltip("경험치 진행 바")] private Slider m_playerLevelSlider;

        [Header("<color=green>메인 컨트롤 UI</color>")] [SerializeField, Tooltip("게임 시작 버튼")]
        private Button m_startBtn;

        [SerializeField, Tooltip("튜토리얼 시작 버튼")]
        private Button m_tutorialBtn;

        [SerializeField, Tooltip("옵션 팝업 열기 버튼")]
        private Button m_optionBtn;

        [Header("<color=green>게임 선택 팝업</color>")] [SerializeField, Tooltip("옵션 팝업 프리팹")]
        private OptionPopupView m_optionPopupPrefab;

        [SerializeField, Tooltip("게임 모드 선택 팝업 오브젝트")]
        private GameObject m_gameSelectPopUp;

        [SerializeField, Tooltip("게임 선택 팝업 닫기 버튼")]
        private Button m_closeGameSelectPopUpBtn;

        [SerializeField, Tooltip("본 게임 시작 버튼")]
        private Button m_gameStartButton;

        [SerializeField, Tooltip("테스트용 게임 시작 버튼")]
        private Button m_gametestStartButton;

        [Header("<color=green>서브 시스템 열기 버튼</color>")] [SerializeField, Tooltip("캐릭터 선택 관리자")]
        private CharacterSelectUIManager m_characterSelectUIManager;

        [SerializeField, Tooltip("캐릭터 선택창 열기 버튼")]
        private Button m_openCharacterSelectButton;

        [SerializeField, Tooltip("우편 관리 화면")] private InGame.UI.Popups.PostView m_postManager;

        [SerializeField, Tooltip("우편함 열기 버튼")] private Button m_openPostButton;

        [SerializeField, Tooltip("퀘스트 관리 화면")] private InGame.UI.Popups.QuestInfoView m_questPanelManager;

        [SerializeField, Tooltip("퀘스트창 열기 버튼")]
        private Button m_openQuestPanelButton;

        [SerializeField, Tooltip("상점 관리 화면")] private InGame.UI.Popups.StoreView m_storeManager;

        [SerializeField, Tooltip("상점 열기 버튼")] private Button m_openStoreButton;

        [SerializeField, Tooltip("아이템 선택 관리 화면")]
        private InGame.UI.Popups.ItemSelectView m_itemSelectManager;

        [SerializeField, Tooltip("아이템 선택창 열기 버튼")]
        private Button m_openItemSelectButton;

        [Header("<color=green>서브 시스템 닫기 버튼</color>")] [SerializeField]
        private Button m_getPostRewardButton;

        [SerializeField] private Button m_closePostButton;
        [SerializeField] private Button m_getPostExpansionRewardButton;
        [SerializeField] private Button m_closePostExpansionButton;
        [SerializeField] private Button m_closeQuestPanelButton;
        [SerializeField] private Button m_closeQuestExpansionButton;
        [SerializeField] private Button m_closeStoreButton;
        [SerializeField] private Button m_closeStoreExpendPopUp;
        [SerializeField] private Button m_closeItemSelectButton;
        [SerializeField] private Button m_closeItemSelectExpansionButton;

        [Header("<color=green>재화 및 데이터</color>")] [SerializeField, Tooltip("보유 골드 텍스트")]
        private TMP_Text m_goldText;

        [SerializeField, Tooltip("보유 다이아 텍스트")]
        private TMP_Text m_diaText;

        [SerializeField, Tooltip("플레이어 데이터 관리자")]
        private PlayerDataManager m_playerDataManager;

        [Header("<color=green>배경 애니메이션</color>")] [SerializeField, Tooltip("배경 애니메이터")]
        private Animator m_backgroundAnimator;

        [SerializeField, Tooltip("배경 애니메이션 재생 속도")]
        private float m_backgroundAnimationSpeed = 1.7f;

        [Header("Debug")] [SerializeField] private bool m_isDebugMode;

        #endregion

        #region 2. 내부 변수 및 상태

        private OptionPopupView m_currentOptionPopup;
        private float m_cachedAnimationSpeed = -1f;

        // MVVM 연결을 위한 ViewModel 및 관리 객체
        private LobbyViewModel m_viewModel;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        #endregion

        #region 3. 유니티 생명주기

        private void Awake()
        {
            InitializeButtons();
        }

        private void Start()
        {
            // 1. 의존성 초기화
            if (m_playerDataManager == null)
            {
                m_playerDataManager = PlayerDataManager.Instance;
            }

            // 2. ViewModel 생성 및 데이터 바인딩
            m_viewModel = new LobbyViewModel(m_playerDataManager);
            BindViewModel();

            // 3. 환경 설정 및 사운드
            SoundManager.PlaySound(Sound.BGM, SoundKeys.Lobby, true);
            SoundManager.Instance.LoadSoundSetting();

            // 4. 초기 배경 연출
            if (m_backgroundAnimator != null)
            {
                m_backgroundAnimator.speed = m_backgroundAnimationSpeed;
            }

            PlayBackgroundAnimation("Start");
        }

        private void Update()
        {
            // 실시간 애니메이션 속도 제어 (Debug/Tuning용)
            if (m_backgroundAnimator != null &&
                Mathf.Abs(m_cachedAnimationSpeed - m_backgroundAnimationSpeed) > Mathf.Epsilon)
            {
                m_cachedAnimationSpeed = m_backgroundAnimationSpeed;
                m_backgroundAnimator.speed = m_backgroundAnimationSpeed;
            }

            // ESC 키 입력 시 팝업 닫기 처리
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

        #region 4. MVVM 데이터 바인딩

        /// <summary>
        /// ViewModel의 ReactiveProperty들을 구독하여 UI 요소와 동기화합니다.
        /// </summary>
        private void BindViewModel()
        {
            if (m_viewModel == null) return;

            // 닉네임 동기화
            m_viewModel.Nickname.Subscribe(nick =>
            {
                if (m_playerNameText != null) m_playerNameText.SetText(nick);
            }).AddTo(m_disposables);

            // 레벨 동기화
            m_viewModel.Level.Subscribe(level =>
            {
                if (m_playerLevelText != null) m_playerLevelText.SetText("Lv. {0}", level);
            }).AddTo(m_disposables);

            // 경험치 진행도 동기화
            m_viewModel.Experience.Subscribe(exp =>
            {
                if (m_playerLevelSlider != null) m_playerLevelSlider.value = exp;
            }).AddTo(m_disposables);

            // 재화(골드) 동기화
            m_viewModel.Gold.Subscribe(gold =>
            {
                if (m_goldText != null) m_goldText.SetText("{0}", gold);
            }).AddTo(m_disposables);

            // 재화(다이아) 동기화
            m_viewModel.Diamond.Subscribe(dia =>
            {
                if (m_diaText != null) m_diaText.SetText("{0}", dia);
            }).AddTo(m_disposables);
        }

        /// <summary>
        /// 외부 로직에 의해 플레이어 데이터가 변경되었을 때 뷰모델을 갱신합니다.
        /// </summary>
        public void RefreshPlayerData()
        {
            m_viewModel?.RefreshFromPlayerData();
        }

        #endregion

        #region 5. 초기화 및 이벤트 등록

        /// <summary>
        /// 모든 버튼 컴포넌트에 클릭 이벤트 핸들러를 등록합니다.
        /// </summary>
        private void InitializeButtons()
        {
            // 1. 메인 버튼 (네비게이션)
            m_startBtn.SetOnClick(() => OpenPopup(m_gameSelectPopUp));
            m_tutorialBtn.SetOnClick(() => SceneLoader.Instance.LoadScene("Tutorial"));
            m_optionBtn.SetOnClick(OnOptionButtonClicked);

            // 2. 게임 모드 선택 팝업 관련
            m_closeGameSelectPopUpBtn.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);
            m_gameStartButton.SetOnClick(() => SceneLoader.Instance.LoadScene("VamSerlike"));
            m_gametestStartButton.SetOnClick(() => SceneLoader.Instance.LoadScene("VamSerLike_Test"));

            // 3. 서브 시스템 진입 (Open)
            m_openCharacterSelectButton.SetOnClick(() =>
            {
                if (m_characterSelectUIManager != null) m_characterSelectUIManager.OpenCharacterSelectPanel();
            });
            m_openPostButton.SetOnClick(() =>
            {
                if (m_postManager != null) m_postManager.OpenPostBoxPanel();
            });
            m_openQuestPanelButton.SetOnClick(() =>
            {
                if (m_questPanelManager != null) m_questPanelManager.OpenQuestPanel();
            });
            m_openStoreButton.SetOnClick(() =>
            {
                if (m_storeManager != null) m_storeManager.OpenStorePanel();
            });
            m_openItemSelectButton.SetOnClick(() =>
            {
                if (m_itemSelectManager != null) m_itemSelectManager.OpenItemSelectPanel();
            });

            // 4. 서브 시스템 보상 및 닫기 제어 (Close/Reward)
            m_closePostButton.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);
            m_closePostExpansionButton.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);
            m_getPostRewardButton.SetOnClick(() =>
            {
                if (m_postManager != null) m_postManager.OnClickDetailRewardBtn();
            });
            m_getPostExpansionRewardButton.SetOnClick(() =>
            {
                if (m_postManager != null) m_postManager.OnClickDetailRewardBtn();
            });

            m_closeQuestPanelButton.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);
            m_closeQuestExpansionButton.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);

            m_closeStoreButton.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);
            m_closeStoreExpendPopUp.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);

            m_closeItemSelectButton.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);
            m_closeItemSelectExpansionButton.SetOnClick(InGame.UI.PopupManager.Instance.CloseTopPopup);
        }

        #endregion

        #region 6. 상태 제어 및 팝업 대행

        /// <summary>
        /// 해당 게임 오브젝트를 활성화하고 PopupManager에 닫기 액션을 등록합니다.
        /// </summary>
        private void OpenPopup(GameObject popup)
        {
            if (popup == null) return;

            popup.SetActive(true);
            InGame.UI.PopupManager.Instance.RegisterPopup(() => popup.SetActive(false));
        }

        /// <summary>
        /// 옵션 팝업 버튼 클릭 시 새 옵션 UI 인스턴스를 생성하고 관리합니다.
        /// </summary>
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

        #region 7. 비주얼 조절 (애니메이션)

        /// <summary>
        /// 지정된 트리거 이름을 기반으로 배경 애니메이션을 재생합니다.
        /// </summary>
        public void PlayBackgroundAnimation(string triggerName)
        {
            if (m_backgroundAnimator != null && !string.IsNullOrEmpty(triggerName))
            {
                m_backgroundAnimator.SetTrigger(triggerName);
            }
        }

        /// <summary>
        /// 배경 애니메이션을 초기 상태로 되돌리고 정지합니다.
        /// </summary>
        public void StopBackgroundAnimation()
        {
            if (m_backgroundAnimator != null)
            {
                m_backgroundAnimator.Rebind();
                m_backgroundAnimator.Update(0f);
            }
        }

        #endregion
    }

    /// <summary>
    /// 버튼 컴포넌트 편의를 위한 확장 메서드 클래스입니다.
    /// </summary>
    public static class ButtonExtensions
    {
        /// <summary>
        /// 기존 리스너를 모두 제거하고 새로운 액션을 등록합니다.
        /// </summary>
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