using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DogGuns_Games.vamsir
{
    public class VamserLikeUI : MonoBehaviour
    {
        #region 필드 및 변수

        [Header("<color=green>Text UI")] [SerializeField]
        private TMP_Text mobWaveText;

        [SerializeField] private TMP_Text coinText;
        [SerializeField] private TMP_Text mobCountText;
        [SerializeField] private TMP_Text playerLevelText;


        [SerializeField] private Button menuBtn;

        [Header("<color=green>Menu UI")] [SerializeField]
        private GameObject menuPanel;

        [SerializeField] private Button settingBtn;
        [SerializeField] private Button exitBtn;

        public List<GameObject> weaponUIList = new List<GameObject>();
        public List<GameObject> juListUIList = new List<GameObject>();


        [Header("<color=green>조이스틱")] [SerializeField]
        private VariableJoystick variableJoystick;

        [SerializeField] private Transform joystickTransform;

        VamserLikeGameManager _gameManager;
        private CancellationTokenSource _cancellationTokenSource;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            _gameManager = FindFirstObjectByType<VamserLikeGameManager>();
        }

        private void Start()
        {
            PlayStateManager.OnGameStart += GameStart;
            PlayStateManager.OnGamePause += Pause;
            PlayStateManager.OnGameResume += Resume;
        }

        private void OnDestroy()
        {
            _cancellationTokenSource?.Cancel();
            
            // 이벤트 구독 해제 추가
            PlayStateManager.OnGameStart -= GameStart;
            PlayStateManager.OnGamePause -= Pause;
            PlayStateManager.OnGameResume -= Resume;
        }

        #endregion

        #region 게임 상태 관리

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

        #endregion

        #region UI 설정 및 초기화

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
            menuBtn.onClick.AddListener(ToggleMenuAndJoystickVisibility);

            exitBtn.onClick.AddListener(ToggleMenuAndJoystickVisibility);

            settingBtn.onClick.AddListener(() =>
            {
                _gameManager.Open_OptionPopUp();
                settingBtn.enabled = false;
            });
        }

        #endregion

        #region UI 이벤트 및 동작

        private void ToggleMenuAndJoystickVisibility()
        {
            // 메뉴 패널의 현재 활성 상태의 반대로 설정합니다.
            bool isMenuPanelBecomingActive = !menuPanel.activeSelf;
            menuPanel.SetActive(isMenuPanelBecomingActive);

            // isMenuPanelBecomingActive 값에 따라 게임의 Pause/Resume 상태를 설정합니다.
            _gameManager.Open_MenuPopUp(isMenuPanelBecomingActive);

            // 메뉴 패널이 활성화되면 조이스틱을 비활성화하고, 그 반대의 경우도 마찬가지입니다.
            joystickTransform.gameObject.SetActive(!isMenuPanelBecomingActive);
        }

        #endregion

        #region UI 업데이트

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

        #endregion
    }
}