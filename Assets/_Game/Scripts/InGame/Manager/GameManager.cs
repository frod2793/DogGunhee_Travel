using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using InGame.Lobby;
using InGame.Player.Player_Base;
using InGame.Weapon.Base;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Lobby;
using InGame.Weapon;

namespace InGame.Manager
{
    /// <summary>
    /// 게임의 전체적인 흐름과 전역 상태를 관리하는 중앙 관리자 클래스입니다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region 정적 멤버 및 싱글톤

        /// <summary>플레이어 캐릭터가 변경될 때 발생하는 이벤트입니다.</summary>
        public static event Action<PlayerBase> OnPlayerChanged;

        private static GameManager s_instance;

        /// <summary>GameManager의 전역 싱글톤 인스턴스입니다.</summary>
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

        #endregion

        #region 인스펙터 필드

        [Header("에디터 시작 설정 (Editor Only)")]
        [SerializeField] private int m_startCharacterIndex = 0;

        [Header("데이터 참조")]
        [SerializeField] private SkillDatabase m_skillDatabase;
        [SerializeField] private SettingsData m_settingsData;

        [Header("참조 설정")]
        [SerializeField] private GameObject m_playerContainer;
        [SerializeField] private SpriteRenderer m_mapRange;
        [SerializeField] private OptionPopupView m_optionPopupPrefab;

        [Header("디버그")]
        public List<SkillData> TestWeapons = new List<SkillData>();

        #endregion

        #region 내부 캐시 및 상태 변수

        private ObjectPoolSpawner m_objectPoolSpawner;
        private PlayerController m_playerController;
        private VariableJoystick m_variableJoystick;
        private Camera m_mainCamera;
        private UIManager m_uiManager;
        private PlayStateManager m_state;

        private static readonly Vector3 k_SpawnPosition = Vector3.zero;

        #endregion

        #region 프로퍼티 및 상태

        /// <summary>현재 맵에 스폰된 플레이어 캐릭터입니다.</summary>
        public PlayerBase SpawnedPlayer { get; private set; }

        /// <summary>오브젝트 풀 및 스폰 시스템 참조입니다.</summary>
        public ObjectPoolSpawner ObjectPoolSpawner => m_objectPoolSpawner;

        /// <summary>플레이어 입력 및 상태 제어 컨트롤러입니다.</summary>
        public PlayerController PlayerController => m_playerController;

        /// <summary>게임의 전역 상태 관리 로직입니다.</summary>
        public PlayStateManager State => m_state;

        /// <summary>이동 조이스틱 참조입니다.</summary>
        public VariableJoystick Joystick => m_variableJoystick;

        /// <summary>메인 카메라 참조입니다.</summary>
        public Camera MainCamera => m_mainCamera;

        /// <summary>UI 시스템 관리자 참조입니다.</summary>
        public UIManager UIManagerManager => m_uiManager;

        /// <summary>현재 맵의 경계 범위를 반환합니다.</summary>
        public Bounds MapBounds => m_mapRange != null ? m_mapRange.bounds : new Bounds(Vector3.zero, Vector3.one * 100);

        /// <summary>현재 활성화된 몹의 수를 반환합니다.</summary>
        public int ActiveMobCount => m_objectPoolSpawner != null ? m_objectPoolSpawner.ActiveMobCount : 0;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;

            // 게임 상태 관리자 초기화 (POCO)
            m_state = new PlayStateManager();

            // 안드로이드 및 모바일 환경 최적화 설정
            Application.targetFrameRate = 120;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            CacheComponents();
            SubscribeEvents();
        }

        private void Start()
        {
            // 설정 로드 및 프레임 적용
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

        private async void OnEnable()
        {
            await SpawnPlayerAndInitialWeaponsAsync();

            if (m_uiManager != null)
            {
                m_uiManager.StartGameCountdown();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        #endregion

        #region 초기화 및 이벤트 관리

        private void CacheComponents()
        {
            m_objectPoolSpawner = FindFirstObjectByType<ObjectPoolSpawner>();
            m_playerController = FindFirstObjectByType<PlayerController>();
            m_variableJoystick = FindFirstObjectByType<VariableJoystick>();
            m_uiManager = FindFirstObjectByType<UIManager>();
            m_mainCamera = Camera.main;
        }

        private void SubscribeEvents()
        {
            if (m_state == null) return;
            m_state.OnGameStart += OnGameStart;
            m_state.OnGamePause += OnPause;
            m_state.OnGameResume += OnResume;
            m_state.OnGameOver += OnGameOver;
        }

        private void UnsubscribeEvents()
        {
            if (m_state == null) return;
            m_state.OnGameStart -= OnGameStart;
            m_state.OnGamePause -= OnPause;
            m_state.OnGameResume -= OnResume;
            m_state.OnGameOver -= OnGameOver;
        }

        #endregion

        #region 게임 상태 제어

        private async void OnGameStart()
        {
            try
            {
                // 인게임 스택 초기화
                if (InventoryDataManager.Instance != null)
                {
                    InventoryDataManager.Instance.ClearInGameSkills();
                }

                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.PlayerData.nowPlayMObkillCOunt = 0;
                }

                SoundManager.PlaySound(Sound.BGM, SoundKeys.InGame, true);

                // 스포너 시작
                if (SpawnedPlayer != null && m_objectPoolSpawner != null)
                {
                    await m_objectPoolSpawner.InitializeAndStartSpawning(SpawnedPlayer);
                }
            }
            catch (Exception e)
            {
                LogManager.LogError($"[게임 매니저] 게임 시작 중 오류 발생: {e.Message}");
            }
        }

        private void OnPause()
        {
            // 일시 정지 시 추가 처리가 필요한 경우 여기에 구현
        }

        private void OnResume()
        {
            // 재개 시 추가 처리가 필요한 경우 여기에 구현
        }

        private async void OnGameOver()
        {
            SpawnedPlayer = null;
            OnPlayerChanged?.Invoke(null);

            // 결과 데이터 처리 및 서버 업로드
            var dataManager = PlayerDataManager.Instance;
            if (dataManager != null && dataManager.PlayerData != null)
            {
                var playerData = dataManager.PlayerData;
                playerData.currency1 += playerData.ingameCoin;
                playerData.ingameCoin = 0;

                await UniTask.SwitchToMainThread();

                var param = new BackEnd.Param();
                param.Add("Money1", playerData.currency1);

                try
                {
                    await ServerManager.Instance.UploadDataAsync("User_Data", param);
                }
                catch (Exception e)
                {
                    LogManager.LogError($"[게임 매니저] 데이터 업로드 실패: {e.Message}");
                }
            }
        }

        #endregion

        #region UI 및 팝업 상태 제어

        /// <summary>
        /// 메뉴 팝업 노출 상태에 따라 게임의 일시정지 여부를 설정합니다.
        /// </summary>
        public void SetMenuPopupState(bool isPause)
        {
            if (m_state == null) return;
            if (isPause) m_state.Pause();
            else m_state.Resume();
        }

        /// <summary>
        /// 옵션 팝업을 생성하고 노출합니다.
        /// </summary>
        public void OpenOptionPopup()
        {
            if (m_optionPopupPrefab == null) return;
            var popup = Instantiate(m_optionPopupPrefab, transform);
            popup.gameObject.SetActive(true);
        }

        #endregion

        #region 플레이어 및 무기 관리

        /// <summary>
        /// 캐릭터와 무기를 새로 고침하여 다시 스폰합니다. (테스트 및 캐릭터 변경용)
        /// </summary>
        public async UniTask ChangeCharacterAndWeapon_Spawn()
        {
            if (m_playerContainer == null) return;

            // 기존 캐릭터 제거
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
                // 어드레서블을 통한 플레이어 에셋 로드 및 생성
                int charIndex = PlayerDataManager.Instance.SelectCharacterIndex;
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

                // 초기 무기 설정
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
                // 무기 순차 장착
                foreach (var weaponSkill in initialWeapons.Distinct())
                {
                    await EquipNewWeapon(weaponSkill, false);
                }

                // 컨트롤러 연결
                if (m_playerController != null)
                {
                    m_playerController.AssignCharacter(SpawnedPlayer);
                }

                OnPlayerChanged?.Invoke(SpawnedPlayer);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[게임 매니저] 플레이어 스폰 과정 오류: {ex.Message}");
                SpawnedPlayer = null;
            }
        }

        /// <summary>
        /// 새로운 무기를 장착합니다.
        /// </summary>
        /// <param name="skillData">장착할 무기 스킬 데이터</param>
        /// <param name="playEffect">이펙트 재생 여부</param>
        /// <param name="startLevel">시작 레벨</param>
        /// <param name="startEvolved">진화 상태 시작 여부</param>
        public async UniTask EquipNewWeapon(SkillData skillData, bool playEffect = true, int startLevel = 1, bool startEvolved = false)
        {
            await UniTask.Yield();

            if (SpawnedPlayer == null || skillData.skillType != SkillType.Weapon) return;

            // 순수 C# 로직 기반 무기 생성 (WeaponFactory 사용)
            if (skillData.weaponData != null && WeaponFactory.IsRegistered(skillData.skillCode))
            {
                var controller = WeaponFactory.CreateController(skillData.weaponData, SpawnedPlayer.transform, () => m_playerController.GetCalculatedAttackDirection());
                
                if (controller != null)
                {
                    controller.SkillData = skillData;

                    // 레벨 업 처리
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
                        controller.LevelUp(); // 진화 시도
                    }

                    SpawnedPlayer.AddController(controller);

                    if (playEffect)
                    {
                        EffectManager.Instance.PlayLevelUpEffect(SpawnedPlayer.GetComponent<SpriteRenderer>());
                    }
                    
                    LogManager.Log($"[게임 매니저] 무기 장착 완료: {skillData.skillName}", LogManager.LogCategory.Weapon);
                }
            }
            else
            {
                LogManager.LogWarning($"[게임 매니저] 무기 생성 불가: {skillData.skillName}");
            }
        }

        /// <summary>
        /// 테스트를 위해 특정 무기를 제거합니다.
        /// </summary>
        public void RemoveWeaponForTest(string skillCode)
        {
            if (SpawnedPlayer != null)
            {
                SpawnedPlayer.RemoveWeapon(skillCode);
            }
        }

        #endregion

        #region 데이터 접근자

        /// <summary>현재 웨이브의 누적 적 처치 수를 반환합니다.</summary>
        public int GetMobKillCount()
        {
            if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.PlayerData != null)
            {
                return PlayerDataManager.Instance.PlayerData.nowPlayMObkillCOunt;
            }
            return 0;
        }

        /// <summary>현재 진행 중인 웨이브 번호를 반환합니다.</summary>
        public int GetCurrentWave()
        {
            return m_objectPoolSpawner != null ? m_objectPoolSpawner.CurrentWave : 0;
        }

        /// <summary>현재 플레이어의 레벨을 반환합니다.</summary>
        public float GetPlayerLevel()
        {
            return SpawnedPlayer != null ? SpawnedPlayer.Level : 1f;
        }

        /// <summary>현재 플레이어의 경험치 진행도(0-100)를 반환합니다.</summary>
        public float GetPlayerExpProgress()
        {
            return SpawnedPlayer != null ? SpawnedPlayer.GetExpProgress() : 0f;
        }

        /// <summary>현재까지 획득한 골드 수를 반환합니다.</summary>
        public int GetCoinCount()
        {
            if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.PlayerData != null)
            {
                return PlayerDataManager.Instance.PlayerData.ingameCoin;
            }
            return 0;
        }

        /// <summary>플레이어의 트랜스폼 참조를 반환합니다.</summary>
        public Transform PlayerTransfrom()
        {
            return m_playerContainer != null ? m_playerContainer.transform : transform;
        }

        #endregion
    }
}