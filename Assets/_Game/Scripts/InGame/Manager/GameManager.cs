using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using InGame.Lobby;
using InGame.ObjectPool;
using InGame.Player.Player_Base;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Lobby;
using InGame.Weapon;
using InGame.Mob.Systems;
using InGame.Data;
using InGame.Services;
using InGame.Core.Interfaces;

namespace InGame.Managers
{
    /// <summary>
    /// [설명]: 게임의 전체 흐름(시작, 정지, 종료)과 전역 상태(플레이어, 스포너, UI)를 총괄하는 중앙 관리자 클래스입니다.
    /// 외부 시스템과 연동하는 PlayerDataDTO를 주입받아 초기화됩니다.
    /// </summary>

    public class GameManager : MonoBehaviour, IGameStateService, IPlayerContext, ICombatContext, IGameDataProvider, IGameFlowController
    
    {
        #region 싱글톤 및 이벤트

        /// <summary> 플레이어 캐릭터가 스폰되거나 변경될 때 발생하는 이벤트 </summary>
        public event Action<PlayerBase> OnPlayerChanged;

        #endregion

        #region 에디터 설정

        [Header("에디터 설정")]
        [SerializeField, Tooltip("에디터에서 바로 시작할 때 사용할 캐릭터 인덱스")] private int m_startCharacterIndex;
        [SerializeField, Tooltip("테스트용 무기 목록")] private List<SkillData> m_testWeapons = new List<SkillData>();

        [Header("데이터 참조")]
        [SerializeField, Tooltip("전체 스킬 데이터베이스")] private SkillDatabase m_skillDatabase;
        [SerializeField, Tooltip("게임 설정 데이터 (프레임 등)")] private SettingsData m_settingsData;

        [Header("인게임 참조")]
        [SerializeField, Tooltip("플레이어가 생성될 부모 컨테이너")] private GameObject m_playerContainer;
        [SerializeField, Tooltip("맵 경계를 정의하는 스프라이트")] private SpriteRenderer m_mapRange;
        [SerializeField, Tooltip("옵션 팝업 프리팹")] private OptionPopupView m_optionPopupPrefab;

        #endregion

        #region 내부 필드

        private ObjectPoolSpawner m_objectPoolSpawner;
        private PlayerController m_playerController;
        private VariableJoystick m_variableJoystick;
        private Camera m_mainCamera;
        private UIManager m_uiManager;
        private PlayStateManager m_state;
        private bool m_isCleared;
        private WeaponPoolManager m_weaponPoolManager;
        private PlayerHUD m_playerHUD;
        private PlayerCameraAgent m_playerCameraAgent;
        private MobManager m_mobManager;
        private PlayerDataDTO m_playerData;
        private PlayerDataService m_playerService;
        private IGameDataService m_gameDataService;
        private ServerSessionDTO m_serverSession;
        private ISoundManager m_soundManager;
        private ISceneLoader m_sceneLoader;
        private InGame.UI.IPopupService m_popupService;
        private InGame.Managers.IEffectService m_effectService;
        private InGame.Data.Managers.IRemoteDataUpdateService m_remoteDataService;
        private IInventoryContext m_inventoryCtx;
        private bool m_isInitialized; // [추가]: 초기화 완료 플래그 (중복 방지)
        private bool m_initialWavePause = false; // [추가]: 초기화 전 웨이브 일시정지 명령 저장

        private static readonly Vector3 k_SpawnPosition = Vector3.zero;

        #endregion

        #region 공개 프로퍼티

        /// <summary> [설명]: 주입받은 플레이어 데이터 DTO를 반환합니다. </summary>
        public PlayerDataDTO PlayerData => m_playerData;

        /// <summary> 현재 맵에 스폰된 플레이어 캐릭터 </summary>
        public PlayerBase SpawnedPlayer { get; private set; }

        /// <summary> 오브젝트 풀 및 몬스터 스폰 시스템 </summary>
        public ObjectPoolSpawner ObjectPoolSpawner => m_objectPoolSpawner;

        /// <summary> 플레이어 입력 컨트롤러 </summary>
        public PlayerController PlayerController => m_playerController;

        /// <summary> 사운드 매니저 참조 </summary>
        public InGame.Services.ISoundManager SoundManager => m_soundManager;

        public PlayStateManager.GameState PlayState => m_state?.PlayState ?? PlayStateManager.GameState.Ready;
        /// <summary> 게임의 상태(시작, 정지, 종료) 관리자 </summary>
        public PlayStateManager State => m_state;

        /// <summary> [설명]: 현재 게임이 클리어된 상태인지 여부입니다. </summary>
        public bool IsCleared => m_isCleared;

        /// <summary> 가상 조이스틱 참조 </summary>
        public VariableJoystick Joystick => m_variableJoystick;

        /// <summary> 메인 카메라 참조 </summary>
        public Camera MainCamera => m_mainCamera;

        /// <summary> UI 매니저 참조 </summary>
        public UIManager UIManager => m_uiManager;

        /// <summary> [IPlayerContext 구현]: 플레이어 위치 정보를 가진 Transform </summary>
        public Transform PlayerTransform => m_playerContainer != null ? m_playerContainer.transform : transform;

        /// <summary> [IGameStateService 구현]: 현재 게임이 플레이 중인지 여부 </summary>
        public bool IsPlaying => m_state != null && m_state.IsPlaying;

        /// <summary> [IGameStateService 구현]: 이펙트 서비스 </summary>
        public IEffectService EffectService => m_effectService;

        /// <summary> [IGameStateService 구현]: 사운드 서비스 </summary>
        public ISoundManager SoundService => m_soundManager;

        /// <summary> 현재 맵의 이동 가능 경계 </summary>
        public Bounds MapBounds
        {
            get
            {
                if (m_objectPoolSpawner != null && m_objectPoolSpawner.MapBounds.size.sqrMagnitude > 1f)
                {
                    return m_objectPoolSpawner.MapBounds;
                }
                return m_mapRange != null ? m_mapRange.bounds : new Bounds(Vector3.zero, Vector3.one * 100);
            }
        }

        public int ActiveMobCount => m_mobManager != null ? m_mobManager.GetAllActiveTargets().Count : 0;

        /// <summary> 몬스터 타겟팅 및 탐색 관리자 </summary>
        public MobManager MobManager => m_mobManager;

        /// <summary> 에디터 테스트용 무기 목록 </summary>
        public List<SkillData> TestWeapons => m_testWeapons;

        #endregion

        #region 유니티 생명주기

        private void Awake()
        {
            // [추가]: 씬 진입 시 타임스케일 초기화
            Time.timeScale = 1f;

            // 상태 관리자 및 시스템 초기화
            m_state = new PlayStateManager();
            m_mobManager = new MobManager();

            // 최적화 설정
            Application.targetFrameRate = 120;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            CacheComponents();
            SubscribeEvents();
        }

        /// <summary>
        /// [설명]: CompositionRoot로부터 전달된 데이터를 사용하여 게임 매니저를 초기화합니다.
        /// </summary>
        public async UniTask InitializeAsync(object payload)
        {
            if (m_isInitialized)
            {
                LogManager.Log("[GameManager] 이미 초기화되었습니다. 호출을 무시합니다.", LogManager.LogCategory.System);
                return;
            }

            if (payload is ScenePayloadDTO scenePayload)
            {
                m_playerData = scenePayload.PlayerData;
                m_soundManager = scenePayload.SoundService;
                m_sceneLoader = scenePayload.SceneLoader;
                m_popupService = scenePayload.PopupService;
                m_effectService = scenePayload.EffectService;
                m_remoteDataService = scenePayload.RemoteDataService;
                m_inventoryCtx = scenePayload.InventoryContext;

                if (scenePayload.ServerSession != null)
                {
                    m_serverSession = scenePayload.ServerSession;
                    m_gameDataService = m_serverSession.GameData;
                }
            }
            else if (payload is PlayerDataDTO dto)
            {
                m_playerData = dto;
            }

#if UNITY_EDITOR
            // [에디터 폴백]: 씬 직접 실행 시 RemoteDataUpdateManager 찾기
            if (m_remoteDataService == null)
            {
                m_remoteDataService = FindFirstObjectByType<InGame.Data.Managers.RemoteDataUpdateManager>();
                if (m_remoteDataService == null)
                {
                    GameObject go = new GameObject("[Editor_RemoteDataSync_Fallback]");
                    m_remoteDataService = go.AddComponent<InGame.Data.Managers.RemoteDataUpdateManager>();
                    LogManager.Log("[GameManager] 에디터 폴백: RemoteDataUpdateManager를 생성했습니다.", LogManager.LogCategory.System);
                }
            }
#endif

            if (m_playerData != null)
            {
                var encryptionService = new InGame.Services.EncryptionService();
                m_playerService = new InGame.Services.PlayerDataService(
                    m_playerData,
                    encryptionService,
                    new InGame.Data.LocalPlayerDataRepository(encryptionService),
                    m_gameDataService
                );
            }

            // [수정]: UIManager.Initialize는 CompositionRoot에서 이미 수행하므로 여기서 중복 호출하지 않습니다.
            // 다만, 의존성 조립이 필요한 경우를 대비해 캐싱만 확인합니다.
            if (m_uiManager == null) m_uiManager = FindFirstObjectByType<UIManager>();

            if (m_playerController != null)
            {
                m_playerController.Initialize(this, this);
                LogManager.Log("[GameManager] PlayerController 의존성 주입 완료", LogManager.LogCategory.System);
            }
            else
            {
                m_playerController = FindFirstObjectByType<PlayerController>();
                if (m_playerController != null) m_playerController.Initialize(this, this);
                LogManager.LogWarning($"[GameManager] PlayerController 캐싱 재시도 결과: {(m_playerController != null ? "성공" : "실패")}", LogManager.LogCategory.System);
            }

            // 1. 리모트 데이터 동기화
            LogManager.Log($"[GameManager] 씬 전체 초기화 및 리모트 동기화 대기 시작 (TimeScale: {Time.timeScale})", LogManager.LogCategory.System);
            await InitializeGameAsync();

            // 2. 플레이어 및 무기 생성 대기 (SceneLoader가 닫히기 전에 생성하여 로딩 화면 뒤에서 캐릭터가 미리 렌더링되도록 함)
            try
            {
                await SpawnPlayerAndInitialWeaponsAsync();
                LogManager.Log("[GameManager] 플레이어 및 초기 무기 스폰 완료", LogManager.LogCategory.System);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[GameManager] 플레이어 스폰 중 치명적 오류 발생: {ex.Message}", LogManager.LogCategory.System);
            }

            // 3. 페이드 아웃 완료 대기 및 카운트다운 시작 비동기 위임
            WaitAndStartCountdownAsync().Forget();

            LogManager.Log($"[GameManager] InitializeAsync 완료", LogManager.LogCategory.System);
            m_isInitialized = true;
        }

        private async UniTaskVoid WaitAndStartCountdownAsync()
        {
            // 4. 페이드 아웃 연출이 끝날 때까지 대기
            if (m_sceneLoader != null)
            {
                await m_sceneLoader.WaitUntilFadedOutAsync();
            }

            // 5. 화면이 밝아진 후 카운트다운 시작
            if (m_uiManager != null)
            {
                LogManager.Log("[GameManager] 카운트다운 시작 명령 전달", LogManager.LogCategory.System);
                m_uiManager.StartGameCountdown().Forget();
            }
            else
            {
                LogManager.LogWarning("[GameManager] UIManager를 찾을 수 없어 카운트다운을 생략하고 즉시 시작을 시도합니다.", LogManager.LogCategory.System);
                m_state.StartGame();
            }
        }

        private void Start()
        {
            if (m_settingsData != null)
            {
                m_settingsData.LoadSettings();
                Application.targetFrameRate = m_settingsData.TargetFrameRate;
                if (m_objectPoolSpawner != null)
                {
                    m_objectPoolSpawner.OnStageCleared += OnStageCleared;
                }
            }

            // 에디터 직접 실행 혹은 데이터가 없는 경우의 방어 로직
            if (m_playerData == null)
            {
                m_playerData = new PlayerDataDTO();
                m_playerService = new InGame.Services.PlayerDataService(m_playerData, new InGame.Services.EncryptionService(), new InGame.Data.LocalPlayerDataRepository(new InGame.Services.EncryptionService()));
#if UNITY_EDITOR
                m_playerData.SelectCharacterIndex = m_startCharacterIndex;
#endif
            }

            // 에디터 직접 실행이나 비정상적인 경로로 진입 시 서비스 방어 로직
            if (m_soundManager == null)
            {
                m_soundManager = FindFirstObjectByType<SoundManager>();
            }
            if (m_effectService == null)
            {
                m_effectService = FindFirstObjectByType<EffectManager>();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 하위 매니저 및 필요한 컴포넌트들을 캐싱합니다.
        /// </summary>
        private void CacheComponents()
        {
            m_objectPoolSpawner = FindFirstObjectByType<ObjectPoolSpawner>();
            m_playerController = FindFirstObjectByType<PlayerController>();
            m_variableJoystick = FindFirstObjectByType<VariableJoystick>();
            m_uiManager = FindFirstObjectByType<UIManager>();
            m_weaponPoolManager = FindFirstObjectByType<WeaponPoolManager>();
            m_playerHUD = FindFirstObjectByType<PlayerHUD>();
            m_playerCameraAgent = FindFirstObjectByType<PlayerCameraAgent>();
            m_mainCamera = Camera.main;

            if (m_variableJoystick != null)
            {
                m_variableJoystick.gameObject.SetActive(false); // [추가]: 조이스틱 초기 비활성화 (카운트다운 중 조작 방지)
            }

            LogManager.Log($"[GameManager] 컴포넌트 캐싱 결과 - Spawner: {m_objectPoolSpawner != null}, PC: {m_playerController != null}, JS: {m_variableJoystick != null}, UI: {m_uiManager != null}", LogManager.LogCategory.System);

            // [추가]: 캐싱 완료 후 보관된 초기 웨이브 일시정지 명령 적용
            if (m_initialWavePause && m_objectPoolSpawner != null)
            {
                m_objectPoolSpawner.SetWavePause(true);
            }
        }

        #endregion

        #region 초기화 로직

        /// <summary>
        /// [설명]: 게임 상태 변화에 따른 이벤트 구독을 수행합니다.
        /// </summary>
        private void SubscribeEvents()
        {
            if (m_state != null)
            {
                m_state.OnGameStart += OnGameStart;
                m_state.OnGamePause += OnPause;
                m_state.OnGameResume += OnResume;
                m_state.OnGameOver += OnGameOver;
            }
        }

        /// <summary>
        /// [설명]: 등록된 이벤트 구독을 해제합니다.
        /// </summary>
        private void UnsubscribeEvents()
        {
            if (m_state != null)
            {
                m_state.OnGameStart -= OnGameStart;
                m_state.OnGamePause -= OnPause;
                m_state.OnGameResume -= OnResume;
                m_state.OnGameOver -= OnGameOver;
            }

            if (m_objectPoolSpawner != null)
            {
                m_objectPoolSpawner.OnStageCleared -= OnStageCleared;
            }
        }


        /// <summary>
        /// [설명]: 비동기 방식으로 플레이어 생성 및 초기 게임 설정을 수행합니다.
        /// </summary>
        private async UniTask InitializeGameAsync()
        {
            try
            {
                if (m_remoteDataService != null)
                {
                    LogManager.Log("[GameManager] 리모트 데이터 동기화 시작...", LogManager.LogCategory.System);

                    // [복구]: 데이터베이스 참조 확보 (m_gameDataService 대신 실제 SO 전달)
                    var stageDatabase = Resources.Load<StageDatabase>("Data/StageDatabase");
                    if (stageDatabase == null)
                    {
                         LogManager.LogWarning("[GameManager] Resources에서 StageDatabase를 로드하지 못했습니다.", LogManager.LogCategory.System);
                    }

                    await m_remoteDataService.UpdateAllRemoteDataAsync(m_skillDatabase, stageDatabase, this.GetCancellationTokenOnDestroy(), force: false);
                    LogManager.Log("[GameManager] 리모트 데이터 동기화 완료 (Force=False)", LogManager.LogCategory.System);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[GameManager] 리모트 데이터 동기화 중 오류 발생: {ex.Message}. 로컬 데이터로 진행을 시도합니다.", LogManager.LogCategory.System);
            }

            // [수정] 플레이어 스폰 대기는 백그라운드(WaitAndStartCountdownAsync)로 이동했습니다.
        }

        #endregion

        #region 게임 흐름 제어

        private void OnGameStart()
        {
            OnGameStartAsync().Forget();
        }

        /// <summary>
        /// [설명]: 게임 시작 시 비동기 초기화 및 웨이브 시작을 수행합니다.
        /// 이벤트 핸들러와 분리되어 독립적으로 실행됩니다.
        /// </summary>
        private async UniTaskVoid OnGameStartAsync()
        {
            LogManager.Log($"[GameManager] OnGameStart 진입 (TimeScale: {Time.timeScale})", LogManager.LogCategory.System);

            try
            {
                // 게임 재개 상태 보장 (카운트다운 시 Pause 상태일 수 있음)
                Time.timeScale = 1f;

                if (m_variableJoystick != null)
                {
                    m_variableJoystick.gameObject.SetActive(true); // 카운트다운 완료 후 게임 시작 시 조이스틱 활성화
                }

                if (m_inventoryCtx != null)
                {
                    m_inventoryCtx.ClearInGameSkills();
                    LogManager.Log("[GameManager] 인게임 스킬 인벤토리 초기화 완료", LogManager.LogCategory.System);
                }

                m_playerData.NowPlayMobKillCount = 0; // Assuming m_killCount.Value refers to playerData.NowPlayMobKillCount

                if (m_soundManager != null)
                {
                    m_soundManager.Play("BGM_Ingame_Wave", Sound.BGM, loop: true);
                }

                if (SpawnedPlayer != null)
                {
                    if (m_objectPoolSpawner != null)
                    {
                        // [수정]: PlayerDataDTO에 LastClearedStageId가 없으므로 우선 1로 설정 (또는 추후 데이터 구조 확장 필요)
                        int targetStageId = 1;
                        LogManager.Log($"[GameManager] Spawner 초기화 시도 - Stage: {targetStageId}", LogManager.LogCategory.System);

                        await m_objectPoolSpawner.InitializeAndStartSpawning(
                            this,
                            m_mobManager,
                            m_playerData,
                            m_soundManager,
                            this,
                            this,
                            targetStageId);
                    }
                    else
                    {
                        LogManager.LogError("[GameManager] m_objectPoolSpawner가 null이라 웨이브를 시작할 수 없습니다!", LogManager.LogCategory.System);
                    }
                }
                else
                {
                    LogManager.LogError("[GameManager] SpawnedPlayer가 null이라 웨이브를 시작할 수 없습니다!", LogManager.LogCategory.System);
                }

                LogManager.Log("[GameManager] OnGameStart 실행 완료", LogManager.LogCategory.System);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[GameManager] OnGameStart 중 오류 발생: {ex.Message}\n{ex.StackTrace}", LogManager.LogCategory.System);
            }
        }

        private void OnPause()
        {
            Time.timeScale = 0f;
        }

        private void OnResume()
        {
            Time.timeScale = 1f;
        }

        private void OnStageCleared(int stageId)
        {
            LogManager.Log($"[GameManager] 스테이지 {stageId} 클리어!", LogManager.LogCategory.VamserLikeGameManager);

            m_isCleared = true;

            if (m_state != null)
            {
                m_state.GameOver();
            }
        }


        private void OnGameOver()
        {
            Time.timeScale = 0f;
            LogManager.Log("[GameManager] OnGameOver 진입 - TimeScale=0 설정 완료", LogManager.LogCategory.System);

            // [수정]: async void를 제거하고, 저장 로직을 fire-and-forget으로 분리.
            // UIManager의 OnGameOver 핸들러가 정상적으로 실행된 후에
            // 비동기 저장이 독립적으로 진행되도록 합니다.
            SaveAndCleanupAsync().Forget();
        }

        /// <summary>
        /// [설명]: 게임 결과를 저장하고 플레이어 참조를 해제하는 비동기 로직입니다.
        /// OnGameOver 이벤트 핸들러와 분리되어 독립적으로 실행됩니다.
        /// </summary>
        private async UniTaskVoid SaveAndCleanupAsync()
        {
            try
            {
                await SaveGameResult();
                LogManager.Log("[GameManager] SaveGameResult 완료", LogManager.LogCategory.System);
            }
            catch (Exception e)
            {
                LogManager.LogError($"[GameManager] SaveAndCleanupAsync 예외: {e.Message}", LogManager.LogCategory.System);
            }

            // 게임 결과 저장 후 플레이어 참조 해제
            SpawnedPlayer = null;
            OnPlayerChanged?.Invoke(null);
        }

        /// <summary>
        /// [설명]: 게임 결과를 저장하고 서버에 업로드합니다.
        /// </summary>
        public async UniTask SaveGameResult()
        {
            if (m_playerData == null || m_playerService == null)
            {
                return;
            }

            try
            {
                // 이번 판에서 획득한 코인 (인게임 HUD 등에서 이미 m_playerData.IngameCoin에 반영됨)
                int earnedCoin = m_playerData.IngameCoin;
                
                // 로컬 총합 갱신
                m_playerData.Currency1 += earnedCoin;
                m_playerData.IngameCoin = 0;

                // 1. 로컬 저장 (백업)
                await m_playerService.SaveLocalAsync();
                
                // [체크]: 서버 기능 사용 가능 여부 확인
                if (m_gameDataService == null)
                {
                    LogManager.LogWarning("[GameManager] 서버 세션이 유효하지 않아 로컬에만 저장하고 서버 업로드를 건너뜁니다.", LogManager.LogCategory.System);
                }
                else
                {
                    // 2. 서버에 로컬의 최종 절대값을 업로드하여 동기화합니다.
                    // (AddCalculation 방식 폐기: 데이터 유실 방지를 위해 절대값 덮어쓰기 사용)
                    await m_playerService.UploadToServerAsync(includeCurrency: true);
                }

                LogManager.Log($"[GameManager] 게임 결과 통합 저장 완료 (획득 골드: {earnedCoin})", LogManager.LogCategory.System);
            }
            catch (Exception e)
            {
                LogManager.LogError($"[GameManager] 결과 업로드 중 예외 발생: {e.Message}");
            }

            await UniTask.CompletedTask;
        }

        /// <summary>
        /// [설명]: 로비로 복귀 시 전달할 최신 상 데이터를 담은 페이로드를 생성합니다.
        /// </summary>
        public ScenePayloadDTO GetResultPayload()
        {
            var payload = new ScenePayloadDTO(m_playerData, m_serverSession != null ? m_serverSession : new ServerSessionDTO(null, m_gameDataService, null))
            {
                SoundService = m_soundManager,
                SceneLoader = m_sceneLoader,
                PopupService = m_popupService,
                EffectService = m_effectService,
                RemoteDataService = m_remoteDataService,
                InventoryContext = m_inventoryCtx,
                IsFirstLogin = false // 인게임 복귀이므로 false
            };
            return payload;
        }

        #endregion

        #region 플레이어 및 무기 관리

        /// <summary>
        /// [설명]: 캐릭터와 무기를 리셋하고 다시 생성합니다.
        /// </summary>
        public async UniTask ChangeCharacterAndWeapon_Spawn()
        {
            if (m_playerContainer == null)
            {
                return;
            }

            for (int i = m_playerContainer.transform.childCount - 1; i >= 0; i--)
            {
                GameObject childObj = m_playerContainer.transform.GetChild(i).gameObject;
                Addressables.ReleaseInstance(childObj);
            }

            SpawnedPlayer = null;
            OnPlayerChanged?.Invoke(null);

            await SpawnPlayerAndInitialWeaponsAsync();
        }

        /// <summary>
        /// [설명]: Addressables를 사용하여 플레이어 캐릭터를 스폰하고 초기 무기를 장착합니다.
        /// </summary>
        private async UniTask SpawnPlayerAndInitialWeaponsAsync()
        {
            if (m_playerContainer == null)
            {
                return;
            }

            try
            {
                int charIndex = m_playerData != null ? m_playerData.SelectCharacterIndex : 0;
                string charKey = $"Player_Character_{charIndex}";

                GameObject charInstance = await Addressables
                    .InstantiateAsync(charKey, k_SpawnPosition, Quaternion.identity, m_playerContainer.transform)
                    .ToUniTask();

                if (charInstance == null)
                {
                    return;
                }

                charInstance.transform.localPosition = Vector3.zero;
                SpawnedPlayer = charInstance.GetComponent<PlayerBase>();

                if (SpawnedPlayer == null)
                {
                    Addressables.ReleaseInstance(charInstance);
                    return;
                }

                // 플레이어 시스템 초기화 (DTO 서비스 및 사운드 매니저 주입)
                SpawnedPlayer.Init(
                    playerService: m_playerService,
                    soundManager: m_soundManager,
                    gameStateService: this);

                var initialWeapons = new List<SkillData>();
                if (m_skillDatabase != null)
                {
                    SkillData defaultWeaponSkill = m_skillDatabase.allSkills.FirstOrDefault(s => s.skillCode == "WP_BONE");
                    if (defaultWeaponSkill != null)
                    {
                        initialWeapons.Add(defaultWeaponSkill);
                    }
                }

#if UNITY_EDITOR
                initialWeapons.AddRange(m_testWeapons.Where(w => w != null));
#endif
                foreach (var weaponSkill in initialWeapons.Distinct())
                {
                    await EquipNewWeapon(weaponSkill, false);
                }

                if (m_playerController != null)
                {
                    m_playerController.AssignCharacter(SpawnedPlayer, m_mobManager, m_playerCameraAgent, m_playerHUD);
                }

                OnPlayerChanged?.Invoke(SpawnedPlayer);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[GameManager] 스폰 프로세스 오류: {ex.Message}");
                SpawnedPlayer = null;
            }
        }

        /// <summary>
        /// [설명]: 새로운 무기를 생성하여 플레이어에게 장착시킵니다.
        /// </summary>
        public async UniTask EquipNewWeapon(SkillData skillData, bool playEffect = true, int startLevel = 1, bool startEvolved = false)
        {
            await UniTask.Yield();

            if (SpawnedPlayer == null || skillData.skillType != SkillType.Weapon)
            {
                return;
            }

            if (skillData.weaponData != null && WeaponFactory.IsRegistered(skillData.skillCode))
            {
                var controller = WeaponFactory.CreateController(
                    skillData.weaponData,
                    SpawnedPlayer.transform,
                    m_weaponPoolManager,
                    () => m_playerController != null ? m_playerController.GetCalculatedAttackDirection() : Vector3.zero,
                    this, // IGameStateService
                    this, // ICombatContext
                    this  // IPlayerContext
                );

                if (controller != null)
                {
                    controller.SkillData = skillData;

                    for (int i = 1; i < startLevel; i++)
                    {
                        controller.LevelUp();
                    }

                    if (startEvolved)
                    {
                        while (controller.CurrentLevel < controller.MaxLevel)
                        {
                            controller.LevelUp();
                        }
                        controller.LevelUp();
                    }

                    SpawnedPlayer.AddController(controller);

                    // [추가]: 새로운 무기 획득 시 즉시 1회 공격을 수행하여 무기 활성화 확인
                    Vector3 attackDir = m_playerController != null ? m_playerController.GetCalculatedAttackDirection() : Vector3.right;
                    controller.Attack(attackDir);

                    if (playEffect)
                    {
                        var renderer = SpawnedPlayer.GetComponent<SpriteRenderer>();
                        if (m_effectService != null && renderer != null)
                        {
                            m_effectService.PlayLevelUpEffect(renderer);
                        }
                    }

                    LogManager.Log($"[GameManager] 무기 장착: {skillData.skillName}", LogManager.LogCategory.Weapon);
                }
            }
            else
            {
                LogManager.LogWarning($"[GameManager] 무기 생성 불가 (미등록 코드): {skillData.skillCode}");
            }
        }

        /// <summary>
        /// [설명]: 테스트 목적으로 장착된 무기를 제거합니다.
        /// </summary>
        public void RemoveWeaponForTest(string skillCode)
        {
            if (SpawnedPlayer != null)
            {
                SpawnedPlayer.RemoveWeapon(skillCode);
            }
        }

        #endregion

        #region UI 제어

        /// <summary>
        /// [설명]: 팝업 상태에 따른 게임 일시정지 상태를 제어합니다.
        /// </summary>
        public void SetMenuPopupState(bool isPause)
        {
            if (m_state == null)
            {
                return;
            }

            if (isPause)
            {
                m_state.Pause();
            }
            else
            {
                m_state.Resume();
            }
        }

        /// <summary>
        /// [설명]: 옵션 설정 팝업을 생성합니다.
        /// </summary>
        public void OpenOptionPopup()
        {
            if (m_optionPopupPrefab != null)
            {
                var popup = Instantiate(m_optionPopupPrefab, transform);
                popup.Initialize(m_soundManager, m_popupService);

                // ESC 키 등으로 닫힐 수 있도록 PopupManager에 등록
                if (m_popupService != null)
                {
                    m_popupService.RegisterPopup(() =>
                    {
                        if (popup != null)
                        {
                            Destroy(popup.gameObject);
                        }
                    });
                }
            }
        }

        #endregion

        #region 데이터 접근자

        /// <summary> [설명]: 현재 몬스터 처치 수를 반환합니다. </summary>
        public int GetMobKillCount()
        {
            if (m_playerData != null)
            {
                return m_playerData.NowPlayMobKillCount;
            }
            return 0;
        }

        /// <summary> [설명]: 현재 진행 중인 웨이브 번호를 반환합니다. </summary>
        public int GetCurrentWave()
        {
            return m_objectPoolSpawner != null ? m_objectPoolSpawner.CurrentWave : 0;
        }

        /// <summary> [설명]: 현재 스테이지 ID를 반환합니다. </summary>
        public int GetCurrentStageId()
        {
            return m_objectPoolSpawner != null ? m_objectPoolSpawner.CurrentStage : 0;
        }

        /// <summary> [설명]: 현재 플레이어의 레벨을 반환합니다. </summary>
        public float GetPlayerLevel()
        {
            return SpawnedPlayer != null ? SpawnedPlayer.Level : 1f;
        }

        /// <summary> [설명]: 현재 플레이어의 경험치 진행률(0~1)을 반환합니다. </summary>
        public float GetPlayerExpProgress()
        {
            return SpawnedPlayer != null ? SpawnedPlayer.GetExpProgress() : 0f;
        }

        /// <summary> [설명]: 현재 인게임에서 획득한 코인 수를 반환합니다. </summary>
        public int GetCoinCount()
        {
            if (m_playerData != null)
            {
                return m_playerData.IngameCoin;
            }
            return 0;
        }

        /// <summary> [설명]: 플레이어의 위치 정보를 가진 Transform을 반환합니다. </summary>
        public Transform PlayerTransfrom()
        {
            return m_playerContainer != null ? m_playerContainer.transform : transform;
        }
        #endregion

        #region 테스트 관련 기능

        /// <summary>
        /// [설명]: 테스트 목적으로 스테이지 클리어 상태를 강제로 발생시킵니다.
        /// </summary>
        public void ClearStageForTest()
        {
            LogManager.Log("[GameManager] 테스트 클리어 비즈니스 로직 실행", LogManager.LogCategory.VamserLikeGameManager);
            OnStageCleared(0);
        }

        /// <summary> [설명]: 웨이브 시스템을 일시 중단하거나 재개합니다. (테스트용) </summary>
        public void SetWaveSystemPause(bool pause)
        {
            m_initialWavePause = pause;

            if (m_objectPoolSpawner != null)
            {
                m_objectPoolSpawner.SetWavePause(pause);
            }
        }

        #endregion
    }
}
