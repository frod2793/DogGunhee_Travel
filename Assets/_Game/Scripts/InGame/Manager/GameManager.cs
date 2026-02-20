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
using InGame.Core;

namespace InGame.Managers
{
    /// <summary>
    /// [설명]: 게임의 전체 흐름(시작, 정지, 종료)과 전역 상태(플레이어, 스포너, UI)를 총괄하는 중앙 관리자 클래스입니다.
    /// 외부 시스템과 연동하는 PlayerDataDTO를 주입받아 초기화됩니다.
    /// </summary>
    public class GameManager : MonoBehaviour, ISceneInitializer
    {
        #region 싱글톤 및 이벤트

        private static GameManager s_instance;

        public static GameManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindFirstObjectByType<GameManager>();
                }

                return s_instance;
            }
        }

        /// <summary> 플레이어 캐릭터가 스폰되거나 변경될 때 발생하는 전역 이벤트 </summary>
        public static event Action<PlayerBase> OnPlayerChanged;

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
        private ISoundManager m_soundManager;

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

        /// <summary> 현재 맵의 이동 가능 경계 </summary>
        public Bounds MapBounds => m_mapRange != null ? m_mapRange.bounds : new Bounds(Vector3.zero, Vector3.one * 100);

        /// <summary> 현재 활성화된 몬스터 수 </summary>
        public int ActiveMobCount => m_objectPoolSpawner != null ? m_objectPoolSpawner.ActiveMobCount : 0;

        /// <summary> 몬스터 타겟팅 및 탐색 관리자 </summary>
        public MobManager MobManager => m_mobManager;

        /// <summary> 에디터 테스트용 무기 목록 </summary>
        public List<SkillData> TestWeapons => m_testWeapons;

        #endregion

        #region 유니티 생명주기

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;

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
        /// [설명]: SceneLoader로부터 전달된 데이터를 사용하여 게임 매니저를 초기화합니다.
        /// </summary>
        public async UniTask OnInitialize(object payload)
        {
            if (payload is ScenePayloadDTO scenePayload)
            {
                m_playerData = scenePayload.PlayerData;
                m_soundManager = scenePayload.SoundService;

                if (scenePayload.ServerSession != null)
                {
                    m_gameDataService = scenePayload.ServerSession.GameData;
                }

                if (m_uiManager != null)
                {
                    m_uiManager.Initialize(m_soundManager);
                }
            }
            else if (payload is PlayerDataDTO dto)
            {
                m_playerData = dto;
            }

            if (m_playerData != null)
            {
                m_playerService = new InGame.Services.PlayerDataService(
                    m_playerData, 
                    new InGame.Services.EncryptionService(), 
                    new InGame.Data.LocalPlayerDataRepository(new InGame.Services.EncryptionService())
                );
            }

            // [추가] 씬 전체 초기화 대기 (리모트 데이터 동기화 포함)
            await InitializeGameAsync();
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

            // 에디터 직접 실행이나 비정상적인 경로로 진입 시 SoundManager 방어 로직
            if (m_soundManager == null)
            {
                m_soundManager = FindFirstObjectByType<SoundManager>();
                if (m_uiManager != null && m_soundManager != null)
                {
                    m_uiManager.Initialize(m_soundManager);
                }
            }
        }

        private void OnEnable()
        {
            InitializeGameAsync().Forget();
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
            // [추가] 인게임 진입 직후 가장 먼저 리모트 데이터(구글 시트) 동기화 대기
            if (InGame.Data.Managers.RemoteDataUpdateManager.Instance != null)
            {
                var stageDatabase = Resources.Load<StageDatabase>("Data/StageDatabase");
                await InGame.Data.Managers.RemoteDataUpdateManager.Instance.UpdateAllRemoteDataAsync(m_skillDatabase, stageDatabase, this.GetCancellationTokenOnDestroy());
            }

            await SpawnPlayerAndInitialWeaponsAsync();

            if (m_uiManager != null)
            {
                m_uiManager.StartGameCountdown().Forget();
            }
        }

        #endregion

        #region 게임 흐름 제어

        private async void OnGameStart()
        {
            try
            {
                // [변경] InventoryDataManager -> InventoryManager
                if (InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.ClearInGameSkills();
                }

                if (m_playerData != null)
                {
                    m_playerData.NowPlayMobKillCount = 0;
                }

                // BGM 재생 로직 (DI 사용)
                if (m_soundManager != null)
                {
                    m_soundManager.Play(SoundKeys.InGame.ToString(), Sound.BGM, loop: true);
                }

                if (SpawnedPlayer != null)
                {
                    if (m_objectPoolSpawner != null)
                    {
                        await m_objectPoolSpawner.InitializeAndStartSpawning(SpawnedPlayer, m_mobManager, m_playerData, m_soundManager, 1);
                    }
                }
            }
            catch (Exception e)
            {
                LogManager.LogError($"[GameManager] 게임 시작 실패: {e.Message}");
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

        /// <summary>
        /// [설명]: 테스트 목적으로 스테이지 클리어 상태를 강제로 발생시킵니다.
        /// </summary>
        public void ClearStageForTest()
        {
            LogManager.Log("[GameManager] 테스트 클리어 비즈니스 로직 실행", LogManager.LogCategory.VamserLikeGameManager);
            OnStageCleared(0);
        }

        private async void OnGameOver()
        {
            Time.timeScale = 0f;

            await SaveGameResult();

            // 게임 결과 저장 후 플레이어 참조 해제
            SpawnedPlayer = null;
            OnPlayerChanged?.Invoke(null);
        }

        /// <summary>
        /// [설명]: 게임 결과를 저장하고 서버에 업로드합니다.
        /// </summary>
        public async UniTask SaveGameResult()
        {
            if (m_playerData == null)
            {
                return;
            }

            var playerData = m_playerData;

            playerData.Currency1 += playerData.IngameCoin;
            playerData.IngameCoin = 0;

            await UniTask.SwitchToMainThread();

            var param = new BackEnd.Param();
            param.Add("Money1", playerData.Currency1);

            try
            {
                if (m_gameDataService != null)
                {
                    await m_gameDataService.UploadDataAsync("User_Data", param);
                }
            }
            catch (Exception e)
            {
                LogManager.LogError($"[GameManager] 결과 업로드 실패: {e.Message}");
            }
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
                SpawnedPlayer.Init(playerService: m_playerService, soundManager: m_soundManager);

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
                    () => m_playerController != null ? m_playerController.GetCalculatedAttackDirection() : Vector3.zero
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

                    if (playEffect)
                    {
                        var renderer = SpawnedPlayer.GetComponent<SpriteRenderer>();
                        if (EffectManager.Instance != null && renderer != null)
                        {
                            EffectManager.Instance.PlayLevelUpEffect(renderer);
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
                popup.Initialize(m_soundManager);

                // ESC 키 등으로 닫힐 수 있도록 PopupManager에 등록
                InGame.UI.PopupManager.Instance.RegisterPopup(() =>
                {
                    if (popup != null)
                    {
                        Destroy(popup.gameObject);
                    }
                });
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
    }
}