using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
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
        public List<Image> juListUIList = new List<Image>();

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

        [Header("설정 데이터")] [SerializeField] private SettingsData_oBJ settingsData;

        [Tooltip("조이스틱의 위치와 크기를 제어하는 RectTransform 입니다.")] [SerializeField]
        private RectTransform joystickTransform;

        VamserLikeGameManager _gameManager;
        private CancellationTokenSource _cancellationTokenSource;
        private Tween _expSliderTween;

        // WebGL 메모리 최적화를 위한 변수
        private int _lastWave = -1; // Wave UI 업데이트 최적화를 위한 변수

        private readonly List<SelectSkillBtnPrefab>
            _skillButtonPool = new List<SelectSkillBtnPrefab>(); // 스킬 버튼 오브젝트 풀링

        private readonly List<SkillData> _skillChoices = new List<SkillData>(3); // 스킬 선택 최적화를 위한 리스트

        private readonly List<SkillData> _acquiredAccessorySkills = new List<SkillData>(); // 획득한 장신구 스킬 목록
        private int _nextJuListSlotIndex = 0; // 장신구 UI 슬롯 업데이트 최적화를 위한 인덱스

        /// <summary>
        /// 레벨업 시 표시되는 스킬 선택 UI입니다.
        /// 3개의 랜덤 스킬이 제시되며, 선택 시 팝업이 닫힙니다.
        /// 리프레시 버튼으로 선택지를 다시 뽑을 수 있습니다.
        /// </summary>
        [Header("Skill Selection UI")] [Tooltip("스킬 선택 팝업의 최상위 패널입니다.")] [SerializeField]
        private GameObject skillSelectionPanel;

        [Tooltip("스킬 선택지를 다시 뽑는 리프레시 버튼입니다.")] [SerializeField]
        private Button refreshButton;

        [Tooltip("동적으로 생성될 스킬 선택 버튼의 프리팹입니다.")] [SerializeField]
        private SelectSkillBtnPrefab skillSelectionButtonPrefab;

        [Tooltip("생성된 스킬 선택 버튼들이 위치할 부모 컨테이너입니다.")] [SerializeField]
        private GameObject skillButtonContainer;

        [SerializeField] TMP_Text countdownText;
        [SerializeField] private Slider countDownslider;


        [Header("Skill Data")] [Tooltip("게임 내 모든 스킬 정보가 담긴 데이터베이스입니다.")] [SerializeField]
        private SkillDatabase skillDatabase;

        private int _pendingSkillSelections = 0; // 처리 대기 중인 스킬 선택 횟수
        private bool _isSkillSelectionActive = false; // 스킬 선택 UI가 활성화되어 있는지 여부
        private CancellationTokenSource _skillSelectionTimerCts; // 자동 스킬 선택 타이머를 위한 CancellationTokenSource

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            // 싱글톤 인스턴스를 사용하여 더 효율적이고 안정적으로 참조를 가져옵니다.
            _gameManager = VamserLikeGameManager.Instance; // Instance 프로퍼티가 null 체크를 담당합니다.

#if UNITY_STANDALONE || UNITY_WEBGL|| UNITY_STANDALONE_OSX
            // PC 및 WebGL 환경에서 창 크기를 720x1280으로 고정합니다.
            Screen.SetResolution(720, 1280, false);
            LogManager.Log("PC/WebGL 환경으로 감지되어 화면 크기를 720x1280으로 설정합니다.", LogManager.LogCategory.VamserLikeUI);
#endif
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
                    LogManager.Log($"VamPlayerControll을 찾았습니다. 자동 공격 상태를 {isOn}(으)로 변경합니다.",
                        LogManager.LogCategory.VamserLikeUI);
                    playerController.AutoAttackEnabledByToggle = isOn;
                }
                else
                {
                    LogManager.LogError(
                        "VamPlayerControll을 찾을 수 없습니다! 플레이어 오브젝트가 활성화되어 있고 VamPlayerControll 컴포넌트가 추가되었는지 확인하세요.",
                        LogManager.LogCategory.VamserLikeUI);
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
            InitializeJuListUI(); // 스킬 UI 리스트 초기화
            _acquiredAccessorySkills.Clear(); // 게임 시작 시 획득한 스킬 목록 초기화
            _cancellationTokenSource = new CancellationTokenSource();

            // UI 초기 상태 설정
            // 레벨 텍스트 초기화 (메모리 최적화)
            LevelText.SetText("Lv. {0}", (int)_gameManager.PlayerLevel());
            playerLevelText.SetText("Lv. {0}", (int)_gameManager.PlayerLevel());

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

        /// <summary>
        /// 보조무기 UI 리스트를 초기화하여 모든 슬롯을 투명하게 만듭니다.
        /// </summary>
        private void InitializeJuListUI()
        {
            foreach (var image in juListUIList)
            {
                if (image == null) continue;
                var color = image.color;
                color.a = 0f;
                image.color = color;
                // 아이콘도 초기화하여 이전 게임의 잔상이 남지 않도록 합니다.
                image.sprite = null;
            }

            _nextJuListSlotIndex = 0; // 인덱스 초기화
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

            // 메뉴 패널이 활성화될 때만 장신구 UI를 업데이트합니다.
            if (isMenuPanelBecomingActive) RefreshJuListDisplay();
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
        /// 게임 오버 UI의 텍스트들을 업데이트합니다. (메모리 최적화)
        /// </summary>
        private void UpdateGameOverUI()
        {
            gameOverText.text = "Game Over";
            gameOverCoinText.SetText("Coins: {0}", getcoinCount);
            gameOverWaveText.SetText("Wave: {0}", _gameManager.MobSpawnWave());
            gameOverMobCountText.SetText("Kills: {0}", _gameManager.Mob_Count());
        }

        #endregion

        #region UI 업데이트

        private async UniTask UpdateUI(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int currentWave = _gameManager.MobSpawnWave();
                if (_lastWave != currentWave)
                {
                    _lastWave = currentWave;
                    // 문자열 할당은 Wave가 변경될 때만 발생하도록 최적화
                    await WaveTextFadeEffect($"Wave {currentWave}");
                }

                // SetText를 사용하여 숫자 업데이트 시 문자열 할당 방지
                coinText.SetText("{0}", _gameManager.CoinCount());
                getcoinCount = _gameManager.CoinCount();
                mobCountText.SetText("{0}", _gameManager.Mob_Count());
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

            // 디버그 로그 (메모리 최적화)
            // WebGL 환경에서는 GC 부담을 줄이기 위해 릴리즈 빌드에서 이 로그를 비활성화하는 것이 좋습니다.
            // LogManager.Log($"경험치 UI 업데이트: {currentExp:F1}/{maxExp:F1} ({progress * 100:F1}%)",
            //     LogManager.LogCategory.VamserLikeUI);
        }

        /// <summary>
        /// 플레이어 레벨업 시 호출되는 메서드입니다.
        /// </summary>
        private void OnPlayerLevelUp(float newLevel)
        {
            // 레벨 텍스트 업데이트 (문자열 할당 최적화)
            LevelText.SetText("Lv. {0}", (int)newLevel);
            playerLevelText.SetText("Lv. {0}", (int)newLevel);

            // 레벨업 축하 효과 (선택사항)
            ShowLevelUpEffect(newLevel);
            LogManager.Log($"레벨업 UI 업데이트: 새 레벨 {newLevel}", LogManager.LogCategory.VamserLikeUI);

            // 레벨 2부터 스킬 선택 UI를 표시합니다.
            if (newLevel >= 2)
            {
                _pendingSkillSelections++;
                LogManager.Log($"레벨업 이벤트 수신. 보류 중인 스킬 선택: {_pendingSkillSelections}",
                    LogManager.LogCategory.VamserLikeUI);

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
            LogManager.Log("카운트다운 시작.", LogManager.LogCategory.VamserLikeUI);
            const float duration = 6.0f;
            float timer = duration;

            countdownText.gameObject.SetActive(true);
            countDownslider.gameObject.SetActive(true);
            countDownslider.value = 1f;

            // 타이머가 진행 중이고, 취소 요청이 없을 때만 루프를 실행합니다.
            while (timer > 0.01f && !cancellationToken.IsCancellationRequested)
            {
                countdownText.text = Mathf.CeilToInt(timer).ToString();
                countDownslider.value = timer / duration;

                // await UniTask.Yield()는 취소 시 예외를 던지므로, 안전하게 다음 프레임까지 기다립니다.
                await UniTask.NextFrame(cancellationToken);
                timer -= Time.deltaTime;
            }

            LogManager.Log($"카운트다운 루프 종료. 취소 상태: {cancellationToken.IsCancellationRequested}",
                LogManager.LogCategory.VamserLikeUI);

            // 타이머가 취소되지 않고 정상적으로 완료되었을 때만 자동 선택을 실행합니다.
            if (!cancellationToken.IsCancellationRequested)
            {
                LogManager.Log("카운트다운 정상 완료. 자동 선택을 시작합니다.", LogManager.LogCategory.VamserLikeUI);
                // 시간이 다 되면 0을 표시하고 자동 선택 실행
                countdownText.text = "0";
                countDownslider.value = 0;
                // 애니메이션이 끝날 때까지 기다린 후 다음 로직을 실행합니다.
                await SelectRandomSkill();
            }
            else
            {
                LogManager.Log("스킬 선택 타이머가 사용자에 의해 취소되었습니다.", LogManager.LogCategory.VamserLikeUI);
            }
        }

        /// <summary>
        /// 현재 표시된 스킬 중 하나를 랜덤으로 선택합니다.
        /// </summary>
        private async UniTask SelectRandomSkill()
        {
            LogManager.Log("SelectRandomSkill 진입.", LogManager.LogCategory.VamserLikeUI);
            // UI의 childCount 대신, 현재 선택지로 채워진 데이터 리스트를 직접 확인하여 안정성을 높입니다.
            if (_skillChoices.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, _skillChoices.Count);
                SkillData randomSkill = _skillChoices[randomIndex];

                LogManager.Log("시간 초과! 랜덤 스킬을 자동으로 선택합니다.", LogManager.LogCategory.VamserLikeUI);
                LogManager.Log($"자동 선택될 스킬: {randomSkill.skillName}", LogManager.LogCategory.VamserLikeUI);

                // 데이터와 일치하는 활성화된 버튼을 찾아서 애니메이션을 재생합니다.
                var targetButton = _skillButtonPool.FirstOrDefault(btn =>
                    btn.gameObject.activeSelf && btn.GetCurrentSkillData() == randomSkill);

                if (targetButton != null)
                {
                    // 선택 애니메이션을 재생하고 끝날 때까지 기다립니다.
                    await targetButton.PlaySelectionAnimation();
                }

                // OnSkillSelected를 호출하여 사용자 선택과 동일한 흐름을 타도록 합니다.
                OnSkillSelected(randomSkill); // 애니메이션 후 콜백 호출
            }
        }

        /// <summary>
        /// 랜덤 스킬 선택지를 생성하여 UI에 표시하고, 카운트다운을 초기화합니다. (메모리 최적화)
        /// </summary>
        private void GenerateSkillChoices()
        {
            LogManager.Log("새로운 스킬 선택지 생성 및 타이머 재시작.", LogManager.LogCategory.VamserLikeUI);
            // 1. 새로운 선택지를 생성하기 전에, 카운트다운 타이머를 먼저 재시작합니다.
            StartAutoSelectionTimer();

            // 2. 기존 버튼 비활성화 (오브젝트 풀링)
            foreach (var button in _skillButtonPool)
            {
                button.gameObject.SetActive(false);
            }

            // 2. LINQ 대신 수동으로 랜덤 스킬 선택 (메모리 최적화)
            _skillChoices.Clear();
            var totalSkills = skillDatabase.allSkills.Count;
            int skillsToSelect = Mathf.Min(3, totalSkills);

            for (int i = 0; i < skillsToSelect; i++)
            {
                SkillData selectedSkill;
                do
                {
                    int randomIndex = Random.Range(0, totalSkills);
                    selectedSkill = skillDatabase.allSkills[randomIndex];
                } while (_skillChoices.Contains(selectedSkill)); // 중복 방지

                _skillChoices.Add(selectedSkill);
            }

            // 3. 풀에서 버튼을 가져와 UI를 설정합니다.
            for (int i = 0; i < _skillChoices.Count; i++)
            {
                SelectSkillBtnPrefab button;
                if (i < _skillButtonPool.Count)
                {
                    // 풀에서 재사용
                    button = _skillButtonPool[i];
                    // 버튼을 재사용할 때, 부모를 다시 설정하여 childCount 문제를 방지합니다.
                    button.transform.SetParent(skillButtonContainer.transform, false);
                }
                else
                {
                    // 풀이 부족하면 새로 생성하고 추가
                    button = Instantiate(skillSelectionButtonPrefab, skillButtonContainer.transform);
                    _skillButtonPool.Add(button);
                }

                button.gameObject.SetActive(true);
                button.Setup(_skillChoices[i], OnSkillSelected);
            }
        }

        /// <summary>
        /// 스킬 버튼이 클릭되었을 때 호출되는 콜백 메서드입니다.
        /// </summary>
        /// <param name="selectedSkill">선택된 스킬 데이터</param>
        private void OnSkillSelected(SkillData selectedSkill)
        {
            LogManager.Log($"OnSkillSelected 호출됨: {selectedSkill.skillName}. 타이머를 취소합니다.",
                LogManager.LogCategory.VamserLikeUI);
            _skillSelectionTimerCts?.Cancel(); // 사용자가 선택했으므로 타이머 취소

            // TODO: 실제 스킬 적용 로직 (예: 플레이어 스탯 강화, 새 무기 추가 등)
            // 선택된 스킬을 인게임 인벤토리에 직접 추가합니다.
            if (_gameManager.spawnedPlayer != null)
            {
                var playerRenderer = _gameManager.spawnedPlayer.GetComponent<SpriteRenderer>();
                EffectManager.Instance.PlayLevelUpEffect(playerRenderer);
            }

            DogGuns_Games.Lobby.InventoryDataManagerDontdestory.Instance.AddInGameSkill(selectedSkill);
            // UpdateJuListUI(selectedSkill); // 모든 스킬 선택 시 장신구 UI 업데이트
            _acquiredAccessorySkills.Add(selectedSkill); // 선택한 스킬을 리스트에 저장
            LogManager.Log($"장신구 '{selectedSkill.skillName}' 획득. 메뉴 오픈 시 UI에 반영됩니다.",
                LogManager.LogCategory.VamserLikeUI);

            _pendingSkillSelections--;
            LogManager.Log($"스킬 선택 완료. 남은 선택: {_pendingSkillSelections}", LogManager.LogCategory.VamserLikeUI);

            if (_pendingSkillSelections > 0)
            {
                // 아직 선택할 스킬이 남았다면, 목록만 새로고침합니다.
                GenerateSkillChoices();
            }
            else
            {
                // 모든 선택이 끝났으면, 패널을 닫고 게임을 재개합니다.
                _skillSelectionTimerCts?.Cancel();
                _skillSelectionTimerCts = null;
                skillSelectionPanel.SetActive(false);
                _isSkillSelectionActive = false;

                // 패널이 닫힐 때 카운트다운 UI를 비활성화합니다.
                if (countdownText != null)
                {
                    countdownText.gameObject.SetActive(false);
                }

                if (countDownslider != null)
                {
                    countDownslider.gameObject.SetActive(false);
                }

                _gameManager.SetMenuPopupState(false); // 게임 재개
            }
        }

        /// <summary>
        /// 현재까지 획득한 장신구 목록을 기반으로 UI 디스플레이를 새로 고칩니다.
        /// </summary>
        private void RefreshJuListDisplay()
        {
            LogManager.Log("장신구 UI 목록을 새로 고칩니다.", LogManager.LogCategory.VamserLikeUI);
            // juListUIList와 _acquiredAccessorySkills 중 더 작은 크기를 기준으로 반복합니다.
            int displayCount = Mathf.Min(juListUIList.Count, _acquiredAccessorySkills.Count);

            for (int i = 0; i < juListUIList.Count; i++)
            {
                var targetSlot = juListUIList[i];
                if (targetSlot == null)
                {
                    LogManager.LogWarning("항목이 비어있음 ");
                    continue;
                }

                if (i < displayCount)
                {
                    // 획득한 스킬이 있으면 Image 컴포넌트를 활성화하고 아이콘을 설정합니다.
                    targetSlot.enabled = true;
                    targetSlot.sprite = _acquiredAccessorySkills[i].skillIcon;
                    var color = targetSlot.color;
                    color.a = 1f;
                    targetSlot.color = color;
                }
                else
                {
                    // 획득한 스킬이 없는 슬롯은 투명하게 처리하고, Image 컴포넌트를 비활성화하여 렌더링을 막습니다.
                    var color = targetSlot.color;
                    color.a = 0f;
                    targetSlot.color = color;
                    targetSlot.enabled = false;
                }
            }
        }

        #endregion
    }
}