using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 뱀서라이크 게임의 핵심 로직(스폰, 상태, UI 연동)을 관리하는 매니저 클래스 (최적화됨)
    /// </summary>
    public class VamserLikeGameManager : MonoBehaviour
    {
        #region 정적 멤버 및 이벤트

        // 플레이어 변경 알림 이벤트 (스폰/사망 등)
        public static event Action<PlayerBase> OnPlayerChanged;

        private static VamserLikeGameManager s_instance;
        public static VamserLikeGameManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindFirstObjectByType<VamserLikeGameManager>();
                    if (s_instance == null)
                    {
                        LogManager.LogError("[GameManager] 인스턴스가 씬에 없습니다.");
                    }
                }
                return s_instance;
            }
        }

        #endregion

        #region 인스펙터 필드

        [Header("Reference Settings")]
        [Tooltip("캐릭터 및 무기가 스폰될 부모 오브젝트 (Player Container)")]
        [FormerlySerializedAs("inGameObjectPlayerParent")]
        [SerializeField] private GameObject m_playerContainer;

        [Tooltip("옵션 팝업 매니저 프리팹")]
        [FormerlySerializedAs("optionPopupManager")]
        [SerializeField] private OptionPopupManager m_optionPopupPrefab;

        #endregion

        #region 내부 캐시 및 상태 변수

        // 외부 컴포넌트 참조
        private ObjectPoolSpawner m_objectPoolSpawner;
        private VamPlayerControll m_playerController;
        private VariableJoystick m_variableJoystick;
        private Camera m_mainCamera;
        private VamserLikeUI m_uiManager;

        // 현재 상태
        public PlayerBase SpawnedPlayer { get; private set; }
        public ObjectPoolSpawner ObjectPoolSpawner => m_objectPoolSpawner;
        public VamPlayerControll PlayerController => m_playerController;
        public VariableJoystick Joystick => m_variableJoystick;
        public Camera MainCamera => m_mainCamera;
        public VamserLikeUI UIManager => m_uiManager;

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
        }

        private void OnEnable()
        {
            // 게임 시작 전 카운트다운 요청
            m_uiManager?.StartGameCountdown();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        #endregion

        #region 초기화 및 이벤트 관리

        private void CacheComponents()
        {
            // FindFirstObjectByType은 비용이 크므로 Awake에서 한 번만 수행
            m_objectPoolSpawner = FindFirstObjectByType<ObjectPoolSpawner>();
            m_playerController = FindFirstObjectByType<VamPlayerControll>();
            m_variableJoystick = FindFirstObjectByType<VariableJoystick>();
            m_uiManager = FindFirstObjectByType<VamserLikeUI>();
            m_mainCamera = Camera.main;

            // 필수 컴포넌트 누락 경고
            if (m_objectPoolSpawner == null) LogManager.LogWarning("[GameManager] Spawner Missing");
            if (m_playerController == null) LogManager.LogWarning("[GameManager] Controller Missing");
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

        // [최적화] async void -> async UniTaskVoid (예외 추적 용이)
        private async void OnGameStart()
        {
            try
            {
                // 데이터 초기화
                if (DogGuns_Games.Lobby.InventoryDataManagerDontdestory.Instance != null)
                    DogGuns_Games.Lobby.InventoryDataManagerDontdestory.Instance.ClearInGameSkills();
                
                if (PlayerDataManagerDontdesytoy.Instance != null)
                    PlayerDataManagerDontdesytoy.Instance.PlayerData.nowPlayMObkillCOunt = 0;

                SoundManager.PlaySound(Sound.BGM, SoundKeys.InGame, true);

                // 플레이어 스폰 대기
                await SpawnPlayerAsync();

                // 스포너 활성화
                if (SpawnedPlayer != null && m_objectPoolSpawner != null)
                {
                    await m_objectPoolSpawner.InitializeAndStartSpawning(SpawnedPlayer);
                    LogManager.Log("[GameManager] Game Started Successfully");
                }
                else
                {
                    LogManager.LogError("[GameManager] Failed to initialize spawner or player");
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

        // [최적화] async void -> async UniTaskVoid
        private async void OnGameOver()
        {
            SpawnedPlayer = null;
            OnPlayerChanged?.Invoke(null);

            LogManager.Log("[GameManager] Game Over Processing...");

            // 코인 정산 및 서버 업로드
            var dataManager = PlayerDataManagerDontdesytoy.Instance;
            if (dataManager != null && dataManager.PlayerData != null)
            {
                var playerData = dataManager.PlayerData;
                playerData.currency1 += playerData.ingameCoin;
                playerData.ingameCoin = 0;

                // 서버 통신은 메인 스레드에서 안전하게 처리
                await UniTask.SwitchToMainThread();
                
                var param = new BackEnd.Param();
                param.Add("Money1", playerData.currency1);

                try
                {
                    await ServerManager.Instance.UploadDataAsync("User_Data", param);
                    LogManager.Log("[GameManager] Coin Data Uploaded");
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

        #region 플레이어 스폰 시스템 (Addressables)

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
            // 게임이 플레이 중이 아니거나 컨테이너가 없으면 중단
            if (!PlayStateManager.instance.IsPlaying || m_playerContainer == null) return;

            try
            {
                // 1. 무기 스폰
                Weaphon_base weapon = await SpawnWeaponAsync();
                if (weapon == null) return;

                // 2. 캐릭터 스폰 및 무기 장착
                await SpawnCharacterAsync(weapon);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[GameManager] Spawn Failed: {ex.Message}");
                SpawnedPlayer = null;
            }
        }

        private async UniTask<Weaphon_base> SpawnWeaponAsync()
        {
            int index = PlayerDataManagerDontdesytoy.Instance.SelectWeaponIndex;
            string key = $"Weapon_{index}";

            try
            {
                var op = Addressables.InstantiateAsync(key, k_SpawnPosition, Quaternion.identity, m_playerContainer.transform);
                GameObject instance = await op.ToUniTask();

                if (instance != null)
                {
                    instance.transform.localPosition = Vector3.zero;
                    return instance.GetComponent<Weaphon_base>();
                }
                return null;
            }
            catch (Exception e)
            {
                LogManager.LogError($"[GameManager] Weapon Spawn Error ({key}): {e.Message}");
                return null;
            }
        }

        private async UniTask SpawnCharacterAsync(Weaphon_base weapon)
        {
            int index = PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex;
            string key = $"Player_Character_{index}";

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
                        SpawnedPlayer.InitializeWeapon(weapon);
                        
                        // 컨트롤러 연결
                        if (m_playerController != null)
                        {
                            m_playerController.AssignCharacter(SpawnedPlayer);
                        }
                    }

                    // 이벤트 전파
                    OnPlayerChanged?.Invoke(SpawnedPlayer);
                }
            }
            catch (Exception e)
            {
                LogManager.LogError($"[GameManager] Character Spawn Error ({key}): {e.Message}");
                SpawnedPlayer = null;
            }
        }

        #endregion

        #region 데이터 접근자 (Helper Methods - 최적화됨)

        // Null 조건 연산자(?.)와 Null 병합 연산자(??)를 사용하여 코드를 간결하게 만듭니다.

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

        public float GetPlayerCurrentExp()
        {
            return SpawnedPlayer != null ? SpawnedPlayer.CurrentExp : 0f;
        }

        public float GetPlayerMaxExp()
        {
            return SpawnedPlayer != null ? SpawnedPlayer.MaxExp : 100f;
        }

        public float GetPlayerExpProgress()
        {
            return SpawnedPlayer != null ? SpawnedPlayer.GetExpProgress() : 0f;
        }

        public int GetCoinCount()
        {
            return PlayerDataManagerDontdesytoy.Instance?.PlayerData?.ingameCoin ?? 0;
        }

        public Vector3 GetPlayerPosition()
        {
            return m_playerContainer != null ? m_playerContainer.transform.position : Vector3.zero;
        }

        public Transform PlayerTransfrom()
        {
            return m_playerContainer != null ? m_playerContainer.transform : transform;
        }

        public void MovePlayer(Vector3 newPosition)
        {
            if (m_playerContainer != null)
                m_playerContainer.transform.position = newPosition;
        }

        #endregion
    }
}