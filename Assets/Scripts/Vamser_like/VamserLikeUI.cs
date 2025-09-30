using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Random = UnityEngine.Random;


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

        [Header("설정 데이터")]
        [SerializeField] private SettingsData_oBJ settingsData;

        [Tooltip("조이스틱의 위치와 크기를 제어하는 RectTransform 입니다.")]
        [SerializeField] private RectTransform joystickTransform;
        VamserLikeGameManager _gameManager;
        private CancellationTokenSource _cancellationTokenSource;
        private Tween _expSliderTween;

        /// <summary>
        /// 레벨업 시 표시되는 스킬 선택 UI입니다.
        /// 3개의 랜덤 스킬이 제시되며, 선택 시 팝업이 닫힙니다.
        /// 리프레시 버튼으로 선택지를 다시 뽑을 수 있습니다.
        /// </summary>
        [Header("Skill Selection UI")]
        [Tooltip("스킬 선택 팝업의 최상위 패널입니다.")]
        [SerializeField] private GameObject skillSelectionPanel;
        [Tooltip("스킬 선택지를 다시 뽑는 리프레시 버튼입니다.")]
        [SerializeField] private Button refreshButton;
        [Tooltip("동적으로 생성될 스킬 선택 버튼의 프리팹입니다.")]
        [SerializeField] private SelectSkillBtnPrefab skillSelectionButtonPrefab;
        [Tooltip("생성된 스킬 선택 버튼들이 위치할 부모 컨테이너입니다.")]
        [SerializeField] private GameObject skillButtonContainer;
        [SerializeField] TMP_Text countdownText;
        [SerializeField] private Slider countDownslider;
        
        
        [Header("Skill Data")]
        [Tooltip("게임 내 모든 스킬 정보가 담긴 데이터베이스입니다.")]
        [SerializeField] private SkillDatabase skillDatabase;

        private int _pendingSkillSelections = 0; // 처리 대기 중인 스킬 선택 횟수
        private bool _isSkillSelectionActive = false; // 스킬 선택 UI가 활성화되어 있는지 여부
        private CancellationTokenSource _skillSelectionTimerCts; // 자동 스킬 선택 타이머를 위한 CancellationTokenSource

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            // 싱글톤 인스턴스를 사용하여 더 효율적이고 안정적으로 참조를 가져옵니다.
            _gameManager = VamserLikeGameManager.Instance; // Instance 프로퍼티가 null 체크를 담당합니다.
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
            SettingsData_oBJ.OnSettingsChanged += JoystickSetting; // 설정 변경 이벤트 구독

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
            
            // 리프레시 버튼 이벤트 연결
            refreshButton.onClick.AddListener(GenerateSkillChoices);
        }

        private void OnDestroy()
        {
            _cancellationTokenSource?.Cancel();
            _expSliderTween?.Kill();
            _skillSelectionTimerCts?.Cancel(); // 컴포넌트 파괴 시 타이머 취소

            // 이벤트 구독 해제 추가
            PlayStateManager.OnGameStart -= GameStart;
            PlayStateManager.OnGamePause -= Pause;
            PlayStateManager.OnGameResume -= Resume;
            PlayStateManager.OnGameOver -= ShowGameOverPopup;

            // 플레이어 경험치 이벤트 구독 해제
            PlayerBase.OnExpChanged -= OnPlayerExpChanged;
            PlayerBase.OnLevelUp -= OnPlayerLevelUp;
            SettingsData_oBJ.OnSettingsChanged -= JoystickSetting; // 설정 변경 이벤트 구독 해제
            
            // 리프레시 버튼 이벤트 해제
            refreshButton.onClick.RemoveListener(GenerateSkillChoices);
        }

        #endregion

        #region 게임 상태 관리

        private void GameStart()
        {
            // 게임 시작 시, 파일에 저장된 최신 설정값을 명시적으로 불러옵니다.
            settingsData.LoadSettings();

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

            if (settingsData == null)
            {
                LogManager.LogError("VamserLikeUI에 SettingsData가 할당되지 않았습니다. 인스펙터에서 할당해주세요.",
                    LogManager.LogCategory.VamserLikeUI);
                return;
            }
            
            // OnSettingsChanged 이벤트는 이미 메모리의 settingsData가 업데이트된 후에 호출됩니다.
            // 여기서 LoadSettings()를 다시 호출하면 파일의 이전 데이터로 덮어쓰여 문제가 발생하므로 제거합니다.

            joystickTransform.localScale = new Vector3(settingsData.joystickSize,
                settingsData.joystickSize, 1);
            variableJoystick.SetMode((JoystickType)settingsData.joystickType); 
            
            joystickTransform.anchoredPosition = settingsData.joystickPos;

            CheckJoystickVisibilityAndResetIfOutside();
        }

        /// <summary>
        /// 조이스틱이 화면 밖에 있는지 확인하고, 밖에 있다면 기본 위치로 재설정합니다.
        /// Screen Space - Camera 렌더 모드를 기준으로 동작합니다.
        /// </summary>
        private void CheckJoystickVisibilityAndResetIfOutside()
        {
            var canvas = joystickTransform.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // ScreenSpaceOverlay는 이 로직으로 처리할 수 없으므로, 필요 시 별도 구현
                return;
            }

            var camera = canvas.worldCamera;
            if (camera == null)
            {
                LogManager.LogWarning("조이스틱 가시성 검사를 위한 렌더 카메라를 찾을 수 없습니다.", LogManager.LogCategory.VamserLikeUI);
                return;
            }

            Rect screenRect = new Rect(0, 0, Screen.width, Screen.height);
            Vector3[] joystickCorners = new Vector3[4];
            joystickTransform.GetWorldCorners(joystickCorners);

            bool isVisible = joystickCorners.Any(corner => screenRect.Contains(camera.WorldToScreenPoint(corner)));

            if (!isVisible)
            {
                joystickTransform.anchoredPosition = new Vector2(300, 300); // 안전한 기본 위치
                LogManager.LogWarning("저장된 조이스틱 위치가 화면 밖이라 기본 위치로 재설정합니다.", LogManager.LogCategory.VamserLikeUI);
            }
        }

        private void SoundSetting()
        {
            SoundManager.Instance.LoadSoundSetting();
        }


        private void BtnSetting()
        {
            menuBtn.onClick.AddListener(PausePopUp);

            exitBtn.onClick.AddListener(PausePopUp);

            settingBtn.onClick.AddListener(() => { _gameManager.OpenOptionPopup(); });

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
            _gameManager.SetMenuPopupState(isMenuPanelBecomingActive);

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
            // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
            string levelString = $"Lv. {newLevel:F0}";
            LevelText.text = levelString;
            playerLevelText.text = levelString;
            
            // 레벨업 축하 효과 (선택사항)
            ShowLevelUpEffect(newLevel);
            LogManager.Log($"레벨업 UI 업데이트: 새 레벨 {newLevel}", LogManager.LogCategory.VamserLikeUI);
            
            // 레벨 2부터 스킬 선택 UI를 표시합니다.
            if (newLevel >= 2)
            {
                _pendingSkillSelections++;
                LogManager.Log($"레벨업 이벤트 수신. 보류 중인 스킬 선택: {_pendingSkillSelections}", LogManager.LogCategory.VamserLikeUI);

                // 스킬 선택이 진행 중이 아닐 때만 새로운 프로세스를 시작합니다.
                if (!_isSkillSelectionActive)
                {
                    ProcessSkillSelectionQueue();
                }
            }
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
        
        #region 스킬 선택 UI

        /// <summary>
        /// 보류 중인 스킬 선택 큐를 처리합니다.
        /// </summary>
        private void ProcessSkillSelectionQueue()
        {
            if (_pendingSkillSelections > 0)
            {
                ShowSkillSelectionPanel();
            }
        }

        /// <summary>
        /// 스킬 선택 패널을 표시하고 게임을 일시정지합니다.
        /// </summary>
        private void ShowSkillSelectionPanel()
        {
            _gameManager.SetMenuPopupState(true); // 게임 일시정지
            _isSkillSelectionActive = true;
            skillSelectionPanel.SetActive(true);
            GenerateSkillChoices();
            StartAutoSelectionTimer(); // 자동 선택 타이머 시작
        }

        /// <summary>
        /// 6초 후 랜덤 스킬을 선택하는 타이머를 시작하고, UI에 카운트다운을 표시합니다.
        /// </summary>
        private void StartAutoSelectionTimer()
        {
            _skillSelectionTimerCts?.Cancel(); // 이전 타이머가 있다면 취소
            _skillSelectionTimerCts = new CancellationTokenSource();

            CountdownAndAutoSelect(_skillSelectionTimerCts.Token).Forget();
        }

        /// <summary>
        /// 카운트다운을 UI에 표시하고, 시간이 다 되면 랜덤 스킬을 선택합니다.
        /// </summary>
        private async UniTaskVoid CountdownAndAutoSelect(CancellationToken cancellationToken)
        {
            try
            {
                const float duration = 6.0f;
                float timer = duration;

                countdownText.gameObject.SetActive(true);
                countDownslider.gameObject.SetActive(true);
                countDownslider.value = 1f;

                while (timer > 0.01f) // 0에 가까워지면 루프 종료
                {
                    countdownText.text = Mathf.CeilToInt(timer).ToString();
                    countDownslider.value = timer / duration;
                    
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                    timer -= Time.deltaTime;
                }

                // 시간이 다 되면 0을 표시하고 자동 선택 실행
                countdownText.text = "0";
                countDownslider.value = 0;
                // 애니메이션이 끝날 때까지 기다린 후 다음 로직을 실행합니다.
                await SelectRandomSkill();
            }
            catch (OperationCanceledException)
            {
                // 사용자가 선택하여 타이머가 취소된 경우, 정상적인 동작입니다.
                LogManager.Log("스킬 선택 타이머가 사용자에 의해 취소되었습니다.", LogManager.LogCategory.VamserLikeUI);
            }
            finally
            {
                // 타이머가 끝나거나 취소되면 텍스트를 비활성화합니다.
                if (countdownText != null)
                {
                    countdownText.gameObject.SetActive(false);
                }
                if (countDownslider != null)
                {
                    countDownslider.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 현재 표시된 스킬 중 하나를 랜덤으로 선택합니다.
        /// </summary>
        private async UniTask SelectRandomSkill()
        {
            if (skillButtonContainer.transform.childCount > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, skillButtonContainer.transform.childCount);
                var randomButton = skillButtonContainer.transform.GetChild(randomIndex).GetComponent<SelectSkillBtnPrefab>();
                if (randomButton != null)
                {
                    LogManager.Log("시간 초과! 랜덤 스킬을 자동으로 선택합니다.", LogManager.LogCategory.VamserLikeUI);
                    // 선택 애니메이션을 재생하고 끝날 때까지 기다립니다.
                    await randomButton.PlaySelectionAnimation();
                    randomButton.TriggerSelectionCallback(); // 애니메이션 후 콜백 호출
                }
            }
        }

        /// <summary>
        /// 랜덤 스킬 선택지를 생성하여 UI에 표시합니다.
        /// </summary>
        private void GenerateSkillChoices()
        {
            // 1. 기존 버튼들 제거
            foreach (Transform child in skillButtonContainer.transform)
            {
                Destroy(child.gameObject);
            }

            // 2. 전체 스킬 목록에서 랜덤으로 3개를 중복 없이 선택합니다.
            // 이전에 선택한 스킬도 다시 나올 수 있습니다.
            var selectedSkills = skillDatabase.allSkills
                .OrderBy(skill => Random.value) // 리스트를 랜덤하게 섞습니다.
                .Take(3)                        // 상위 3개를 선택합니다.
                .ToList();
            // 3. 선택된 스킬들로 버튼 생성
            foreach (var skill in selectedSkills)
            {
                var skillButtonInstance = Instantiate(skillSelectionButtonPrefab, skillButtonContainer.transform);
                skillButtonInstance.Setup(skill, OnSkillSelected);
            }
        }

        /// <summary>
        /// 스킬 버튼이 클릭되었을 때 호출되는 콜백 메서드입니다.
        /// </summary>
        /// <param name="selectedSkill">선택된 스킬 데이터</param>
        private void OnSkillSelected(SkillData selectedSkill)
        {
            _skillSelectionTimerCts?.Cancel(); // 사용자가 선택했으므로 타이머 취소

            // TODO: 실제 스킬 적용 로직 (예: 플레이어 스탯 강화, 새 무기 추가 등)
            // 선택된 스킬을 인게임 인벤토리에 직접 추가합니다.
            if (_gameManager.spawnedPlayer != null)
            {
                var playerRenderer = _gameManager.spawnedPlayer.GetComponent<SpriteRenderer>();
                EffectManager.Instance.PlayLevelUpEffect(playerRenderer);
            }
            
            DogGuns_Games.Lobby.InventoryDataManagerDontdestory.Instance.AddInGameSkill(selectedSkill);
            _pendingSkillSelections--;
            LogManager.Log($"스킬 선택 완료. 남은 선택: {_pendingSkillSelections}", LogManager.LogCategory.VamserLikeUI);

            if (_pendingSkillSelections > 0)
            {
                // 아직 선택할 스킬이 남았다면, 목록만 새로고침합니다.
                GenerateSkillChoices();
                StartAutoSelectionTimer(); // 다음 선택을 위한 타이머 다시 시작
            }
            else
            {
                // 모든 선택이 끝났으면, 패널을 닫고 게임을 재개합니다.
                _skillSelectionTimerCts?.Cancel();
                _skillSelectionTimerCts = null;
                skillSelectionPanel.SetActive(false);
                _isSkillSelectionActive = false;
                _gameManager.SetMenuPopupState(false); // 게임 재개
            }
        }
        #endregion
    }
}