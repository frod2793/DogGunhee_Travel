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
    public class GameManager : MonoBehaviour
    {
        #region 정적 멤버 및 이벤트

        public static event Action<PlayerBase> OnPlayerChanged;

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

        #endregion

        #region 인스펙터 필드

        [Header("에디터 시작 설정 (Editor Only)")] [SerializeField]
        private int m_startCharacterIndex = 0;

        [Header("데이터 참조")] [SerializeField]
        private SkillDatabase m_skillDatabase;
        [SerializeField] private SettingsData m_settingsData;

        [Header("참조 설정")] [SerializeField]
        private GameObject m_playerContainer;
        [SerializeField] private SpriteRenderer m_mapRange; // 맵 범위 스프라이트 추가
        [SerializeField] private OptionPopupView m_optionPopupPrefab;

        [Header("디버그")] public List<SkillData> TestWeapons = new List<SkillData>();

        #endregion

        #region 내부 캐시 및 상태 변수

        private ObjectPoolSpawner m_objectPoolSpawner;
        private PlayerControll m_playerController;
        private VariableJoystick m_variableJoystick;
        private Camera m_mainCamera;
        private UIManager _mUIManagerManager;

        public PlayerBase SpawnedPlayer { get; private set; }
        public ObjectPoolSpawner ObjectPoolSpawner => m_objectPoolSpawner;
        public PlayerControll PlayerController => m_playerController;
        public VariableJoystick Joystick => m_variableJoystick;
        public Camera MainCamera => m_mainCamera;
        public UIManager UIManagerManager => _mUIManagerManager;
        public Bounds MapBounds => m_mapRange.bounds; // 맵 경계 반환 프로퍼티 추가

        private static readonly Vector3 k_SpawnPosition = Vector3.zero;

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

            // 안드로이드 및 모바일 환경 최적화 설정
#if UNITY_ANDROID
            Application.targetFrameRate = 120;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
#else
            Application.targetFrameRate = 120;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
#endif

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

            if (_mUIManagerManager != null)
            {
                _mUIManagerManager.StartGameCountdown();
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
            m_playerController = FindFirstObjectByType<PlayerControll>();
            m_variableJoystick = FindFirstObjectByType<VariableJoystick>();
            _mUIManagerManager = FindFirstObjectByType<UIManager>();
            m_mainCamera = Camera.main;
        }

        private void SubscribeEvents()
        {
            PlayStateManager.OnGameStart += OnGameStart;
            PlayStateManager.OnGamePause += OnPause;
            PlayStateManager.OnGameResume += OnResume;
            PlayStateManager.OnGameOver += OnGameOver;
        }

        private void UnsubscribeEvents()
        {
            PlayStateManager.OnGameStart -= OnGameStart;
            PlayStateManager.OnGamePause -= OnPause;
            PlayStateManager.OnGameResume -= OnResume;
            PlayStateManager.OnGameOver -= OnGameOver;
        }

        #endregion

        #region 게임 상태 핸들러

        private async void OnGameStart()
        {
            try
            {
                if (InventoryDataManager.Instance != null)
                    InventoryDataManager.Instance.ClearInGameSkills();

                if (PlayerDataManager.Instance != null)
                    PlayerDataManager.Instance.PlayerData.nowPlayMObkillCOunt = 0;

                SoundManager.PlaySound(Sound.BGM, SoundKeys.InGame, true);

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
        }

        private void OnResume()
        {
        }

        private async void OnGameOver()
        {
            SpawnedPlayer = null;
            OnPlayerChanged?.Invoke(null);

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

        #region UI 및 팝업 관리

        public void SetMenuPopupState(bool isPause)
        {
            if (isPause) PlayStateManager.instance.Pause();
            else PlayStateManager.instance.Resume();
        }

        public void OpenOptionPopup()
        {
            if (m_optionPopupPrefab == null) return;
            var popup = Instantiate(m_optionPopupPrefab, transform);
            popup.gameObject.SetActive(true);
        }

        #endregion

        #region 플레이어 스폰 및 무기 장착

        public async UniTask ChangeCharacterAndWeapon_Spawn()
        {
            if (m_playerContainer == null) return;

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

                var initialWeapons = new List<SkillData>();

                if (m_skillDatabase != null)
                {
                    SkillData defaultWeaponSkill =
                        m_skillDatabase.allSkills.FirstOrDefault(s => s.skillCode == "WP_BONE");
                    if (defaultWeaponSkill != null)
                    {
                        initialWeapons.Add(defaultWeaponSkill);
                    }
                }
#if UNITY_EDITOR
                initialWeapons.AddRange(TestWeapons.Where(w => w != null));
#endif
                foreach (var weaponSkill in initialWeapons.Distinct())
                {
                    await EquipNewWeapon(weaponSkill, false);
                }

                if (m_playerController != null)
                {
                    m_playerController.AssignCharacter(SpawnedPlayer);
                }

                OnPlayerChanged?.Invoke(SpawnedPlayer);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[게임 매니저] 스폰 과정에서 오류 발생: {ex.Message}");
                SpawnedPlayer = null;
            }
        }

        public async UniTask EquipNewWeapon(SkillData skillData, bool playEffect = true, int startLevel = 1,
            bool startEvolved = false)
        {
            await UniTask.Yield(); // 비동기 메서드 경고(CS1998) 해결

            if (SpawnedPlayer == null || skillData.skillType != SkillType.Weapon)
            {
                return;
            }


            #region 신규 무기 시스템 (WeaponFactory + WeaponDataSO)

            if (skillData.weaponData != null && WeaponFactory.IsRegistered(skillData.skillCode))
            {
                // WeaponFactory를 사용하여 컨트롤러 생성 (POCO)
                var controller = WeaponFactory.CreateController(skillData.weaponData, SpawnedPlayer.transform, () => m_playerController.GetCalculatedAttackDirection());
                
                if (controller != null)
                {
                    controller.SkillData = skillData;

                    // 레벨 설정
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
                        controller.LevelUp(); // 진화
                    }

                    SpawnedPlayer.AddController(controller);

                    if (playEffect)
                    {
                        EffectManager.Instance.PlayLevelUpEffect(SpawnedPlayer.GetComponent<SpriteRenderer>());
                    }
                    
                    LogManager.Log($"[게임 매니저] 신규 시스템 기반 무기 장착 완료: {skillData.skillName}", LogManager.LogCategory.Weapon);
                }
            }
            else
            {
                LogManager.LogWarning($"[게임 매니저] 무기 생성 실패: {skillData.skillName} (Data: {skillData.weaponData != null}, Registered: {WeaponFactory.IsRegistered(skillData.skillCode)})");
            }

            #endregion
        }

        public void RemoveWeaponForTest(string skillCode)
        {
            if (SpawnedPlayer != null)
            {
                SpawnedPlayer.RemoveWeapon(skillCode);
            }
        }

        #endregion

        #region 데이터 접근자

        public int GetMobKillCount()
        {
            if (PlayerDataManager.Instance != null &&
                PlayerDataManager.Instance.PlayerData != null)
            {
                return PlayerDataManager.Instance.PlayerData.nowPlayMObkillCOunt;
            }

            return 0;
        }

        public int GetCurrentWave()
        {
            return m_objectPoolSpawner != null ? m_objectPoolSpawner.CurrentWave : 0;
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
            if (PlayerDataManager.Instance != null &&
                PlayerDataManager.Instance.PlayerData != null)
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