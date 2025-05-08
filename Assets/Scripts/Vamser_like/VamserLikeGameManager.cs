
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace DogGuns_Games.vamsir
{
    public class VamserLikeGameManager : MonoBehaviour
    {
        private Player_Base[] _playerBases;
        private Weaphon_base[] _weaphonBases;
        
        [Header("<color=green> 캐릭터 및 무기가 스폰 될시 부모 오브젝트")]
        [SerializeField] private GameObject inGameObjectParent;

        private Vector3 _spawnPosition = new Vector3(0, 0, 0);
        
        [Header("<color=green> 옵션 팝업 매니저")]
        [SerializeField] private OptionPopupManager optionPopupManager;
        public SettingsData_oBJ settingsData;

        private ObjectPoolSpawner _objectPoolSpawner;

        private void Awake()
        {
            _objectPoolSpawner = FindFirstObjectByType<ObjectPoolSpawner>();

            PlayStateManager.OnGameStart += GameStart;
            PlayStateManager.OnGamePause += Pause;
            PlayStateManager.OnGameResume += Resume;
        }


        private void GameStart()
        {
            PlayStateManager.instance.isPlay = true;
            PlayerDataManagerDontdesytoy.Instance.scritpableobjPlayerData.nowPlayMObkillCOunt = 0;
            SpawnPlayer();
        }

        private void Pause()
        {
            PlayStateManager.instance.isPlay = false;
        }

        private void Resume()
        {
            PlayStateManager.instance.isPlay = true;
        }

        private void SpawnPlayer()
        {
            if (PlayStateManager.instance.isPlay)
            {
                _weaphonBases = Resources.LoadAll<Weaphon_base>("Weaphon");
                for (int i = 0; i < _weaphonBases.Length; i++)
                {
                    if (PlayerDataManagerDontdesytoy.Instance.SelectWeaponIndex == _weaphonBases[i].weaphonIndex)
                    {
                        Instantiate(_weaphonBases[i].gameObject, _spawnPosition, Quaternion.identity,
                            inGameObjectParent.transform);
                    }
                }

                _playerBases = Resources.LoadAll<Player_Base>("Player_Character");

                for (int i = 0; i < _playerBases.Length; i++)
                {
                    if (PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex == _playerBases[i].characterIndex)
                    {
                        Instantiate(_playerBases[i].gameObject, _spawnPosition, Quaternion.identity,
                            inGameObjectParent.transform);
                    }
                }
            }
        }


        public void ChangeCharacterAndWeapon_Spawn()
        {
            for (int i = 0; i < inGameObjectParent.transform.childCount; i++)
            {
                //Player_Base 와 Weaphon_base 를 찾아서 삭제
                if (inGameObjectParent.transform.GetChild(i).GetComponent<Player_Base>() != null)
                {
                    Destroy(inGameObjectParent.transform.GetChild(i).gameObject);
                }
            }

            SpawnPlayer();
        }

        public void WaveTextFadeEffect(TMP_Text mobWaveText)
        {
            mobWaveText.DOFade(1, 1).SetEase(Ease.Linear).OnComplete(() =>
            {
                mobWaveText.DOFade(0, 1).SetEase(Ease.Linear).SetDelay(1f);
            });
         
        }

        public void Open_MenuPopUp(bool isPause)
        {
            PlayStateManager.instance.PlayState = isPause ? PlayStateManager.GameState.Pause : PlayStateManager.GameState.Resume;
        }
        public void Open_OptionPopUp()
        {
            Instantiate(optionPopupManager);
            optionPopupManager.gameObject.SetActive(true);
        }

        public int Mob_Count()
        {
            return PlayerDataManagerDontdesytoy.Instance.scritpableobjPlayerData.nowPlayMObkillCOunt;
        }

        public int MobSpawnWave()
        {
            return _objectPoolSpawner.MobSpawnWave;
        }

        public float PlayerLevel()
        {
            return _playerBases[PlayerDataManagerDontdesytoy.Instance.SelectCharacterIndex].Level;
        }

        public int CoinCount()
        {
            return PlayerDataManagerDontdesytoy.Instance.scritpableobjPlayerData.currency1;
        }
    }
}