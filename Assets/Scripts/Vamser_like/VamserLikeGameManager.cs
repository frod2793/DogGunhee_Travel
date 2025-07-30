using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 뱀서라이크 게임의 기본 게임플레이 관리자
    /// </summary>
    public class VamserLikeGameManager : MonoBehaviour
    {
        //TODO: 게임 오버시 플레이어 이동 완전정지 및 조이스틱 비활성화 
        //게임오버시 획득 코인 플레이어 데이터에 저장후 동기화 
        
        #region 필드 및 변수

        private PlayerBase _spawnedPlayer;
        
        [Header("캐릭터 및 무기가 스폰 될시 부모 오브젝트")]
        [SerializeField] private GameObject inGameObjectParent;

        private readonly Vector3 _spawnPosition = Vector3.zero;

        [Header("옵션 팝업 매니저")]
        [SerializeField] private OptionPopupManager optionPopupManager;

        private ObjectPoolSpawner _objectPoolSpawner;
        private VamPlayerControll _vamPlayerControll;
        #endregion

        #region Unity 라이프사이클

        /// <summary>
        /// 컴포넌트 초기화 및 이벤트 구독
        /// </summary>
        /// <summary>
        /// 컴포넌트 초기화 및 이벤트 구독
        /// </summary>
        private void Awake()
        {
            _objectPoolSpawner = FindFirstObjectByType<ObjectPoolSpawner>();
            _vamPlayerControll = FindFirstObjectByType<VamPlayerControll>();

            PlayStateManager.OnGameStart += GameStart;
            PlayStateManager.OnGamePause += Pause;
            PlayStateManager.OnGameResume += Resume;
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void OnDestroy()
        {
            PlayStateManager.OnGameStart -= GameStart;
            PlayStateManager.OnGamePause -= Pause;
            PlayStateManager.OnGameResume -= Resume;
        }


        #endregion

        #region 게임 상태 관리

        /// <summary>
        /// 게임 시작 이벤트 처리 (비동기로 변경)
        /// </summary>
        private async void GameStart()
        {
            PlayStateManager.instance.isPlay = true;
            PlayerDataManagerDontdesytoy.Instance.scritpableobjPlayerData.nowPlayMObkillCOunt = 0;
            
            SoundManager.PlaySound(Sound.BGM, SoundKeys.InGame, true);
            // 플레이어와 무기 스폰이 완료될 때까지 기다립니다.
            
            await SpawnPlayer();
            
            // 플레이어 스폰이 성공적으로 완료된 후에 몹 스포너를 활성화합니다.
            if (_spawnedPlayer != null && _objectPoolSpawner != null)
            {
                await _objectPoolSpawner.InitializeAndStartSpawning(_spawnedPlayer);
                Debug.Log("게임 시작: 플레이어 스폰 완료 후 몹 스포너 활성화");
            }
            else
            {
                Debug.LogError("플레이어 스폰에 실패했거나 ObjectPoolSpawner를 찾을 수 없어 게임을 시작할 수 없습니다.");
            }
        }

        /// <summary>
        /// 게임 일시정지 이벤트 처리
        /// </summary>
        private void Pause()
        {
            PlayStateManager.instance.isPlay = false;
            Debug.Log("게임 일시정지");
        }

        /// <summary>
        /// 게임 재개 이벤트 처리
        /// </summary>
        private void Resume()
        {
            PlayStateManager.instance.isPlay = true;
            Debug.Log("게임 재개");
        }

        /// <summary>
        /// 메뉴 팝업 열기와 게임 상태 변경
        /// </summary>
        public void Open_MenuPopUp(bool isPause)
        {
            PlayStateManager.instance.PlayState = isPause ? 
                PlayStateManager.GameState.Pause : 
                PlayStateManager.GameState.Resume;
            
            Debug.Log($"메뉴 팝업 {(isPause ? "열림" : "닫힘")}");
        }

        /// <summary>
        /// 옵션 팝업 열기
        /// </summary>
        public void Open_OptionPopUp()
        {
            if (optionPopupManager == null)
            {
                Debug.LogError("옵션 팝업 매니저가 설정되지 않았습니다.");
                return;
            }

            Instantiate(optionPopupManager);
            optionPopupManager.gameObject.SetActive(true);
            Debug.Log("옵션 팝업 열림");
        }

        #endregion

        #region 플레이어/무기 스폰 관리

       
        /// <summary>
        /// 현재 선택된 플레이어와 무기를 비동기적으로 스폰합니다. (반환 타입을 UniTask로 변경)
        /// </summary>
        private async UniTask SpawnPlayer()
        {
            if (!PlayStateManager.instance.isPlay || inGameObjectParent == null)
            {
                Debug.LogWarning("게임이 플레이 상태가 아니거나 부모 오브젝트가 설정되지 않았습니다.");
                return;
            }

            try
            {
                Weaphon_base spawnedWeapon = await SpawnSelectedWeapon();
                if (spawnedWeapon == null)
                {
                    Debug.LogError("무기 스폰에 실패하여 캐릭터를 스폰할 수 없습니다.");
                    return;
                }

                await SpawnSelectedCharacter(spawnedWeapon);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"플레이어 또는 무기 스폰 중 예외 발생: {ex.Message}");
                _spawnedPlayer = null; // 실패 시 참조를 null로 설정
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
                GameObject weaponInstance = await Addressables.InstantiateAsync(addressableKey, _spawnPosition, Quaternion.identity, inGameObjectParent.transform).ToUniTask();
                if (weaponInstance != null)
                {
                    Debug.Log($"무기 스폰 성공: {addressableKey}");
                    return weaponInstance.GetComponent<Weaphon_base>();
                }
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Addressable 키 '{addressableKey}'를 가진 무기 스폰 중 예외 발생: {ex.Message}");
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
                GameObject characterInstance = await Addressables.InstantiateAsync(addressableKey, _spawnPosition, Quaternion.identity, inGameObjectParent.transform).ToUniTask();
                if (characterInstance != null)
                {
                    Debug.Log($"캐릭터 스폰 성공: {addressableKey}");
                    _spawnedPlayer = characterInstance.GetComponent<PlayerBase>();
                    if (_spawnedPlayer != null)
                    {
                        _spawnedPlayer.InitializeWeapon(weaponToAssign);
                        if (_vamPlayerControll != null)
                        {
                            _vamPlayerControll.AssignCharacter(_spawnedPlayer);
                        }
                        else
                        {
                            Debug.LogError("VamPlayerControll을 찾을 수 없습니다.");
                        }
                    }
                    else
                    {
                        Debug.LogError("스폰된 캐릭터에서 PlayerBase 컴포넌트를 찾을 수 없습니다.");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Addressable 키 '{addressableKey}'를 가진 캐릭터 스폰 중 예외 발생: {ex.Message}");
                // 실패 시 _spawnedPlayer를 null로 설정
                _spawnedPlayer = null;
            }
        }


        /// <summary>
        /// 현재 스폰된 캐릭터와 무기를 변경
        /// </summary>
        public async void ChangeCharacterAndWeapon_Spawn()
        {
            if (inGameObjectParent == null)
            {
                Debug.LogError("인게임 오브젝트 부모가 설정되지 않았습니다.");
                return;
            }

            // 현재 캐릭터와 무기 제거
            for (int i = inGameObjectParent.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = inGameObjectParent.transform.GetChild(i);
                // Addressables로 생성된 오브젝트는 Addressables.ReleaseInstance로 해제하는 것이 좋습니다.
                Addressables.ReleaseInstance(child.gameObject);
            }
            _spawnedPlayer = null; // 참조 제거

            // 새 캐릭터와 무기 스폰 (비동기 호출 및 대기)
            await SpawnPlayer();
            Debug.Log("캐릭터와 무기 변경 완료");
        }

        #endregion

        #region UI 효과 및 데이터 접근

        /// <summary>
        /// 웨이브 텍스트에 페이드 효과 적용
        /// </summary>
        public void WaveTextFadeEffect(TMP_Text mobWaveText)
        {
            if (mobWaveText == null)
            {
                Debug.LogError("웨이브 텍스트가 null입니다.");
                return;
            }
            
            // 페이드 인 효과
            mobWaveText.DOFade(1, 1)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    // 페이드 아웃 효과
                    mobWaveText.DOFade(0, 1)
                        .SetEase(Ease.Linear)
                        .SetDelay(1f);
                });
        }

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
            return _objectPoolSpawner?.MobSpawnWave ?? 0;
        }

     
        /// <summary>
        /// 현재 플레이어 레벨 반환
        /// </summary>
        public float PlayerLevel()
        {
            if (_spawnedPlayer != null)
            {
                return _spawnedPlayer.Level;
            }
            
            Debug.LogWarning("스폰된 플레이어가 없어 레벨을 가져올 수 없습니다.");
            return 1; // 기본값 반환
        }

        /// <summary>
        /// 현재 플레이어의 경험치 반환
        /// </summary>
        public float GetPlayerCurrentExp()
        {
            if (_spawnedPlayer != null)
            {
                return _spawnedPlayer.CurrentExp;
            }
            
            return 0f;
        }

        /// <summary>
        /// 현재 플레이어의 최대 경험치 반환
        /// </summary>
        public float GetPlayerMaxExp()
        {
            if (_spawnedPlayer != null)
            {
                return _spawnedPlayer.MaxExp;
            }
            
            return 100f; // 기본값
        }

        /// <summary>
        /// 현재 플레이어의 경험치 진행률 반환 (0~1)
        /// </summary>
        public float GetPlayerExpProgress()
        {
            if (_spawnedPlayer != null)
            {
                return _spawnedPlayer.GetExpProgress();
            }
            
            return 0f;
        }

        /// <summary>
        /// 현재 코인 수 반환
        /// </summary>
        public int CoinCount()
        {
            return PlayerDataManagerDontdesytoy.Instance?.scritpableobjPlayerData?.currency1 ?? 0;
        }

        #endregion
    }
}