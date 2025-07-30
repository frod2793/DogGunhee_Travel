using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;


namespace DogGuns_Games.vamsir
{
    public class VamserLikeUI : MonoBehaviour
    {
        
        //TODO: 게임 오버시 플레이어 이동 완전정지 및 조이스틱 비활성화 
        //게임오버시 획득 코인 플레이어 데이터에 저장후 동기화 
        
        #region 필드 및 변수
        //TODO 플레이어 레벨 플레이어 데이터에 동기화 
        [Header("<color=green>User Info UI")] [SerializeField]
        private TMP_Text LevelText;
        [SerializeField] private Slider playerLevelSlider;

        [Header("<color=green>Text UI")] [SerializeField]
        private TMP_Text mobWaveText;

        [SerializeField] private TMP_Text coinText;
        [SerializeField] private TMP_Text mobCountText;
        [SerializeField] private TMP_Text playerLevelText;
        [SerializeField] private Slider expSlider;
private int getcoinCount = 0;// 초기화 전 코인 정보를 담을 변수

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
            
            // 플레이어 경험치 이벤트 구독
            PlayerBase.OnExpChanged += OnPlayerExpChanged;
            PlayerBase.OnLevelUp += OnPlayerLevelUp;
        }

        private void OnDestroy()
        {
            _cancellationTokenSource?.Cancel();

            // 이벤트 구독 해제 추가
            PlayStateManager.OnGameStart -= GameStart;
            PlayStateManager.OnGamePause -= Pause;
            PlayStateManager.OnGameResume -= Resume;
            PlayStateManager.OnGameOver -= ShowGameOverPopup;
            
            // 플레이어 경험치 이벤트 구독 해제
            PlayerBase.OnExpChanged -= OnPlayerExpChanged;
            PlayerBase.OnLevelUp -= OnPlayerLevelUp;
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
        }

        #endregion

        #region UI 설정 및 초기화

        private void JoystickSetting()
        {
            if (variableJoystick == null)
            {
                variableJoystick = FindFirstObjectByType<VariableJoystick>();
            }

            if (SoundManager.Instance == null)
            {
                Debug.LogError("SoundManager.Instance가 null입니다. 씬에 SoundManager가 배치되어 있는지 확인하세요.");
                return;
            }
            var settingsData = SoundManager.Instance.settingsData;
            if (settingsData == null)
            {
                Debug.LogError("SoundManager의 settingsData가 null입니다. 인스펙터에서 할당되어 있는지 확인하세요.");
                return;
            }
            joystickTransform.localScale = new Vector3(settingsData.joystickSize,
                settingsData.joystickSize, 1);
            variableJoystick.SetMode((JoystickType)settingsData.joystickType);
            joystickTransform.position = new Vector3(settingsData.joystickPos.x,
                settingsData.joystickPos.y, 0);
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
            SceneLoader.Instance.LoadLobbyScene();
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

            // 조이스틱 비활성화 및 위치/상태 초기화
            joystickTransform.gameObject.SetActive(false);
            if (variableJoystick != null)
            {
                   variableJoystick.OnPointerUp(null); // 입력 해제
            }

            // UI 업데이트 중지
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// 게임 오버 UI의 텍스트들을 업데이트합니다.
        /// </summary>
        private void UpdateGameOverUI()
        {
            gameOverText.text = "Game Over";
            gameOverCoinText.text = $"Coins: {getcoinCount}";
            gameOverWaveText.text = $"Wave: {_gameManager.MobSpawnWave()}";
            gameOverMobCountText.text = $"Kills: {_gameManager.Mob_Count()}";
        }

        #endregion

        #region UI 업데이트

        private async UniTask UpdateUI(CancellationToken cancellationToken)
        {
            string lastWaveText = "";
            while (!cancellationToken.IsCancellationRequested)
            {
                string currentWaveText = $"Wave {_gameManager.MobSpawnWave()}";
                if (mobWaveText.text != currentWaveText)
                {
                    await WaveTextFadeEffect(currentWaveText);
                    lastWaveText = currentWaveText;
                }
                coinText.text = $"{_gameManager.CoinCount()}";
                getcoinCount = _gameManager.CoinCount();
                mobCountText.text = $"{_gameManager.Mob_Count()}";
                playerLevelText.text = $"Lv. {_gameManager.PlayerLevel()}";
                // User Info UI 업데이트
                UpdatePlayerLevelUI();
                await UniTask.DelayFrame(1, PlayerLoopTiming.FixedUpdate, cancellationToken);
            }
        }

        // DOTween을 이용한 mobWaveText 페이드 인/아웃 효과
        private async UniTask WaveTextFadeEffect(string waveText)
        {
            mobWaveText.text = waveText;
            mobWaveText.alpha = 0f;
            mobWaveText.gameObject.SetActive(true);
            await mobWaveText.DOFade(1f, 0.5f).AsyncWaitForCompletion(); // 페이드 인
            await UniTask.Delay(1000); // 1초간 표시
            await mobWaveText.DOFade(0f, 0.5f).AsyncWaitForCompletion(); // 페이드 아웃
            mobWaveText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 플레이어 레벨 UI를 업데이트합니다.
        /// </summary>
        private void UpdatePlayerLevelUI()
        {
            float currentLevel = _gameManager.PlayerLevel();
            LevelText.text = $"Lv. {currentLevel:F0}";
            
            // 실제 경험치 시스템 사용
            float expProgress = _gameManager.GetPlayerExpProgress();
            playerLevelSlider.value = expProgress;
            
            // 기존 expSlider도 같은 값으로 업데이트 (중복 슬라이더가 있는 경우)
            if (expSlider != null)
            {
                expSlider.value = expProgress;
            }
        }

        #endregion

        #region 플레이어 경험치 및 레벨 이벤트

        /// <summary>
        /// 플레이어 경험치가 변경되었을 때 호출되는 메서드입니다.
        /// </summary>
        private void OnPlayerExpChanged(float currentExp, float maxExp)
        {
            // 실시간으로 경험치 UI 업데이트
            UpdatePlayerLevelUI();
            
            // 디버그 로그 (선택사항)
          //  Debug.Log($"경험치 UI 업데이트: {currentExp:F1}/{maxExp:F1} ({(currentExp/maxExp)*100:F1}%)");
        }

        /// <summary>
        /// 플레이어 레벨업 시 호출되는 메서드입니다.
        /// </summary>
        private void OnPlayerLevelUp(float newLevel)
        {
            // 레벨업 시 UI 업데이트
            UpdatePlayerLevelUI();
            
            // 레벨업 축하 효과 (선택사항)
            ShowLevelUpEffect(newLevel);
            
            Debug.Log($"레벨업 UI 업데이트: 새 레벨 {newLevel}");
        }

        /// <summary>
        /// 레벨업 시 시각적 효과를 표시합니다.
        /// </summary>
        private void ShowLevelUpEffect(float newLevel)
        {
            // 레벨 텍스트에 간단한 효과 적용 (선택사항)
            if (LevelText != null)
            {
                // 크기 변화 효과 (DOTween 사용)
                LevelText.transform.localScale = Vector3.one * 1.2f;
                LevelText.transform.DOScale(Vector3.one, 0.3f)
                    .SetEase(Ease.OutBack);
                
                // 색상 변화 효과 (DOTween 사용)
                Color originalColor = LevelText.color;
                LevelText.color = Color.yellow;
                LevelText.DOColor(originalColor, 1f);
            }
        }

        #endregion
    }
}
