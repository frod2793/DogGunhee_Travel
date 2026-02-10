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

namespace InGame.Manager
{
    /// <summary>
    /// 게임의 전체 흐름(시작, 정지, 종료)과 전역 상태(플레이어, 스포너, UI)를 총괄하는 중앙 관리자 클래스입니다.
    /// <br/> 싱글톤으로 구현되어 있으며, 외부 시스템(Backend)과의 데이터 동기화도 담당합니다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region 1. 싱글톤 및 정적 이벤트

        private static GameManager s_instance;

        /// <summary>
        /// GameManager의 전역 싱글톤 인스턴스입니다.
        /// </summary>
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

        /// <summary>
        /// 플레이어 캐릭터가 스폰되거나 변경될 때 발생하는 전역 이벤트입니다.
        /// </summary>
        public static event Action<PlayerBase> OnPlayerChanged;

        #endregion

        #region 2. 에디터 설정 (Inspector)

        [Header("에디터 설정")] 
        [SerializeField, Tooltip("에디터에서 바로 시작할 때 사용할 캐릭터 인덱스")]
        private int m_startCharacterIndex;
        
        [Tooltip("테스트용 무기 목록")] 
        public List<SkillData> TestWeapons = new List<SkillData>();

        [Header("데이터 참조")] 
        [SerializeField, Tooltip("전체 스킬 데이터베이스")] 
        private SkillDatabase m_skillDatabase;
        
        [SerializeField, Tooltip("게임 설정 데이터 (프레임 등)")] 
        private SettingsData m_settingsData;

        [Header("인게임 참조")] 
        [SerializeField, Tooltip("플레이어가 생성될 부모 컨테이너")] 
        private GameObject m_playerContainer;
        
        [SerializeField, Tooltip("맵 경계를 정의하는 스프라이트")] 
        private SpriteRenderer m_mapRange;
        
        [SerializeField, Tooltip("옵션 팝업 프리팹")] 
        private OptionPopupView m_optionPopupPrefab;

        #endregion

        #region 3. 내부 상태 및 캐시

        // 하위 매니저 및 시스템 캐시
        private ObjectPoolSpawner m_objectPoolSpawner;
        private PlayerController m_playerController;
        private VariableJoystick m_variableJoystick;
        private Camera m_mainCamera;
        private UIManager m_uiManager;
        private PlayStateManager m_state;
        private WeaponPoolManager m_weaponPoolManager;

        // 상수
        private static readonly Vector3 k_SpawnPosition = Vector3.zero;

        #endregion

        #region 4. 공개 프로퍼티 (Accessors)

        /// <summary>현재 맵에 스폰된 플레이어 캐릭터입니다.</summary>
        public PlayerBase SpawnedPlayer { get; private set; }

        /// <summary>오브젝트 풀 및 몬스터 스폰 시스템입니다.</summary>
        public ObjectPoolSpawner ObjectPoolSpawner => m_objectPoolSpawner;

        /// <summary>플레이어 입력 컨트롤러입니다.</summary>
        public PlayerController PlayerController => m_playerController;

        /// <summary>게임의 상태(시작, 정지, 종료) 관리자입니다.</summary>
        public PlayStateManager State => m_state;

        /// <summary>가상 조이스틱 참조입니다.</summary>
        public VariableJoystick Joystick => m_variableJoystick;

        /// <summary>메인 카메라 참조입니다.</summary>
        public Camera MainCamera => m_mainCamera;

        /// <summary>UI 매니저 참조입니다.</summary>
        public UIManager UIManager => m_uiManager;

        /// <summary>현재 맵의 이동 가능 경계입니다.</summary>
        public Bounds MapBounds => m_mapRange != null ? m_mapRange.bounds : new Bounds(Vector3.zero, Vector3.one * 100);

        /// <summary>현재 활성화된 몬스터 수입니다.</summary>
        public int ActiveMobCount => m_objectPoolSpawner != null ? m_objectPoolSpawner.ActiveMobCount : 0;

        #endregion

        #region 5. 유니티 생명주기

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_instance = this;

            // 상태 관리자 초기화 (POCO)
            m_state = new PlayStateManager();

            // 모바일 환경 최적화
            Application.targetFrameRate = 120;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            CacheComponents();
            SubscribeEvents();
        }

        private void Start()
        {
            // 설정 적용
            if (m_settingsData != null)
            {
                m_settingsData.LoadSettings();
                Application.targetFrameRate = m_settingsData.TargetFrameRate;
            }

#if UNITY_EDITOR
            if (Application.isPlaying && PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.SelectCharacterIndex = m_startCharacterIndex;
            }
#endif
        }

        private void OnEnable()
        {
            // 비동기 초기화 실행 (Fire-and-Forget)
            InitializeGameAsync().Forget();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        #endregion

        #region 6. 초기화 및 이벤트 연결

        private void CacheComponents()
        {
            m_objectPoolSpawner = FindFirstObjectByType<ObjectPoolSpawner>();
            m_playerController = FindFirstObjectByType<PlayerController>();
            m_variableJoystick = FindFirstObjectByType<VariableJoystick>();
            m_uiManager = FindFirstObjectByType<UIManager>();
            m_weaponPoolManager = FindFirstObjectByType<WeaponPoolManager>();
            m_mainCamera = Camera.main;
        }

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

        private void UnsubscribeEvents()
        {
            if (m_state != null)
            {
                m_state.OnGameStart -= OnGameStart;
                m_state.OnGamePause -= OnPause;
                m_state.OnGameResume -= OnResume;
                m_state.OnGameOver -= OnGameOver;
            }
        }

        private async UniTaskVoid InitializeGameAsync()
        {
            await SpawnPlayerAndInitialWeaponsAsync();

            if (m_uiManager != null)
            {
                m_uiManager.StartGameCountdown();
            }
        }

        #endregion

        #region 7. 게임 흐름 제어 (Game Flow)

        private async void OnGameStart()
        {
            try
            {
                // 1. 데이터 초기화
                if (InventoryDataManager.Instance != null)
                {
                    InventoryDataManager.Instance.ClearInGameSkills();
                }

                if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.PlayerData != null)
                {
                    PlayerDataManager.Instance.PlayerData.nowPlayMObkillCOunt = 0;
                }

                // 2. BGM 재생
                SoundManager.PlaySound(Sound.BGM, SoundKeys.InGame, true);

                // 3. 스포너 시작
                if (SpawnedPlayer != null && m_objectPoolSpawner != null)
                {
                    // 기본 스테이지 1로 시작
                    await m_objectPoolSpawner.InitializeAndStartSpawning(SpawnedPlayer, 1);
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

        private async void OnGameOver()
        {
            Time.timeScale = 0f;

            SpawnedPlayer = null;
            OnPlayerChanged?.Invoke(null);

            // 결과 저장 및 서버 업로드
            await SaveGameResult();
        }

        /// <summary>
        /// 게임 결과를 저장하고 서버에 업로드합니다.
        /// </summary>
        public async UniTask SaveGameResult()
        {
            var dataManager = PlayerDataManager.Instance;
            if (dataManager == null || dataManager.PlayerData == null) return;

            var playerData = dataManager.PlayerData;

            // 획득 재화가 없으면 업로드 생략
            if (playerData.ingameCoin <= 0) return;

            // 로컬 데이터 갱신
            playerData.currency1 += playerData.ingameCoin;
            playerData.ingameCoin = 0;

            await UniTask.SwitchToMainThread();

            // 서버 업로드 파라미터 구성
            var param = new BackEnd.Param();
            param.Add("Money1", playerData.currency1);

            try
            {
                await ServerManager.Instance.UploadDataAsync("User_Data", param);
            }
            catch (Exception e)
            {
                LogManager.LogError($"[GameManager] 결과 업로드 실패: {e.Message}");
            }
        }

        #endregion

        #region 8. 플레이어 및 무기 관리 (Spawning)

        /// <summary>
        /// 캐릭터와 무기를 리셋하고 다시 스폰합니다. (테스트/변경용)
        /// </summary>
        public async UniTask ChangeCharacterAndWeapon_Spawn()
        {
            if (m_playerContainer == null) return;

            // 기존 캐릭터 제거 (Addressables Release)
            for (int i = m_playerContainer.transform.childCount - 1; i >= 0; i--)
            {
                GameObject childObj = m_playerContainer.transform.GetChild(i).gameObject;
                Addressables.ReleaseInstance(childObj);
            }

            SpawnedPlayer = null;
            OnPlayerChanged?.Invoke(null);

            await SpawnPlayerAndInitialWeaponsAsync();
        }

        private async UniTask SpawnPlayerAndInitialWeaponsAsync()
        {
            if (m_playerContainer == null) return;

            try
            {
                // 1. 캐릭터 생성
                int charIndex = PlayerDataManager.Instance != null ? PlayerDataManager.Instance.SelectCharacterIndex : 0;
                string charKey = $"Player_Character_{charIndex}";
                
                GameObject charInstance = await Addressables
                    .InstantiateAsync(charKey, k_SpawnPosition, Quaternion.identity, m_playerContainer.transform)
                    .ToUniTask();

                if (charInstance == null) return;

                charInstance.transform.localPosition = Vector3.zero;
                SpawnedPlayer = charInstance.GetComponent<PlayerBase>();

                if (SpawnedPlayer == null)
                {
                    Addressables.ReleaseInstance(charInstance);
                    return;
                }

                // 2. 초기 무기 목록 구성
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
                initialWeapons.AddRange(TestWeapons.Where(w => w != null));
#endif
                // 3. 무기 장착 (중복 제거)
                foreach (var weaponSkill in initialWeapons.Distinct())
                {
                    await EquipNewWeapon(weaponSkill, false);
                }

                // 4. 컨트롤러 연결
                if (m_playerController != null)
                {
                    m_playerController.AssignCharacter(SpawnedPlayer);
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
        /// 새로운 무기를 플레이어에게 장착시킵니다.
        /// </summary>
        public async UniTask EquipNewWeapon(SkillData skillData, bool playEffect = true, int startLevel = 1, bool startEvolved = false)
        {
            await UniTask.Yield();

            if (SpawnedPlayer == null || skillData.skillType != SkillType.Weapon) return;

            // WeaponFactory를 통한 컨트롤러 생성
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

                    // 레벨 업 시뮬레이션
                    for (int i = 1; i < startLevel; i++)
                    {
                        controller.LevelUp();
                    }

                    // 진화 상태 적용
                    if (startEvolved)
                    {
                        while (controller.CurrentLevel < controller.MaxLevel)
                        {
                            controller.LevelUp();
                        }
                        controller.LevelUp(); // 진화
                    }

                    SpawnedPlayer.AddController(controller);

                    // 이펙트 재생
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

        public void RemoveWeaponForTest(string skillCode)
        {
            if (SpawnedPlayer != null)
            {
                SpawnedPlayer.RemoveWeapon(skillCode);
            }
        }

        #endregion

        #region 9. UI 제어 (UI Control)

        /// <summary>
        /// 메뉴 팝업 표시에 따른 게임 일시정지 상태를 제어합니다.
        /// </summary>
        public void SetMenuPopupState(bool isPause)
        {
            if (m_state == null) return;

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
        /// 옵션 팝업을 생성합니다.
        /// </summary>
        public void OpenOptionPopup()
        {
            if (m_optionPopupPrefab != null)
            {
                var popup = Instantiate(m_optionPopupPrefab, transform);
                popup.gameObject.SetActive(true);
            }
        }

        #endregion

        #region 10. 데이터 접근자 (Data Accessors)

        public int GetMobKillCount()
        {
            if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.PlayerData != null)
            {
                return PlayerDataManager.Instance.PlayerData.nowPlayMObkillCOunt;
            }
            return 0;
        }

        public int GetCurrentWave()
        {
            return m_objectPoolSpawner != null ? m_objectPoolSpawner.CurrentWave : 0;
        }

        public int GetCurrentStageId()
        {
            return m_objectPoolSpawner != null ? m_objectPoolSpawner.CurrentStage : 0;
        }

        public float GetPlayerLevel()
        {
            return SpawnedPlayer != null ? SpawnedPlayer.Level : 1f;
        }

        public float GetPlayerExpProgress()
        {
            return SpawnedPlayer != null ? SpawnedPlayer.GetExpProgress() : 0f;
        }

        public int GetCoinCount()
        {
            if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.PlayerData != null)
            {
                return PlayerDataManager.Instance.PlayerData.ingameCoin;
            }
            return 0;
        }

        public Transform PlayerTransfrom()
        {
            return m_playerContainer != null ? m_playerContainer.transform : transform;
        }

        #endregion
    }
}