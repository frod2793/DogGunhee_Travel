using Cysharp.Threading.Tasks;
using InGame;
using InGame.Core;
using InGame.Core.Interfaces;
using InGame.Data;
using InGame.Data.Managers;
using InGame.Managers;
using InGame.Services;
using InGame.UI;
using UnityEngine;
using InGame.Lobby;

namespace Lobby
{
    /// <summary>
    /// [설명]: 로비 씬의 진입점이자 의존성 조립(Composition)을 담당하는 Root 클래스입니다.
    /// SceneLoader로부터 payload를 수신하여 필수 매니저들을 해석하고 LobbyUIViewManager에 주입합니다.
    /// </summary>
    public class LobbySceneCompositionRoot : MonoBehaviour, ISceneInitializer
    {
        #region 에디터 설정

        [Header("<color=orange>독립 프리팹 (DontDestroyOnLoad)</color>")]
        [SerializeField, Tooltip("씬 로더 (독립 프리팹)")]
        private SceneLoader m_sceneLoader;

        [SerializeField, Tooltip("사운드 관리 서비스 (독립 프리팹)")]
        private SoundManager m_soundManager;

        [Header("<color=yellow>[- Core Managers -] 프리팹 내 포함</color>")]
        [SerializeField, Tooltip("팝업 관리 서비스")]
        private PopupManager m_popupManager;  

        [SerializeField, Tooltip("이펙트 관리 서비스")]
        private EffectManager m_effectManager;

        [SerializeField, Tooltip("리모트 데이터 업데이트 서비스")]
        private RemoteDataUpdateManager m_remoteDataManager;

        [SerializeField, Tooltip("인벤토리 시스템 매니저")]
        private InventoryManager m_inventoryManager;

        [Header("<color=cyan>조립 대상</color>")]
        [SerializeField, Tooltip("실제 로비 UI를 구동하는 뷰 매니저")]
        private LobbyUIViewManager m_lobbyUIViewManager;

        [Header("<color=red>프리팹 원본 (씬 내 미존재 시 Instantiate)</color>")]
        [SerializeField, Tooltip("SceneLoader 프리팹 원본")]
        private SceneLoader m_sceneLoaderPrefab;

        [SerializeField, Tooltip("SoundManager 프리팹 원본")]
        private SoundManager m_soundManagerPrefab;

        [SerializeField, Tooltip("[- Core Managers -] 프리팹 원본 (PopupManager, EffectManager, RemoteDataUpdateManager, InventoryManager 포함)")]
        private GameObject m_coreManagersPrefab;

        #endregion

        #region 인터페이스 구현 (ISceneInitializer)

        /// <summary>
        /// [설명]: 씬 전환 시 SceneLoader에서 호출되어 초기 페이로드를 전달합니다.
        /// </summary>
        /// <param name="payload">이전 씬에서 넘어온 통합 상태 데이터 (ScenePayloadDTO 등)</param>
        public UniTask OnInitialize(object payload)
        {
            LogManager.Log("[LobbySceneCompositionRoot] 로비 씬 초기화 시작", LogManager.LogCategory.System);

            // 1. 코어 매니저 찾기 (씬 내 미존재 시 프리팹 Instantiate)
            ResolveCoreDependencies();

            // 2. 페이로드 해석
            PlayerDataDTO playerData = null;
            ServerSessionDTO serverSession = null;
            ISoundManager soundManager = m_soundManager;

            if (payload is ScenePayloadDTO scenePayload)
            {
                playerData = scenePayload.PlayerData;
                serverSession = scenePayload.ServerSession;
                if (scenePayload.SoundService != null)
                {
                    soundManager = scenePayload.SoundService;
                }
            }
            else if (payload is PlayerDataDTO dto)
            {
                playerData = dto;
            }

            // 에디터 직접 실행 방어 코드 (임시 데이터)
            if (playerData == null)
            {
                LogManager.LogWarning("[LobbySceneCompositionRoot] PlayerData가 제공되지 않아 기본값으로 초기화합니다.");
                playerData = new PlayerDataDTO();
            }

            // 3. 서비스 생성
            var encryptService = new EncryptionService();
            var localRepo = new LocalPlayerDataRepository(encryptService);
            var playerService = new PlayerDataService(playerData, encryptService, localRepo);

            // 4. 인벤토리 컨텍스트 초기화 (전역 로직 이관)
            if (m_inventoryManager != null && serverSession?.GameData != null)
            {
                m_inventoryManager.Init(serverSession.GameData);
            }

            // 5. 의존성 묶음 생성
            var dependencies = new LobbyDependencies
            {
                PlayerData = playerData,
                ServerSession = serverSession,
                PlayerService = playerService,
                SoundManager = soundManager,
                SceneLoader = m_sceneLoader,
                PopupService = m_popupManager,
                EffectService = m_effectManager,
                RemoteDataService = m_remoteDataManager,
                InventoryContext = m_inventoryManager
            };

            // 6. 뷰 매니저에 주입 및 초기화 요청
            if (m_lobbyUIViewManager == null)
            {
                m_lobbyUIViewManager = FindFirstObjectByType<LobbyUIViewManager>();
            }

            if (m_lobbyUIViewManager != null)
            {
                m_lobbyUIViewManager.Initialize(dependencies);
            }
            else
            {
                LogManager.LogError("[LobbySceneCompositionRoot] LobbyUIViewManager를 찾을 수 없습니다.");
            }

            return UniTask.CompletedTask;
        }

        #endregion

        #region 내부 메서드

        /// <summary>
        /// [설명]: 필요한 매니저들이 씬 내에 존재하는지 확인하고,
        /// 존재하지 않으면 프리팹을 Instantiate하여 생성합니다.
        /// 탐색 우선순위: 인스펙터 직접 할당 → FindFirstObjectByType → 프리팹 Instantiate
        /// </summary>
        private void ResolveCoreDependencies()
        {
            // --- 독립 프리팹: SceneLoader ---
            if (m_sceneLoader == null)
            {
                m_sceneLoader = FindFirstObjectByType<SceneLoader>();
            }
            if (m_sceneLoader == null && m_sceneLoaderPrefab != null)
            {
                m_sceneLoader = Instantiate(m_sceneLoaderPrefab);
                m_sceneLoader.name = m_sceneLoaderPrefab.name;
                LogManager.Log("[LobbySceneCompositionRoot] SceneLoader 프리팹 Instantiate", LogManager.LogCategory.System);
            }

            // --- 독립 프리팹: SoundManager ---
            if (m_soundManager == null)
            {
                m_soundManager = FindFirstObjectByType<SoundManager>();
            }
            if (m_soundManager == null && m_soundManagerPrefab != null)
            {
                m_soundManager = Instantiate(m_soundManagerPrefab);
                m_soundManager.name = m_soundManagerPrefab.name;
                LogManager.Log("[LobbySceneCompositionRoot] SoundManager 프리팹 Instantiate", LogManager.LogCategory.System);
            }

            // --- [- Core Managers -] 프리팹: PopupManager, EffectManager, RemoteDataUpdateManager, InventoryManager ---
            if (m_popupManager == null) m_popupManager = FindFirstObjectByType<PopupManager>();
            if (m_effectManager == null) m_effectManager = FindFirstObjectByType<EffectManager>();
            if (m_remoteDataManager == null) m_remoteDataManager = FindFirstObjectByType<RemoteDataUpdateManager>();
            if (m_inventoryManager == null) m_inventoryManager = FindFirstObjectByType<InventoryManager>();

            // 4개 중 하나라도 없으면 [- Core Managers -] 프리팹을 통째로 Instantiate
            bool needCoreManagers = (m_popupManager == null || m_effectManager == null
                                     || m_remoteDataManager == null || m_inventoryManager == null);

            if (needCoreManagers && m_coreManagersPrefab != null)
            {
                GameObject coreInstance = Instantiate(m_coreManagersPrefab);
                coreInstance.name = m_coreManagersPrefab.name;
                LogManager.Log("[LobbySceneCompositionRoot] [- Core Managers -] 프리팹 Instantiate", LogManager.LogCategory.System);

                // Instantiate 후 컴포넌트 재탐색
                if (m_popupManager == null) m_popupManager = coreInstance.GetComponentInChildren<PopupManager>();
                if (m_effectManager == null) m_effectManager = coreInstance.GetComponentInChildren<EffectManager>();
                if (m_remoteDataManager == null) m_remoteDataManager = coreInstance.GetComponentInChildren<RemoteDataUpdateManager>();
                if (m_inventoryManager == null) m_inventoryManager = coreInstance.GetComponentInChildren<InventoryManager>();
            }
        }

        #endregion
    }
}
