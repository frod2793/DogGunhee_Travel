
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DogGuns_Games.vamsir
{
    public class VamserLike_UI : MonoBehaviour
    {
        [Header("<color=green>Text UI")] [SerializeField]
        private TMP_Text mobWaveText;
        [SerializeField] private TMP_Text coinText;
        [SerializeField] private TMP_Text mobCountText;
        [SerializeField] private TMP_Text playerLevelText;

        
        [SerializeField] private Button MenuBtn;

        [Header("<color=green>Menu UI")] [SerializeField]
        private GameObject menuPanel;
        [SerializeField] private Button settingBtn;
        [SerializeField] private Button exitBtn;
        
        public List<GameObject> WeaponUIList = new List<GameObject>();
        public List<GameObject> JuListUIList = new List<GameObject>();
        
        
        
        [Header("<color=green>조이스틱")] [SerializeField]
        private VariableJoystick variableJoystick;

        [SerializeField] private Transform joystickTransform;

        VamserLike_GameManager _gameManager;
        private CancellationTokenSource _cancellationTokenSource;

        private void Awake()
        {
            _gameManager = FindFirstObjectByType<VamserLike_GameManager>();
        }

        private void Start()
        {
            Play_State.OnGameStart += GameStart;
            Play_State.OnGamePause += Pause;
            Play_State.OnGameResume += Resume;
        }
        private void OnDestroy()
        {
            _cancellationTokenSource?.Cancel();
        }
        private void GameStart()
        {
            BtnSetting();
            JoystickSetting();  
            _cancellationTokenSource = new CancellationTokenSource(); 
            UpdateUI(_cancellationTokenSource.Token).Forget();
        }

        private void Pause()
        {
            joystickTransform.gameObject.SetActive(false);
        }

        private void Resume()
        {
            joystickTransform.gameObject.SetActive(true);
            JoystickSetting();

            settingBtn.enabled = true;
        }


        private void JoystickSetting()
        {
            if (variableJoystick == null)
            {
                variableJoystick = FindFirstObjectByType<VariableJoystick>();
            }

            joystickTransform.localScale = new Vector3(_gameManager.settingsData.joystickSize,
                _gameManager.settingsData.joystickSize, 1);
            variableJoystick.SetMode((JoystickType)_gameManager.settingsData.joystickType);
            //todo : 조이스틱 위치 조정 필요 
            joystickTransform.position = new Vector3(_gameManager.settingsData.joystickPos.x,
                _gameManager.settingsData.joystickPos.y, 0);
        }

        private void BtnSetting()
        {
            settingBtn.onClick.AddListener(() =>
            {
                _gameManager.Open_OptionPopUp();
                settingBtn.enabled = false;
            });
        }

        private async UniTask UpdateUI(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (mobWaveText.text != $"Wave {_gameManager.MobSpawnWave()}")
                {
                    mobWaveText.text = $"Wave {_gameManager.MobSpawnWave()}";
                    _gameManager.WaveTextFadeEffect(mobWaveText);
                }
                coinText.text = $"{_gameManager.CoinCount()}";
                mobCountText.text = $"{_gameManager.Mob_Count()}";
                playerLevelText.text = $"Lv. {_gameManager.PlayerLevel()}";
                await UniTask.DelayFrame(1, PlayerLoopTiming.FixedUpdate, cancellationToken); 
            }
        }
        
     

     
    }
}