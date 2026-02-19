using System;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using InGame;
using Lobby.UI.Elements;
using InGame.Core;
using Lobby.Core;

namespace Lobby
{
    /// <summary>
    /// [설명]: 로비 UI의 진입점이자 각 서브 뷰 컴포넌트들을 조율하는 루트 관리자 클래스입니다.
    /// 비즈니스 로직은 ViewModel이, 화면 이동은 Navigator가, 서브 시스템 제어는 Service가 담당합니다.
    /// </summary>
    public class LobbyUIViewManager : MonoBehaviour, ISceneInitializer
    {
        #region 에디터 설정 (서브 뷰 컴포넌트)

        [Header("<color=cyan>서브 뷰 컴포넌트</color>")]
        [SerializeField, Tooltip("플레이어 프로필 정보 표시 뷰")]
        private LobbyPlayerProfileView m_playerProfileView;

        [SerializeField, Tooltip("재화 정보 표시 뷰")]
        private LobbyCurrencyView m_currencyView;

        [SerializeField, Tooltip("메인 버튼 및 모드 선택 뷰")]
        private LobbyMainMenuView m_mainMenuView;

        [SerializeField, Tooltip("배경 애니메이션 제어 뷰")]
        private LobbyBackgroundView m_backgroundView;

        #endregion

        #region 에디터 설정 (의존성 매니저)

        [Header("<color=cyan>서브 시스템 매니저</color>")]
        [SerializeField] private OptionPopupView m_optionPopupPrefab;
        [SerializeField] private InGame.UI.Popups.PostView m_postManager;
        [SerializeField] private InGame.UI.Popups.QuestInfoView m_questPanelManager;
        [SerializeField] private InGame.UI.Popups.StoreView m_storeManager;
        [SerializeField] private InGame.UI.Popups.InventoryView m_inventoryManager;
        [SerializeField] private InGame.Lobby.CharacterSelectUIManager m_characterSelectManager;

        [Header("<color=cyan>데이터 관리</color>")]
        private InGame.Data.PlayerDataDTO m_playerData;
        private InGame.Services.PlayerDataService m_playerService;
        private InGame.Data.ServerSessionDTO m_serverSession;
        private InGame.Services.ISoundManager m_soundManager;

        #endregion

        #region 내부 변수 및 상태

        private LobbyViewModel m_viewModel;
        private ILobbyNavigator m_navigator;
        private ILobbySubSystemService m_subSystemService;

        #endregion

        #region 유니티 생명주기

        private void Start()
        {
            InitializeCore();
            InitializeViews();

            // 배경음악 및 환경 설정
            if (m_soundManager != null)
            {
                m_soundManager.Play(SoundKeys.Lobby.ToString(), Sound.BGM, 1.0f, true);
                m_soundManager.LoadSoundSetting();
            }

            // 초기 배경 연출
            if (m_backgroundView != null)
            {
                m_backgroundView.PlayAnimation("Start");
            }
        }

        private void Update()
        {
            // ESC 키 입력 시 팝업 닫기 처리는 PopupManager에서 통합 관리합니다.
        }

        private void OnDestroy()
        {
            m_viewModel?.Dispose();
        }

        #endregion

        #region 초기화 로직

        /// <summary>
        /// [설명]: SceneLoader로부터 전달받은 데이터를 주입받습니다.
        /// </summary>
        public void OnInitialize(object payload)
        {
            if (payload is InGame.Data.ScenePayloadDTO scenePayload)
            {
                m_playerData = scenePayload.PlayerData;
                m_serverSession = scenePayload.ServerSession;
                m_soundManager = scenePayload.SoundService;
            }
            else if (payload is InGame.Data.PlayerDataDTO dto)
            {
                m_playerData = dto;
                // SoundManager 주입 (Navigator 사용 권장)
            }

            if (m_playerData != null)
            {
                m_playerService = new InGame.Services.PlayerDataService(m_playerData, new InGame.Services.EncryptionService(), new InGame.Data.LocalPlayerDataRepository(new InGame.Services.EncryptionService()));
            }
        }

        /// <summary>
        /// [설명]: ViewModel, Navigator, SubSystemService 등 핵심 로직 객체들을 생성합니다.
        /// </summary>
        private void InitializeCore()
        {
            // 데이터가 주입되지 않았을 경우 (직접 씬 실행 등) 기본값 생성
            if (m_playerData == null)
            {
                m_playerData = new InGame.Data.PlayerDataDTO();
                m_playerService = new InGame.Services.PlayerDataService(m_playerData, new InGame.Services.EncryptionService(), new InGame.Data.LocalPlayerDataRepository(new InGame.Services.EncryptionService()));
            }

            m_viewModel = new LobbyViewModel(m_playerData, m_playerService);

            // 에디터 직접 실행이나 비정상적인 경로로 진입 시 SoundManager 방어 로직
            if (m_soundManager == null)
            {
                m_soundManager = FindFirstObjectByType<SoundManager>();
#if UNITY_EDITOR
                if (m_soundManager == null)
                {
                    LogManager.LogWarning("[LobbyUIViewManager] 사운드 매니저를 찾을 수 없습니다. (에디터 확인 필요)");
                }
#endif
            }
            
            // 네비게이터 및 서비스 생성 (의존성 주입)
            m_navigator = new LobbyNavigator(m_optionPopupPrefab, transform.parent, m_soundManager);
            m_subSystemService = new LobbySubSystemService(
                m_postManager,
                m_questPanelManager,
                m_storeManager,
                m_inventoryManager
            );
        }

        /// <summary>
        /// [설명]: 각각의 서브 뷰 컴포넌트들에 비즈니스 로직과 데이터를 연결합니다.
        /// </summary>
        private void InitializeViews()
        {
            // 현재 상태를 담은 페이로드 생성 (다음 씬 전환용)
            var scenePayload = new InGame.Data.ScenePayloadDTO(m_playerData, m_serverSession);

            if (m_playerProfileView != null) m_playerProfileView.Bind(m_viewModel);
            if (m_currencyView != null) m_currencyView.Bind(m_viewModel);
            if (m_mainMenuView != null) m_mainMenuView.Initialize(m_navigator, m_subSystemService, scenePayload);

            // 서버 세션 정보 추출
            var gameData = m_serverSession?.GameData;
            var postService = m_serverSession?.Post;

            // 서브 시스템 팝업 뷰 초기화 (DTO 및 서비스 주입)
            if (m_inventoryManager != null) m_inventoryManager.Initialize(m_playerData, m_playerService);
            if (m_storeManager != null) m_storeManager.Initialize(m_playerData, m_playerService);
            if (m_characterSelectManager != null) m_characterSelectManager.Initialize(m_playerData, m_playerService);
            
            // 전역 인벤토리 매니저 초기화 (서버 데이터 서비스 주입)
            if (InGame.Lobby.InventoryManager.Instance != null)
            {
                InGame.Lobby.InventoryManager.Instance.Init(gameData);
            }

            // 우편함 초기화 (우편 서비스 주입)
            if (m_postManager != null)
            {
                m_postManager.Initialize(postService);
            }
        }

        #endregion

        #region 공개 API

        /// <summary>
        /// [설명]: 외부 로직에 의해 플레이어 데이터가 변경되었을 때 뷰모델을 갱신합니다.
        /// </summary>
        public void RefreshPlayerData()
        {
            m_viewModel?.RefreshFromPlayerData();
        }

        /// <summary>
        /// [설명]: 배경 애니메이션을 외부에서 제어하기 위한 인터페이스입니다.
        /// </summary>
        public void PlayBackgroundAnimation(string triggerName)
        {
            if (m_backgroundView != null)
            {
                m_backgroundView.PlayAnimation(triggerName);
            }
        }

        #endregion
    }
}
