using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Serialization;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 뱀서라이크 게임의 핵심 로직(스폰, 상태 관리, UI 연동, 데이터 관리)을 총괄하는 매니저 클래스입니다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region 정적 멤버 및 이벤트

        // 플레이어 변경 알림 이벤트 (스폰/사망/교체 시 발생)
        public static event Action<PlayerBase> OnPlayerChanged;

        private static GameManager s_instance;
        public static GameManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindFirstObjectByType<GameManager>();
                    if (s_instance == null)
                    {
                        LogManager.LogError("[GameManager] 씬에 GameManager 인스턴스가 없습니다.");
                    }
                }
                return s_instance;
            }
        }

        #endregion

        #region 인스펙터 필드

        [Header("Editor Start Settings (에디터 전용)")]
        [Tooltip("에디터 플레이 모드 시작 시 적용할 캐릭터 인덱스")]
        [SerializeField] private int m_startCharacterIndex = 0;

        [Tooltip("에디터 플레이 모드 시작 시 적용할 무기 인덱스")]
        [SerializeField] private int m_startWeaponIndex = 0;
        
        // [추가] 시작 시 무기 레벨 2 적용 여부
        [Tooltip("에디터 시작 시 무기 레벨 2 적용 여부")]
        [SerializeField] private bool m_startWeaponUpgradeLv2 = false;

        [Header("Reference Settings")]
        [Tooltip("캐릭터 및 무기가 스폰될 부모 오브젝트 (Player Container)")]
        [FormerlySerializedAs("inGameObjectPlayerParent")]
        [SerializeField] private GameObject m_playerContainer;

        [Tooltip("옵션 팝업 매니저 프리팹")]
        [FormerlySerializedAs("optionPopupManager")]
        [SerializeField] private OptionPopupManager m_optionPopupPrefab;

        #endregion

        #region 내부 캐시 및 상태 변수

        // 외부 컴포넌트 참조 캐싱
        private ObjectPoolSpawner m_objectPoolSpawner;
        private PlayerControll m_playerController;
        private VariableJoystick m_variableJoystick;
        private Camera m_mainCamera;
        private UIManager _mUIManagerManager;

        // 현재 상태 프로퍼티
        public PlayerBase SpawnedPlayer { get; private set; }
        public ObjectPoolSpawner ObjectPoolSpawner => m_objectPoolSpawner;
        public PlayerControll PlayerController => m_playerController;
        public VariableJoystick Joystick => m_variableJoystick;
        public Camera MainCamera => m_mainCamera;
        public UIManager UIManagerManager => _mUIManagerManager;

        // 상수
        private static readonly Vector3 k_SpawnPosition = Vector3.zero;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            // 싱글톤 중복 방지
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
            // [중요] 에디터 테스트 설정 적용 (Awake 대신 Start 사용)
            // PlayerDataManager 등 다른 싱글톤이 초기화된 후 실행되어야 안전합니다.
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                if (PlayerDataManagerDontdesytoy.Instance != null)
                {
                    PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex = m_startCharacterIndex;
                    PlayerDataManagerDontdesytoy.Instance.SelectWeaponIndex = m_startWeaponIndex;
                    LogManager.Log($"[Editor] 시작 설정 적용: Char({m_startCharacterIndex}), Wep({m_startWeaponIndex})");
                    
                    // 무기 레벨 설정 이벤트 구독 (일회성)
                    void ApplyStartLevel(PlayerBase player)
                    {
                        if (player != null && player.Weapons.Any())
                        {
                            var firstWeapon = player.Weapons.FirstOrDefault();
                            if (firstWeapon != null)
                            {
                                firstWeapon.isUpgradelv2 = m_startWeaponUpgradeLv2;
                                LogManager.Log($"[Editor] 시작 무기 레벨 적용: {(m_startWeaponUpgradeLv2 ? "Lv2" : "Lv1")}");
                            }
                        }
                        OnPlayerChanged -= ApplyStartLevel;
                    }
                    OnPlayerChanged += ApplyStartLevel;
                }
            }
#endif
        }

        private async void OnEnable()
        {
            // 플레이어와 무기를 먼저 스폰합니다.
            await SpawnPlayerAsync();
            
            // 스폰이 완료된 후 게임 시작 카운트다운을 요청합니다.
            _mUIManagerManager?.StartGameCountdown();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        #endregion

        #region 초기화 및 이벤트 관리

        private void CacheComponents()
        {
            // 비용이 큰 FindFirstObjectByType은 Awake에서 한 번만 수행
            m_objectPoolSpawner = FindFirstObjectByType<ObjectPoolSpawner>();
            m_playerController = FindFirstObjectByType<PlayerControll>();
            m_variableJoystick = FindFirstObjectByType<VariableJoystick>();
            _mUIManagerManager = FindFirstObjectByType<UIManager>();
            m_mainCamera = Camera.main;

            // 필수 컴포넌트 누락 시 경고
            if (m_objectPoolSpawner == null) LogManager.LogWarning("[GameManager] ObjectPoolSpawner Missing");
            if (m_playerController == null) LogManager.LogWarning("[GameManager] PlayerControll Missing");
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

        // [비동기] 게임 시작 로직
        private async void OnGameStart()
        {
            try
            {
                // 1. 데이터 초기화
                if (DogGuns_Games.Lobby.InventoryDataManagerDontdestory.Instance != null)
                    DogGuns_Games.Lobby.InventoryDataManagerDontdestory.Instance.ClearInGameSkills();
                
                if (PlayerDataManagerDontdesytoy.Instance != null)
                    PlayerDataManagerDontdesytoy.Instance.PlayerData.nowPlayMObkillCOunt = 0;

                SoundManager.PlaySound(Sound.BGM, SoundKeys.InGame, true);

                // 2. 플레이어가 이미 스폰되었으므로 몹 스포너를 활성화합니다.
                if (SpawnedPlayer != null && m_objectPoolSpawner != null)
                {
                    await m_objectPoolSpawner.InitializeAndStartSpawning(SpawnedPlayer);
                    LogManager.Log("[GameManager] Game Started Successfully & Spawner Initialized");
                }
                else
                {
                    // 플레이어 스폰이 OnEnable에서 실패했을 수 있습니다.
                    LogManager.LogError("[GameManager] Failed to initialize spawner because player was not spawned.");
                }
            }
            catch (Exception e)
            {
                LogManager.LogError($"[GameManager] Error during GameStart: {e.Message}");
            }
        }

        private void OnPause()
        {
            LogManager.Log("[GameManager] Game Paused");
        }

        private void OnResume()
        {
            LogManager.Log("[GameManager] Game Resumed");
        }

        // [비동기] 게임 오버 로직
        private async void OnGameOver()
        {
            SpawnedPlayer = null;
            OnPlayerChanged?.Invoke(null); // 플레이어 소멸 알림

            LogManager.Log("[GameManager] Game Over Processing...");

            // 코인 정산 및 서버 업로드
            var dataManager = PlayerDataManagerDontdesytoy.Instance;
            if (dataManager != null && dataManager.PlayerData != null)
            {
                var playerData = dataManager.PlayerData;
                
                // 획득한 인게임 코인을 전체 코인에 합산
                playerData.currency1 += playerData.ingameCoin;
                playerData.ingameCoin = 0;

                // 서버 통신은 메인 스레드에서 안전하게 처리
                await UniTask.SwitchToMainThread();
                
                var param = new BackEnd.Param();
                param.Add("Money1", playerData.currency1);

                try
                {
                    await ServerManager.Instance.UploadDataAsync("User_Data", param);
                    LogManager.Log("[GameManager] Coin Data Uploaded Successfully");
                }
                catch (Exception e)
                {
                    LogManager.LogError($"[GameManager] Upload Failed: {e.Message}");
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

            var popup = Instantiate(m_optionPopupPrefab);
            popup.gameObject.SetActive(true);
        }

        #endregion

        #region 플레이어 스폰 및 무기 장착

        /// <summary>
        /// 런타임 중 캐릭터와 무기를 교체하고 다시 스폰합니다.
        /// </summary>
        public async UniTask ChangeCharacterAndWeapon_Spawn()
        {
            if (m_playerContainer == null) return;

            // 기존 객체 정리 (역순 순회로 안전하게 제거)
            for (int i = m_playerContainer.transform.childCount - 1; i >= 0; i--)
            {
                GameObject childObj = m_playerContainer.transform.GetChild(i).gameObject;
                Addressables.ReleaseInstance(childObj); // Addressable로 생성된 객체 해제
            }

            SpawnedPlayer = null;
            OnPlayerChanged?.Invoke(null);

            // 새 플레이어 스폰
            await SpawnPlayerAsync();
        }

        private async UniTask SpawnPlayerAsync()
        {
            // 컨테이너가 없으면 중단
            if (m_playerContainer == null)
            {
                LogManager.LogError("[GameManager] Player Container is not set. Cannot spawn player.");
                return;
            }

            try
            {
                // 1. 무기 스폰
                WeaphonBase weapon = await SpawnWeaponAsync(PlayerDataManagerDontdesytoy.Instance.SelectWeaponIndex);
                if (weapon == null) 
                {
                    LogManager.LogError("[GameManager] Initial Weapon Spawn Failed");
                    return;
                }

                // 2. 캐릭터 스폰 및 무기 장착
                await SpawnCharacterAsync(weapon);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[GameManager] Spawn Process Failed: {ex.Message}");
                SpawnedPlayer = null;
            }
        }

        private async UniTask<WeaphonBase> SpawnWeaponAsync(int weaponIndex)
        {
            string key = $"Weapon_{weaponIndex}"; // Addressable Key

            try
            {
                // [수정] 초기 무기는 PlayerContainer를 부모로 하여 스폰합니다.
                var op = Addressables.InstantiateAsync(key, k_SpawnPosition, Quaternion.identity, m_playerContainer.transform);
                GameObject instance = await op.ToUniTask();

                if (instance != null)
                {
                    instance.transform.localPosition = Vector3.zero;
                    return instance.GetComponent<WeaphonBase>();
                }
                return null;
            }
            catch (Exception e)
            {
                LogManager.LogError($"[GameManager] Weapon Spawn Error ({key}): {e.Message}");
                return null;
            }
        }
        
        private async UniTask<WeaphonBase> SpawnWeaponFromSkillAsync(SkillData skillData)
        {
            if (skillData.skillType != SkillType.Weapon || string.IsNullOrEmpty(skillData.weaponAddressableKey))
            {
                LogManager.LogError($"[GameManager] Invalid skill data for spawning a weapon: {skillData.skillName}");
                return null;
            }
            
            string key = skillData.weaponAddressableKey;

            try
            {
                var op = Addressables.InstantiateAsync(key, k_SpawnPosition, Quaternion.identity, m_playerContainer.transform);
                GameObject instance = await op.ToUniTask();

                if (instance != null)
                {
                    instance.transform.localPosition = Vector3.zero;
                    return instance.GetComponent<WeaphonBase>();
                }
                return null;
            }
            catch (Exception e)
            {
                LogManager.LogError($"[GameManager] Weapon Spawn Error from Skill ({key}): {e.Message}");
                return null;
            }
        }

        private async UniTask SpawnCharacterAsync(WeaphonBase initialWeapon)
        {
            int index = PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex;
            string key = $"Player_Character_{index}"; // Addressable Key

            try
            {
                var op = Addressables.InstantiateAsync(key, k_SpawnPosition, Quaternion.identity, m_playerContainer.transform);
                GameObject instance = await op.ToUniTask();

                if (instance != null)
                {
                    instance.transform.localPosition = Vector3.zero;
                    SpawnedPlayer = instance.GetComponent<PlayerBase>();

                    if (SpawnedPlayer != null)
                    {
                        // 무기 장착 및 부모 재설정
                        initialWeapon.transform.SetParent(SpawnedPlayer.transform);
                        SpawnedPlayer.AddWeapon(initialWeapon);
                        
                        // 컨트롤러 연결
                        if (m_playerController != null)
                        {
                            m_playerController.AssignCharacter(SpawnedPlayer);
                        }
                    }
                    else
                    {
                        LogManager.LogError($"[GameManager] PlayerBase component missing on {instance.name}");
                    }

                    // 이벤트 전파 (UI 업데이트 등)
                    OnPlayerChanged?.Invoke(SpawnedPlayer);
                }
            }
            catch (Exception e)
            {
                LogManager.LogError($"[GameManager] Character Spawn Error ({key}): {e.Message}");
                SpawnedPlayer = null;
            }
        }
        
        /// <summary>
        /// 스킬 선택으로 새로운 무기를 획득하고 장착합니다.
        /// </summary>
        public async UniTask EquipNewWeapon(SkillData skillData)
        {
            if (SpawnedPlayer == null)
            {
                LogManager.LogError("[GameManager] Cannot equip weapon, player is not spawned.");
                return;
            }

            WeaphonBase newWeapon = await SpawnWeaponFromSkillAsync(skillData);
            if (newWeapon != null)
            {
                newWeapon.transform.SetParent(SpawnedPlayer.transform);
                SpawnedPlayer.AddWeapon(newWeapon);
                LogManager.Log($"[GameManager] New weapon equipped: {skillData.skillName}");
                
                // 레벨업 이펙트 재생
                EffectManager.Instance.PlayLevelUpEffect(SpawnedPlayer.GetComponent<SpriteRenderer>());
            }
        }

        #endregion

        #region 데이터 접근자 (Helper Methods - 최적화됨)

        // Null 조건 연산자(?.)와 Null 병합 연산자(??)를 사용하여 안전하게 데이터 반환

        public int GetMobKillCount()
        {
            return PlayerDataManagerDontdesytoy.Instance?.PlayerData?.nowPlayMObkillCOunt ?? 0;
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
            return PlayerDataManagerDontdesytoy.Instance?.PlayerData?.ingameCoin ?? 0;
        }

    

        public Transform PlayerTransfrom()
        {
            return m_playerContainer != null ? m_playerContainer.transform : transform;
        }


        #endregion
    }
}