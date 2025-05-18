using DG.Tweening;
using TMPro;
using UnityEngine;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 뱀서라이크 게임의 기본 게임플레이 관리자
    /// </summary>
    public class VamserLikeGameManager : MonoBehaviour
    {
        #region 필드 및 변수

        private PlayerBase[] _playerBases;
        private Weaphon_base[] _weaphonBases;
        
        [Header("캐릭터 및 무기가 스폰 될시 부모 오브젝트")]
        [SerializeField] private GameObject inGameObjectParent;
        
        private readonly Vector3 _spawnPosition = Vector3.zero;
        
        [Header("옵션 팝업 매니저")]
        [SerializeField] private OptionPopupManager optionPopupManager;
        public SettingsData_oBJ settingsData;

        private ObjectPoolSpawner _objectPoolSpawner;

        #endregion

        #region Unity 라이프사이클

        /// <summary>
        /// 컴포넌트 초기화 및 이벤트 구독
        /// </summary>
        private void Awake()
        {
            _objectPoolSpawner = FindFirstObjectByType<ObjectPoolSpawner>();

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
        /// 게임 시작 이벤트 처리
        /// </summary>
        private void GameStart()
        {
            PlayStateManager.instance.isPlay = true;
            PlayerDataManagerDontdesytoy.Instance.scritpableobjPlayerData.nowPlayMObkillCOunt = 0;
            SpawnPlayer();
            Debug.Log("게임 시작: 플레이어 스폰 완료");
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
        /// 현재 선택된 플레이어와 무기를 스폰
        /// </summary>
        private void SpawnPlayer()
        {
            if (!PlayStateManager.instance.isPlay || inGameObjectParent == null)
            {
                Debug.LogWarning("게임이 플레이 상태가 아니거나 부모 오브젝트가 설정되지 않았습니다.");
                return;
            }

            // 무기 생성
            SpawnSelectedWeapon();
            
            // 캐릭터 생성
            SpawnSelectedCharacter();
        }

        /// <summary>
        /// 선택된 무기 스폰
        /// </summary>
        private void SpawnSelectedWeapon()
        {
            _weaphonBases = Resources.LoadAll<Weaphon_base>("Weaphon");
            
            if (_weaphonBases == null || _weaphonBases.Length == 0)
            {
                Debug.LogError("무기 프리팹을 로드할 수 없습니다.");
                return;
            }

            bool weaponFound = false;
            
            for (int i = 0; i < _weaphonBases.Length; i++)
            {
                if (PlayerDataManagerDontdesytoy.Instance.SelectWeaponIndex == _weaphonBases[i].weaphonIndex)
                {
                    Instantiate(_weaphonBases[i].gameObject, _spawnPosition, Quaternion.identity,
                        inGameObjectParent.transform);
                    weaponFound = true;
                    Debug.Log($"무기 스폰: 인덱스 {_weaphonBases[i].weaphonIndex}");
                    break;
                }
            }
            
            if (!weaponFound)
            {
                Debug.LogWarning($"인덱스 {PlayerDataManagerDontdesytoy.Instance.SelectWeaponIndex}의 무기를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// 선택된 캐릭터 스폰
        /// </summary>
        private void SpawnSelectedCharacter()
        {
            _playerBases = Resources.LoadAll<PlayerBase>("Player_Character");
            
            if (_playerBases == null || _playerBases.Length == 0)
            {
                Debug.LogError("캐릭터 프리팹을 로드할 수 없습니다.");
                return;
            }

            bool characterFound = false;
            
            for (int i = 0; i < _playerBases.Length; i++)
            {
                if (PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex == _playerBases[i].characterIndex)
                {
                    Instantiate(_playerBases[i].gameObject, _spawnPosition, Quaternion.identity,
                        inGameObjectParent.transform);
                    characterFound = true;
                    Debug.Log($"캐릭터 스폰: 인덱스 {_playerBases[i].characterIndex}");
                    break;
                }
            }
            
            if (!characterFound)
            {
                Debug.LogWarning($"인덱스 {PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex}의 캐릭터를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// 현재 스폰된 캐릭터와 무기를 변경
        /// </summary>
        public void ChangeCharacterAndWeapon_Spawn()
        {
            if (inGameObjectParent == null)
            {
                Debug.LogError("인게임 오브젝트 부모가 설정되지 않았습니다.");
                return;
            }
            
            // 현재 캐릭터와 무기 제거
            for (int i = 0; i < inGameObjectParent.transform.childCount; i++)
            {
                Transform child = inGameObjectParent.transform.GetChild(i);
                if (child.GetComponent<PlayerBase>() != null || child.GetComponent<Weaphon_base>() != null)
                {
                    Destroy(child.gameObject);
                }
            }

            // 새 캐릭터와 무기 스폰
            SpawnPlayer();
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
            int characterIndex = PlayerDataManagerDontdesytoy.Instance?.SelectCharacterIndex ?? 0;
            
            if (_playerBases == null || _playerBases.Length == 0 || characterIndex >= _playerBases.Length)
            {
                Debug.LogWarning("플레이어 베이스가 로드되지 않았거나 인덱스가 범위를 벗어났습니다.");
                return 1; // 기본값 반환
            }
            
            return _playerBases[characterIndex].Level;
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