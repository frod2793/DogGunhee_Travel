using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

namespace DogGuns_Games.vamsir
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

        [Header("Editor Start Settings (에디터 전용)")]
        [SerializeField] private int m_startCharacterIndex = 0;

        [Header("Data References")]
        [SerializeField] private SkillDatabase m_skillDatabase;

        [Header("Reference Settings")]
        [SerializeField] private GameObject m_playerContainer;
        [SerializeField] private OptionPopupManager m_optionPopupPrefab;
        
        [Header("Debug")]
        public List<SkillData> TestWeapons = new List<SkillData>();

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

            CacheComponents();
            SubscribeEvents();
        }

        private void Start()
        {
#if UNITY_EDITOR
            if (Application.isPlaying && PlayerDataManagerDontdesytoy.Instance != null)
            {
                PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex = m_startCharacterIndex;
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
                if (DogGuns_Games.Lobby.InventoryDataManagerDontdestory.Instance != null)
                    DogGuns_Games.Lobby.InventoryDataManagerDontdestory.Instance.ClearInGameSkills();
                
                if (PlayerDataManagerDontdesytoy.Instance != null)
                    PlayerDataManagerDontdesytoy.Instance.PlayerData.nowPlayMObkillCOunt = 0;

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

        private void OnPause() { }
        private void OnResume() { }

        private async void OnGameOver()
        {
            SpawnedPlayer = null;
            OnPlayerChanged?.Invoke(null);

            var dataManager = PlayerDataManagerDontdesytoy.Instance;
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
                int charIndex = PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex;
                string charKey = $"Player_Character_{charIndex}";
                GameObject charInstance = await Addressables.InstantiateAsync(charKey, k_SpawnPosition, Quaternion.identity, m_playerContainer.transform).ToUniTask();
                
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
                    SkillData defaultWeaponSkill = m_skillDatabase.allSkills.FirstOrDefault(s => s.skillCode == "WP_BONE");
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

        public async UniTask EquipNewWeapon(SkillData skillData, bool playEffect = true, int startLevel = 1, bool startEvolved = false)
        {
            if (SpawnedPlayer == null || skillData.skillType != SkillType.Weapon || string.IsNullOrEmpty(skillData.weaponAddressableKey))
            {
                return;
            }
            
            string key = skillData.weaponAddressableKey;

            try
            {
                var op = Addressables.InstantiateAsync(key, m_playerContainer.transform);
                GameObject instance = await op.ToUniTask();

                if (instance != null)
                {
                    instance.transform.localPosition = Vector3.zero;
                    var newWeapon = instance.GetComponent<WeaphonBase>();
                    if (newWeapon != null)
                    {
                        newWeapon.skillData = skillData;
                        newWeapon.skillCode = skillData.skillCode;
                        newWeapon.upgradeItemCode = skillData.upgradeItemCode;
                        newWeapon.Thumnail = skillData.skillIcon;
                        newWeapon.ApplyBaseStats();
                        
                        // [추가] 시작 레벨 및 진화 상태 적용
                        for (int i = 1; i < startLevel; i++)
                        {
                            newWeapon.UpgradeLevel();
                        }
                        if (startEvolved)
                        {
                            // 최대 레벨까지 올린 후, 한 번 더 호출하여 진화시킴
                            while (newWeapon.CurrentLevel < WeaphonBase.k_MaxLevel)
                            {
                                newWeapon.UpgradeLevel();
                            }
                            newWeapon.UpgradeLevel();
                        }
                        
                        SpawnedPlayer.AddWeapon(newWeapon);
                        if (playEffect)
                        {
                            EffectManager.Instance.PlayLevelUpEffect(SpawnedPlayer.GetComponent<SpriteRenderer>());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogManager.LogError($"[게임 매니저] 스킬로부터 무기 스폰 오류 ({key}): {e.Message}");
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

        #region 데이터 접근자

        public int GetMobKillCount()
        {
            if (PlayerDataManagerDontdesytoy.Instance != null && PlayerDataManagerDontdesytoy.Instance.PlayerData != null)
            {
                return PlayerDataManagerDontdesytoy.Instance.PlayerData.nowPlayMObkillCOunt;
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
            if (PlayerDataManagerDontdesytoy.Instance != null && PlayerDataManagerDontdesytoy.Instance.PlayerData != null)
            {
                return PlayerDataManagerDontdesytoy.Instance.PlayerData.ingameCoin;
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