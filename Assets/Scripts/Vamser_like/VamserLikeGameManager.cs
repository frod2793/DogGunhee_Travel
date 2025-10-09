using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 뱀서라이크 게임의 기본 게임플레이 관리자
    /// </summary>
    public class VamserLikeGameManager : MonoBehaviour
    {
        #region 필드 및 변수
        
        public static event System.Action<PlayerBase> OnPlayerChanged;
        
        private static VamserLikeGameManager _instance;
        public static VamserLikeGameManager Instance
        {
            get
            {
                // 인스턴스가 아직 없는 경우 씬에서 찾아봅니다.
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<VamserLikeGameManager>();
                    if (_instance == null)
                    {
                        LogManager.LogError("씬에 VamserLikeGameManager 인스턴스가 존재하지 않습니다.");
                    }
                }
                return _instance;
            }
        }

        [HideInInspector] public PlayerBase spawnedPlayer;

        [Header("캐릭터 및 무기가 스폰 될시 부모 오브젝트")] 
        [SerializeField]
        private GameObject inGameObjectPlayerParent;

        private readonly Vector3 _spawnPosition = Vector3.zero;

        [Header("옵션 팝업 매니저")] [SerializeField] private OptionPopupManager optionPopupManager;
        [HideInInspector] public ObjectPoolSpawner objectPoolSpawner;
        [HideInInspector] public VamPlayerControll vamPlayerControll;

        #endregion

        #region Unity 라이프사이클

        /// <summary>
        /// 컴포넌트 초기화 및 이벤트 구독
        /// </summary>
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            
            objectPoolSpawner ??= FindFirstObjectByType<ObjectPoolSpawner>();
            vamPlayerControll ??= FindFirstObjectByType<VamPlayerControll>();

            PlayStateManager.OnGameStart += GameStart;
            PlayStateManager.OnGamePause += Pause;
            PlayStateManager.OnGameResume += Resume;
            PlayStateManager.OnGameOver += OnGameOver;
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void OnDestroy()
        {
            PlayStateManager.OnGameStart -= GameStart;
            PlayStateManager.OnGamePause -= Pause;
            PlayStateManager.OnGameResume -= Resume;
            PlayStateManager.OnGameOver -= OnGameOver;
        }

        #endregion

        #region 게임 상태 관리

        /// <summary>
        /// 게임 시작 이벤트 처리 (비동기로 변경)
        /// </summary>
        private async void GameStart()
        {
            try
            {
                // 새 게임 시작 시, 인게임에서 사용된 인벤토리를 초기화합니다.
                DogGuns_Games.Lobby.InventoryDataManagerDontdestory.Instance.ClearInGameSkills();
                
                PlayStateManager.instance.isPlay = true;
                PlayerDataManagerDontdesytoy.Instance.scritpableobjPlayerData.nowPlayMObkillCOunt = 0;

                SoundManager.PlaySound(Sound.BGM, SoundKeys.InGame, true);
                // 플레이어와 무기 스폰이 완료될 때까지 기다립니다.

                await SpawnPlayer();

                // 플레이어 스폰이 성공적으로 완료된 후에 몹 스포너를 활성화합니다.
                if (spawnedPlayer != null && objectPoolSpawner != null)
                {
                    await objectPoolSpawner.InitializeAndStartSpawning(spawnedPlayer);
                    LogManager.Log("게임 시작: 플레이어 스폰 완료 후 몹 스포너 활성화", LogManager.LogCategory.VamserLikeGameManager);
                }
                else
                {
                    LogManager.LogError("플레이어 스폰에 실패했거나 ObjectPoolSpawner를 찾을 수 없어 게임을 시작할 수 없습니다.");
                }
            }
            catch (System.Exception e)
            {
                LogManager.LogError($"게임 시작 중 심각한 오류 발생: {e.Message}", LogManager.LogCategory.VamserLikeGameManager);
            }
        }

        /// <summary>
        /// 게임 일시정지 이벤트 처리
        /// </summary>
        private void Pause()
        {
            PlayStateManager.instance.isPlay = false;
            LogManager.Log("게임 일시정지", LogManager.LogCategory.VamserLikeGameManager);
        }

        /// <summary>
        /// 게임 재개 이벤트 처리
        /// </summary>
        private void Resume()
        {
            PlayStateManager.instance.isPlay = true;
            LogManager.Log("게임 재개", LogManager.LogCategory.VamserLikeGameManager);
        }

        private async void OnGameOver()
        {
            PlayStateManager.instance.isPlay = false;

            spawnedPlayer = null; // 게임 오버 시 플레이어 참조 초기화
            OnPlayerChanged?.Invoke(null); // 플레이어가 사라졌음을 알림

            // 게임 내에 획득한 코인 합산
            var playerData = PlayerDataManagerDontdesytoy.Instance?.scritpableobjPlayerData;
            if (playerData != null)
            {
                playerData.currency1 += playerData.ingameCoin; // ingameCoin을 totalCoin에 합산
                playerData.ingameCoin = 0; // 인게임 코인 초기화
                LogManager.Log($"게임 오버: 코인 합산 완료 (총 코인: {playerData.currency1})",
                    LogManager.LogCategory.VamserLikeGameManager);

                // 서버 업로드는 반드시 메인 스레드에서 실행
                await UniTask.SwitchToMainThread();
                var param = new BackEnd.Param();
                param.Add("Money1", playerData.currency1); // OnGameOver에서는 코인만 업데이트
                try
                {
                    await ServerManager.Instance.UploadDataAsync("User_Data", param);
                    LogManager.Log("서버에 코인 데이터 업로드 성공", LogManager.LogCategory.VamserLikeGameManager);
                }
                catch (System.Exception e)
                {
                    LogManager.LogError($"서버에 코인 데이터 업로드 실패: {e.Message}", LogManager.LogCategory.VamserLikeGameManager);
                }
            }
            else
            {
                LogManager.LogWarning("PlayerDataManagerDontdesytoy 또는 scritpableobjPlayerData가 null입니다. 코인 합산 실패",
                    LogManager.LogCategory.VamserLikeGameManager);
            }

            LogManager.Log("게임 오버", LogManager.LogCategory.VamserLikeGameManager);
        }

        /// <summary>
        /// 메뉴 팝업 열기와 게임 상태 변경
        /// </summary>
        public void SetMenuPopupState(bool isPause)
        {
            PlayStateManager.instance.PlayState =
                isPause ? PlayStateManager.GameState.Pause : PlayStateManager.GameState.Resume;

            LogManager.Log($"메뉴 팝업 {(isPause ? "열림" : "닫힘")}", LogManager.LogCategory.VamserLikeGameManager);
        }

        /// <summary>
        /// 옵션 팝업 열기
        /// </summary>
        public void OpenOptionPopup()
        {
            if (optionPopupManager == null)
            {
                LogManager.LogError("옵션 팝업 매니저가 설정되지 않았습니다.", LogManager.LogCategory.VamserLikeGameManager);
                return;
            }

            // Instantiate는 프리팹의 복제본을 생성합니다.
            // 생성된 인스턴스를 변수에 저장하여 사용해야 합니다.
            var popupInstance = Instantiate(optionPopupManager);
            popupInstance.gameObject.SetActive(true); // 프리팹이 비활성화 상태일 경우를 대비해 명시적으로 활성화
            LogManager.Log("옵션 팝업 열림", LogManager.LogCategory.VamserLikeGameManager);
        }

        #endregion

        #region 플레이어/무기 스폰 관리

        /// <summary>
        /// 현재 선택된 플레이어와 무기를 비동기적으로 스폰합니다. (반환 타입을 UniTask로 변경)
        /// </summary>
        private async UniTask SpawnPlayer()
        {
            if (!PlayStateManager.instance.isPlay || inGameObjectPlayerParent == null)
            {
                LogManager.LogWarning("게임이 플레이 상태가 아니거나 부모 오브젝트가 설정되지 않았습니다.",
                    LogManager.LogCategory.VamserLikeGameManager);
                return;
            }

            try
            {
                Weaphon_base spawnedWeapon = await SpawnSelectedWeapon();
                if (spawnedWeapon == null)
                {
                    LogManager.LogError("무기 스폰에 실패하여 캐릭터를 스폰할 수 없습니다.", LogManager.LogCategory.VamserLikeGameManager);
                    return;
                }

                await SpawnSelectedCharacter(spawnedWeapon);
            }
            catch (System.Exception ex)
            {
                LogManager.LogError($"플레이어 또는 무기 스폰 중 예외 발생: {ex.Message}",
                    LogManager.LogCategory.VamserLikeGameManager);
                spawnedPlayer = null; // 실패 시 참조를 null로 설정
            }
        }

        /// <summary>
        /// 선택된 무기를 Addressable을 사용하여 스폰합니다.
        /// </summary>
        private async UniTask<Weaphon_base> SpawnSelectedWeapon()
        {
            int weaponIndex = PlayerDataManagerDontdesytoy.Instance.SelectWeaponIndex;
            string addressableKey = $"Weapon_{weaponIndex}"; // 무기 Addressable 주소 규칙

            try
            {
                GameObject weaponInstance = await Addressables.InstantiateAsync(addressableKey, _spawnPosition,
                    Quaternion.identity, inGameObjectPlayerParent.transform).ToUniTask();
                if (weaponInstance != null)
                {
                    weaponInstance.transform.localPosition = Vector3.zero; // 스폰 후 로컬 좌표를 0으로 초기화
                    LogManager.Log($"무기 스폰 성공: {addressableKey}", LogManager.LogCategory.VamserLikeGameManager);
                    return weaponInstance.GetComponent<Weaphon_base>();
                }

                return null;
            }
            catch (System.Exception ex)
            {
                LogManager.LogError($"Addressable 키 '{addressableKey}'를 가진 무기 스폰 중 예외 발생: {ex.Message}",
                    LogManager.LogCategory.VamserLikeGameManager);
                return null;
            }
        }

        /// <summary>
        /// 선택된 캐릭터를 스폰하고 무기를 설정합니다.
        /// </summary>
        private async UniTask SpawnSelectedCharacter(Weaphon_base weaponToAssign)
        {
            int characterIndex = PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex;
            string addressableKey = $"Player_Character_{characterIndex}";

            try
            {
                GameObject characterInstance = await Addressables.InstantiateAsync(addressableKey, _spawnPosition,
                    Quaternion.identity, inGameObjectPlayerParent.transform).ToUniTask();
                if (characterInstance != null)
                {
                    characterInstance.transform.localPosition = Vector3.zero; // 스폰 후 로컬 좌표를 0으로 초기화
                    LogManager.Log($"캐릭터 스폰 성공: {addressableKey}", LogManager.LogCategory.VamserLikeGameManager);
                    spawnedPlayer = characterInstance.GetComponent<PlayerBase>();
                    if (spawnedPlayer != null)
                    {
                        spawnedPlayer.InitializeWeapon(weaponToAssign);
                        if (vamPlayerControll != null)
                        {
                            vamPlayerControll.AssignCharacter(spawnedPlayer);
                        }
                        else
                        {
                            LogManager.LogError("VamPlayerControll을 찾을 수 없습니다.",
                                LogManager.LogCategory.VamserLikeGameManager);
                        }
                    }
                    else
                    {
                        LogManager.LogError("스폰된 캐릭터에서 PlayerBase 컴포넌트를 찾을 수 없습니다.",
                            LogManager.LogCategory.VamserLikeGameManager);
                    }
                    
                    // 새 플레이어가 성공적으로 스폰되었음을 모든 리스너에게 알립니다.
                    OnPlayerChanged?.Invoke(spawnedPlayer);
                }
            }
            catch (System.Exception ex)
            {
                LogManager.LogError($"Addressable 키 '{addressableKey}'를 가진 캐릭터 스폰 중 예외 발생: {ex.Message}",
                    LogManager.LogCategory.VamserLikeGameManager);
                // 실패 시 _spawnedPlayer를 null로 설정
                spawnedPlayer = null;
            }
        }


        /// <summary>
        /// 현재 스폰된 캐릭터와 무기를 변경
        /// </summary>
        public async UniTask ChangeCharacterAndWeapon_Spawn()
        {
            if (inGameObjectPlayerParent == null)
            {
                LogManager.LogError("인게임 오브젝트 부모가 설정되지 않았습니다.", LogManager.LogCategory.VamserLikeGameManager);
                return;
            }

            // 현재 캐릭터와 무기 제거
            for (int i = inGameObjectPlayerParent.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = inGameObjectPlayerParent.transform.GetChild(i);
                // Addressables로 생성된 오브젝트는 Addressables.ReleaseInstance로 해제��는 것이 좋습니다.
                Addressables.ReleaseInstance(child.gameObject);
            }

            spawnedPlayer = null; // 이전 플레이어 참조 제거
            OnPlayerChanged?.Invoke(null); // 플레이어가 제거되었음을 모든 리스너에게 알립니다.

            // 새 캐릭터와 무기 스폰 (비동기 호출 및 대기)
            await SpawnPlayer();
            LogManager.Log("캐릭터와 무기 변경 완료", LogManager.LogCategory.VamserLikeGameManager);
        }

        #endregion

        #region 데이터 접근자 (Data Accessors)
        
        /// <summary>
        /// 현재 처치한 몹 수 반환
        /// </summary>
        public int Mob_Count()
        {
            return PlayerDataManagerDontdesytoy.Instance?.scritpableobjPlayerData?.nowPlayMObkillCOunt ?? 0;
        }

        /// <summary>
        /// 현재 몹 스폰 웨이브 반환
        /// </summary>
        public int MobSpawnWave()
        {
            return objectPoolSpawner?.MobSpawnWave ?? 0;
        }


        /// <summary>
        /// 현재 플레이어 레벨 반환
        /// </summary>
        public float PlayerLevel()
        {
            if (spawnedPlayer != null)
            {
                return spawnedPlayer.Level;
            }

            LogManager.LogWarning("스폰된 플레이어가 없어 레벨을 가져올 수 없습니다.", LogManager.LogCategory.VamserLikeGameManager);
            return 1; // 기본값 반환
        }

        /// <summary>
        /// 현재 플레이어의 경험치 반환
        /// </summary>
        public float GetPlayerCurrentExp()
        {
            if (spawnedPlayer != null)
            {
                return spawnedPlayer.CurrentExp;
            }

            return 0f;
        }

        /// <summary>
        /// 현재 플레이어의 최대 경험치 반환
        /// </summary>
        public float GetPlayerMaxExp()
        {
            if (spawnedPlayer != null)
            {
                return spawnedPlayer.MaxExp;
            }

            return 100f; // 기본값
        }

        /// <summary>
        /// 현재 플레이어의 경험치 진행률 반환 (0~1)
        /// </summary>
        public float GetPlayerExpProgress()
        {
            if (spawnedPlayer != null)
            {
                return spawnedPlayer.GetExpProgress();
            }

            return 0f;
        }

        /// <summary>
        /// 현재 코인 수 반환
        /// </summary>
        public int CoinCount()
        {
            return PlayerDataManagerDontdesytoy.Instance?.scritpableobjPlayerData.ingameCoin ?? 0;
        }

        /// <summary>
        /// 플레이어의 실제 이동 주체인 inGameObjectParent의 현재 월드 위치를 반환합니다.
        /// </summary>
        public Vector3 PlayerPos()
        {
            if (inGameObjectPlayerParent != null)
            {
                return inGameObjectPlayerParent.transform.position;
            }
            LogManager.LogWarning("inGameObjectParent가 할당되지 않아 플레이어 위치를 가져올 수 없습니다.", LogManager.LogCategory.VamserLikeGameManager);
            return Vector3.zero; // 기본값 반환
        }

        public Transform PlayerTransfrom()
        {
            if (inGameObjectPlayerParent != null)
            {
                return inGameObjectPlayerParent.transform;
            }
            LogManager.LogWarning("inGameObjectParent가 할당되지 않아 플레이어 위치를 가져올 수 없습니다.", LogManager.LogCategory.VamserLikeGameManager);
            return inGameObjectPlayerParent.transform;// 기본값 반환
        }
        
        
        /// <summary>
        /// 플레이어(inGameObjectPlayerParent)를 새로운 위치로 이동시킵니다.
        /// </summary>
        /// <param name="newPosition">이동할 새로운 월드 위치</param>
        public void MovePlayer(Vector3 newPosition)
        {
            if (inGameObjectPlayerParent != null) inGameObjectPlayerParent.transform.position = newPosition;
        }
        
        #endregion
    }
}