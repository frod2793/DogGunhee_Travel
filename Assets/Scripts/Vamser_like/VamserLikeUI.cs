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
        #region 필드 및 변수

        [Header("<color=green>User Info UI")] [SerializeField]
        private TMP_Text LevelText;

        [SerializeField] private Slider playerLevelSlider;

        [Header("<color=green>Text UI")] [SerializeField]
        private TMP_Text mobWaveText;

        [SerializeField] private TMP_Text coinText;
        [SerializeField] private TMP_Text mobCountText;
        [SerializeField] private TMP_Text playerLevelText;
        [SerializeField] private Slider expSlider;
        private int getcoinCount = 0; // 초기화 전 코인 정보를 담을 변수

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

        [Header("<color=green>플레이어 자동 공격 활성화 토글 ")] [SerializeField]
        private Toggle autoAttackToggle;

        [SerializeField] private Transform joystickTransform;
        VamserLikeGameManager _gameManager;
        private CancellationTokenSource _cancellationTokenSource;
        private Tween _expSliderTween;
        
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

            _gameManager = FindFirstObjectByType<VamserLikeGameManager>();
            // 자동 공격 토글 이벤트 연결
            autoAttackToggle.onValueChanged.AddListener(isOn =>
            {
                var playerController = FindFirstObjectByType<VamPlayerControll>();
                if (playerController != null)
                {
                    LogManager.Log($"VamPlayerControll을 찾았습니다. 자동 공격 상태를 {isOn}(으)로 변경합니다.", LogManager.LogCategory.VamserLikeUI);
                    playerController.AutoAttackEnabledByToggle = isOn;
                }
                else
                {
                    LogManager.LogError("VamPlayerControll을 찾을 수 없습니다! 플레이어 오브젝트가 활성화되어 있고 VamPlayerControll 컴포넌트가 추가되었는지 확인하세요.", LogManager.LogCategory.VamserLikeUI);
                }
            });
        }

        private void OnDestroy()
        {
            _cancellationTokenSource?.Cancel();
            _expSliderTween?.Kill();

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
            
            // UI 초기 상태 설정
            // 레벨 텍스트 초기화
            string initialLevelText = $"Lv. {_gameManager.PlayerLevel():F0}";
            LevelText.text = initialLevelText;
            playerLevelText.text = initialLevelText;
            // 경험치 슬라이더 초기화
            playerLevelSlider.value = _gameManager.GetPlayerExpProgress();
            if (expSlider != null) expSlider.value = _gameManager.GetPlayerExpProgress();
            
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
                LogManager.LogError("SoundManager.Instance가 null입니다. 씬에 SoundManager가 배치되어 있는지 확인하세요.",
                    LogManager.LogCategory.VamserLikeUI);
                return;
            }

            var settingsData = SoundManager.Instance.settingsData;
            if (settingsData == null)
            {
                LogManager.LogError("SoundManager의 settingsData가 null입니다. 인스펙터에서 할당되어 있는지 확인하세요.",
                    LogManager.LogCategory.VamserLikeUI);
                return;
            }

            joystickTransform.localScale = new Vector3(settingsData.joystickSize,
                settingsData.joystickSize, 1);
            variableJoystick.SetMode((JoystickType)settingsData.joystickType);
            var rectTransform = joystickTransform as RectTransform;
            if (rectTransform != null)
                rectTransform.anchoredPosition = settingsData.joystickPos;
        }


        private void SoundSetting()
        {
            SoundManager.Instance.LoadSoundSetting();
        }


        private void BtnSetting()
        {
            menuBtn.onClick.AddListener(PausePopUp);

            exitBtn.onClick.AddListener(PausePopUp);

            settingBtn.onClick.AddListener(() => { _gameManager.Open_OptionPopUp(); });

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

            autoAttackToggle.isOn = false; // 자동 공격 토글 비활성화

            // 취소 토큰 소스가 있다면 취소합니다.
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

        #endregion

        #region 플레이어 경험치 및 레벨 이벤트

        /// <summary>
        /// 플레이어 경험치가 변경되었을 때 호출되는 메서입니다.
        /// </summary>
        private void OnPlayerExpChanged(float currentExp, float maxExp)
        {
            float progress = (maxExp > 0) ? currentExp / maxExp : 0;

            // 기존 트윈을 중지하고 새 애니메이션 시작
            _expSliderTween?.Kill();
            _expSliderTween = playerLevelSlider.DOValue(progress, 0.2f).SetEase(Ease.OutQuad);

            // 중복 슬라이더가 있는 경우 함께 업데이트
            if (expSlider != null)
            {
                // 이 슬라이더는 별도의 트윈으로 관리할 필요가 거의 없으므로, Kill 없이 바로 실행합니다.
                expSlider.DOValue(progress, 0.2f).SetEase(Ease.OutQuad);
            }
            
            // 디버그 로그 (선택사항)
            LogManager.Log($"경험치 UI 업데이트: {currentExp:F1}/{maxExp:F1} ({progress * 100:F1}%)",
                LogManager.LogCategory.VamserLikeUI);
        }

        /// <summary>
        /// 플레이어 레벨업 시 호출되는 메서드입니다.
        /// </summary>
        private void OnPlayerLevelUp(float newLevel)
        {
            // 레벨 텍스트 업데이트
            string levelString = $"Lv. {newLevel:F0}";
            LevelText.text = levelString;
            playerLevelText.text = levelString;
            
            // 레벨업 축하 효과 (선택사항)
            ShowLevelUpEffect(newLevel);
            LogManager.Log($"레벨업 UI 업데이트: 새 레벨 {newLevel}", LogManager.LogCategory.VamserLikeUI);
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