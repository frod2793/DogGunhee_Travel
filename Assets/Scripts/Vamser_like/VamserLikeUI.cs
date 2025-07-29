using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
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
        [SerializeField] private Slider expSlider;


        [Header("<color=green>Menu UI")] [SerializeField]
        private Button menuBtn;

        [SerializeField] private GameObject menuPanel;
        [SerializeField] private Button settingBtn;
        [SerializeField] private Button exitBtn;
        public List<GameObject> weaponUIList = new List<GameObject>();
        public List<GameObject> juListUIList = new List<GameObject>();

        [Header("<color=green>GameOver UI")] [SerializeField]
        private GameObject gameOverPanel;

        [SerializeField] private Button gameOverExitBtn;
        [SerializeField] private Button gameOverRestartBtn;
        [SerializeField] private TMP_Text gameOverText;
        [SerializeField] private TMP_Text gameOverCoinText;
        [SerializeField] private TMP_Text gameOverWaveText;
        [SerializeField] private TMP_Text gameOverMobCountText;


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
            PlayStateManager.OnGameOver += ShowGameOverPopup;
        }

        private void OnDestroy()
        {
            _cancellationTokenSource?.Cancel();

            // 이벤트 구독 해제 추가
            PlayStateManager.OnGameStart -= GameStart;
            PlayStateManager.OnGamePause -= Pause;
            PlayStateManager.OnGameResume -= Resume;
            PlayStateManager.OnGameOver -= ShowGameOverPopup;
        }

        #endregion

        #region 게임 상태 관리

        private void GameStart()
        {
            BtnSetting();
            JoystickSetting();
            SoundSetting(); // 사운드 설정 추가
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


        private void SoundSetting()
        {
            SoundManager.Instance.LoadSoundSetting();
        }


        private void BtnSetting()
        {
            menuBtn.onClick.AddListener(PausePopUp);

            exitBtn.onClick.AddListener(PausePopUp);

            settingBtn.onClick.AddListener(() =>
            {
                _gameManager.Open_OptionPopUp();
                settingBtn.enabled = false;
            });

            // 게임 오버 버튼 설정
            gameOverExitBtn.onClick.AddListener(GameOverExit);
            gameOverRestartBtn.onClick.AddListener(GameOverRestart);
        }

        #endregion

        #region UI 이벤트 및 동작

        private void PausePopUp()
        {
            // 메뉴 패널의 현재 활성 상태의 반대로 설정합니다.
            bool isMenuPanelBecomingActive = !menuPanel.activeSelf;
            menuPanel.SetActive(isMenuPanelBecomingActive);

            // isMenuPanelBecomingActive 값에 따라 게임의 Pause/Resume 상태를 설정합니다.
            _gameManager.Open_MenuPopUp(isMenuPanelBecomingActive);

            // 메뉴 패널이 활성화되면 조이스틱을 비활성화하고, 그 반대의 경우도 마찬가지입니다.
            joystickTransform.gameObject.SetActive(!isMenuPanelBecomingActive);
        }

        private void GameOverExit()
        {
            // 게임 오버 패널을 비활성화하고, 로비 씬으로 이동
            gameOverPanel.SetActive(false);
            SceneLoader.Instace.LoadLobbyScene();
        }

        private void GameOverRestart()
        {
            // 게임 오버 패널을 비활성화하고, 게임을 다시 시작
            gameOverPanel.SetActive(false);
            // 씬을 다시 로드하여 게임을 재시작
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        #endregion

        #region 게임 오버

        /// <summary>
        /// 게임 오버 팝업을 표시합니다.
        /// </summary>
        public void ShowGameOverPopup()
        {
            // 게임 오버 UI 데이터 업데이트
            UpdateGameOverUI();

            // 게임 오버 패널 활성화
            gameOverPanel.SetActive(true);

            // 조이스틱 비활성화
            joystickTransform.gameObject.SetActive(false);

            // UI 업데이트 중지
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// 게임 오버 UI의 텍스트들을 업데이트합니다.
        /// </summary>
        private void UpdateGameOverUI()
        {
            gameOverText.text = "Game Over";
            gameOverCoinText.text = $"Coins: {_gameManager.CoinCount()}";
            gameOverWaveText.text = $"Wave: {_gameManager.MobSpawnWave()}";
            gameOverMobCountText.text = $"Kills: {_gameManager.Mob_Count()}";
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