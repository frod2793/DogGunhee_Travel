using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using DG.Tweening;
using UnityEngine.UI;
using Random = UnityEngine.Random;


namespace DogGuns_Games.vamsir
{
    public class VamserLikeUI : MonoBehaviour
    {
        #region 필드 및 변수

        [Header("<color=green>User Info UI")]
        [FormerlySerializedAs("LevelText")] [SerializeField] private TMP_Text m_levelText;
        [FormerlySerializedAs("playerLevelSlider")] [SerializeField] private Slider m_playerLevelSlider;

        [Header("<color=green>Text UI")]
        [FormerlySerializedAs("mobWaveText")] [SerializeField] private TMP_Text m_mobWaveText;
        [FormerlySerializedAs("coinText")] [SerializeField] private TMP_Text m_coinText;
        [FormerlySerializedAs("mobCountText")] [SerializeField] private TMP_Text m_mobCountText;
        [FormerlySerializedAs("playerLevelText")] [SerializeField] private TMP_Text m_playerLevelText_InGame;
        [FormerlySerializedAs("expSlider")] [SerializeField] private Slider m_expSlider;
        private int m_getCoinCount = 0; // 초기화 전 코인 정보를 담을 변수

        [Header("<color=green>Menu UI")]
        [FormerlySerializedAs("menuBtn")] [SerializeField] private Button m_menuButton;
        [FormerlySerializedAs("menuPanel")] [SerializeField] private GameObject m_menuPanel;
        [FormerlySerializedAs("settingBtn")] [SerializeField] private Button m_settingButton;
        [FormerlySerializedAs("exitBtn")] [SerializeField] private Button m_exitButton;
        [FormerlySerializedAs("weaponUIList")] public List<GameObject> m_weaponUIList = new List<GameObject>();
        [FormerlySerializedAs("juListUIList")] public List<Image> m_juListUIList = new List<Image>();

        [Header("<color=green>GameOver UI")]
        [FormerlySerializedAs("gameOverPanel")] [SerializeField] private GameObject m_gameOverPanel;
        [FormerlySerializedAs("gameOverExitBtn")] [SerializeField] private Button m_gameOverExitButton;
        [FormerlySerializedAs("gameOverRestartBtn")] [SerializeField] private Button m_gameOverRestartButton;
        [FormerlySerializedAs("gameOverText")] [SerializeField] private TMP_Text m_gameOverText;
        [FormerlySerializedAs("gameOverCoinText")] [SerializeField] private TMP_Text m_gameOverCoinText;
        [FormerlySerializedAs("gameOverWaveText")] [SerializeField] private TMP_Text m_gameOverWaveText;
        [FormerlySerializedAs("gameOverMobCountText")] [SerializeField] private TMP_Text m_gameOverMobCountText;

        [Header("<color=green>조이스틱")]
        [FormerlySerializedAs("variableJoystick")] [SerializeField] private VariableJoystick m_variableJoystick;
        [Tooltip("조이스틱의 위치와 크기를 제어하는 RectTransform 입니다.")]
        [FormerlySerializedAs("joystickTransform")] [SerializeField] private RectTransform m_joystickTransform;

        [Header("<color=green>플레이어 자동 공격 활성화 토글 ")]
        [FormerlySerializedAs("autoAttackToggle")] [SerializeField] private Toggle m_autoAttackToggle;

        [Header("설정 데이터")]
        [FormerlySerializedAs("settingsData")] [SerializeField] private SettingsData_oBJ m_settingsData;

        /// <summary>
        /// 레벨업 시 표시되는 스킬 선택 UI입니다.
        /// 3개의 랜덤 스킬이 제시되며, 선택 시 팝업이 닫힙니다.
        /// 리프레시 버튼으로 선택지를 다시 뽑을 수 있습니다.
        /// </summary>
        [Header("Skill Selection UI")]
        [Tooltip("스킬 선택 팝업의 최상위 패널입니다.")]
        [FormerlySerializedAs("skillSelectionPanel")] [SerializeField] private GameObject m_skillSelectionPanel;
        [Tooltip("스킬 선택지를 다시 뽑는 리프레시 버튼입니다.")]
        [FormerlySerializedAs("refreshButton")] [SerializeField] private Button m_refreshButton;
        [Tooltip("동적으로 생성될 스킬 선택 버튼의 프리팹입니다.")]
        [FormerlySerializedAs("skillSelectionButtonPrefab")] [SerializeField] private SelectSkillBtnPrefab m_skillSelectionButtonPrefab;
        [Tooltip("생성된 스킬 선택 버튼들이 위치할 부모 컨테이너입니다.")]
        [FormerlySerializedAs("skillButtonContainer")] [SerializeField] private GameObject m_skillButtonContainer;
        [FormerlySerializedAs("countdownText")] [SerializeField] private TMP_Text m_countdownText;
        [FormerlySerializedAs("countDownslider")] [SerializeField] private Slider m_countDownSlider;

        [Header("Skill Data")]
        [Tooltip("게임 내 모든 스킬 정보가 담긴 데이터베이스입니다.")]
        [FormerlySerializedAs("skillDatabase")] [SerializeField] private SkillDatabase m_skillDatabase;

        // Private Fields
        private VamserLikeGameManager m_gameManager;
        private VamPlayerControll m_playerController;
        private CancellationTokenSource _cancellationTokenSource;
        private Tween _expSliderTween;

        // WebGL 메모리 최적화를 위한 변수
        private int _lastWave = -1; // Wave UI 업데이트 최적화를 위한 변수

        // 스킬 선택 관련 필드
        private readonly List<SelectSkillBtnPrefab> _skillButtonPool = new List<SelectSkillBtnPrefab>(); // 스킬 버튼 오브젝트 풀링
        private readonly List<SkillData> _skillChoices = new List<SkillData>(3); // 스킬 선택 최적화를 위한 리스트
        private readonly List<SkillData> _acquiredAccessorySkills = new List<SkillData>(); // 획득한 장신구 스킬 목록
        private int _nextJuListSlotIndex = 0; // 장신구 UI 슬롯 업데이트 최적화를 위한 인덱스
        private int _pendingSkillSelections = 0; // 처리 대기 중인 스킬 선택 횟수
        private bool _isSkillSelectionActive = false; // 스킬 선택 UI가 활성화되어 있는지 여부
        private CancellationTokenSource _skillSelectionTimerCts; // 자동 스킬 선택 타이머를 위한 CancellationTokenSource

        // 상수
        private static readonly Vector2 k_DefaultJoystickPosition = new Vector2(300, 300);

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            // 싱글톤 인스턴스를 사용하여 더 효율적이고 안정적으로 참조를 가져옵니다.
            m_gameManager = VamserLikeGameManager.Instance; // Instance 프로퍼티가 null 체크를 담당합니다.

#if UNITY_STANDALONE || UNITY_WEBGL|| UNITY_STANDALONE_OSX
            // PC 및 WebGL 환경에서 창 크기를 720x1280으로 고정합니다.
            Screen.SetResolution(720, 1280, false);
            LogManager.Log("PC/WebGL 환경으로 감지되어 화면 크기를 720x1280으로 설정합니다.", LogManager.LogCategory.VamserLikeUI);
#endif
        }

        private void Start()
        {
            // 참조 캐싱
            m_playerController = m_gameManager.PlayerController;
            m_variableJoystick = m_gameManager.Joystick;
            LogManager.Log(m_playerController != null ? "PlayerController 참조 가져오기 성공" : "PlayerController 참조 가져오기 실패", 
                LogManager.LogCategory.VamserLikeUI);
            LogManager.Log(m_variableJoystick != null ? "VariableJoystick 참조 가져오기 성공" : "VariableJoystick 참조 가져오기 실패", 
                LogManager.LogCategory.VamserLikeUI);
            
            // 모든 참조가 할당된 후 이벤트를 구독합니다.
            SubscribeToEvents();
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

            // 토글 및 버튼 이벤트 해제
            m_autoAttackToggle.onValueChanged.RemoveListener(OnAutoAttackToggleChanged);
            // 리프레시 버튼 이벤트 해제
            m_refreshButton.onClick.RemoveListener(GenerateSkillChoices);
        }

        #endregion

        #region 게임 상태 관리

        private void GameStart()
        {
            // 게임 시작 시, 파일에 저장된 최신 설정값을 명시적으로 불러옵니다.
            m_settingsData.LoadSettings();

            InitializeButtons();
            JoystickSetting();
            SoundSetting(); // 사운드 설정 추가
            InitializeJuListUI(); // 스킬 UI 리스트 초기화
            _acquiredAccessorySkills.Clear(); // 게임 시작 시 획득한 스킬 목록 초기화
            _cancellationTokenSource = new CancellationTokenSource();

            InitializeUI();

            UpdateUI(_cancellationTokenSource.Token).Forget();
        }

        /// <summary>
        /// 게임 시작 전 3초 카운트다운을 표시하고 게임을 시작합니다.
        /// </summary>
        public async void StartGameCountdown()
        {
            try
            {
                if (m_mobWaveText == null)
                {
                    PlayStateManager.instance.StartGame(); // UI가 없으면 즉시 시작
                    return;
                }

                m_mobWaveText.gameObject.SetActive(true);

                // 카운트다운을 위한 빠른 버전의 텍스트 효과 호출
                await WaveTextFadeEffect("3..", 0.5f, 0.2f);
                await WaveTextFadeEffect("2..", 0.5f, 0.2f);
                await WaveTextFadeEffect("1..", 0.5f, 0.2f);

                // 게임 시작 텍스트는 기존 효과 사용
                await WaveTextFadeEffect("Game Start!");
                PlayStateManager.instance.StartGame();
            }
            catch (Exception ex)
            {
                LogManager.LogError($"게임 시작 카운트다운 중 오류 발생: {ex.Message}", LogManager.LogCategory.VamserLikeUI);
                // 카운트다운에 실패하더라도 게임은 시작되도록 처리
                PlayStateManager.instance.StartGame();
            }
        }

        private void Pause()
        {
            m_joystickTransform.gameObject.SetActive(false);
        }

        private void Resume()
        {
            m_joystickTransform.gameObject.SetActive(true);
            JoystickSetting();
        }

        #endregion

        #region UI 설정 및 초기화
        
        /// <summary>
        /// 게임 시작 시 UI 요소들을 초기화합니다.
        /// </summary>
        private void InitializeUI()
        {
            UpdatePlayerLevelUI(m_gameManager.PlayerLevel());
            UpdatePlayerExpUI(m_gameManager.GetPlayerExpProgress());
        }
        
        /// <summary>
        /// 필요한 이벤트들을 구독합니다.
        /// </summary>
        private void SubscribeToEvents()
        {
            PlayStateManager.OnGameStart += GameStart;
            PlayStateManager.OnGamePause += Pause;
            PlayStateManager.OnGameResume += Resume;
            PlayStateManager.OnGameOver += ShowGameOverPopup;

            PlayerBase.OnExpChanged += OnPlayerExpChanged;
            PlayerBase.OnLevelUp += OnPlayerLevelUp;
            SettingsData_oBJ.OnSettingsChanged += JoystickSetting;

            m_autoAttackToggle.onValueChanged.AddListener(OnAutoAttackToggleChanged);
            m_refreshButton.onClick.AddListener(GenerateSkillChoices);
        }
        
        /// <summary>
        /// 자동 공격 토글 상태가 변경될 때 호출됩니다.
        /// </summary>
        private void OnAutoAttackToggleChanged(bool isOn)
        {
            if (m_playerController != null)
                m_playerController.AutoAttackEnabledByToggle = isOn;
            else
                LogManager.LogError("VamPlayerControll을 찾을 수 없습니다!", LogManager.LogCategory.VamserLikeUI);
        }

        private void JoystickSetting()
        {
            if (m_variableJoystick == null)
            {
                LogManager.LogError("VariableJoystick을 찾을 수 없습니다.", LogManager.LogCategory.VamserLikeUI);
                return;
            }

            if (m_settingsData == null)
            {
                LogManager.LogError("VamserLikeUI에 SettingsData가 할당되지 않았습니다. 인스펙터에서 할당해주세요.",
                    LogManager.LogCategory.VamserLikeUI);
                return;
            }

            // OnSettingsChanged 이벤트는 이미 메모리의 settingsData가 업데이트된 후에 호출됩니다.
            // 여기서 LoadSettings()를 다시 호출하면 파일의 이전 데이터로 덮어쓰여 문제가 발생하므로 제거합니다.

            m_joystickTransform.localScale = new Vector3(m_settingsData.joystickSize, m_settingsData.joystickSize, 1);
            if (m_variableJoystick != null)
            {
                m_variableJoystick.SetMode((JoystickType)m_settingsData.joystickType);
            }

            // 저장된 조이스틱 위치가 화면 밖에 있는지 확인하고, 밖에 있다면 기본 위치로 재설정합니다.
            if (IsJoystickVisible(m_settingsData.joystickPos))
            {
                m_joystickTransform.anchoredPosition = m_settingsData.joystickPos;
            }
            else
            {
                m_joystickTransform.anchoredPosition = k_DefaultJoystickPosition; // 안전한 기본 위치
                LogManager.LogWarning("저장된 조이스틱 위치가 화면 밖이라 기본 위치로 재설정합니다.", LogManager.LogCategory.VamserLikeUI);
            }
        }

        /// <summary>
        /// 지정된 위치에 조이스틱이 있을 때 화면에 보이는지 확인합니다.
        /// </summary>
        /// <param name="joystickPosition">확인할 조이스틱의 anchoredPosition</param>
        /// <returns>화면에 보이면 true, 그렇지 않으면 false</returns>
        private bool IsJoystickVisible(Vector2 joystickPosition)
        {
            var canvas = m_joystickTransform.GetComponentInParent<Canvas>();
            if (canvas == null) return false;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Rect joystickRect = new Rect(
                joystickPosition.x - (m_joystickTransform.rect.width * m_joystickTransform.pivot.x * m_joystickTransform.localScale.x),
                joystickPosition.y - (m_joystickTransform.rect.height * m_joystickTransform.pivot.y * m_joystickTransform.localScale.y),
                m_joystickTransform.rect.width * m_joystickTransform.localScale.x,
                m_joystickTransform.rect.height * m_joystickTransform.localScale.y
            );

            return canvasRect.rect.Overlaps(joystickRect, true);
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
            foreach (var image in m_juListUIList)
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

        private void InitializeButtons()
        {
            m_menuButton.onClick.AddListener(PausePopUp);

            m_exitButton.onClick.AddListener(PausePopUp);

            m_settingButton.onClick.AddListener(() => { m_gameManager.OpenOptionPopup(); });

            // 게임 오버 버튼 설정
            m_gameOverExitButton.onClick.AddListener(GameOverExit);
            m_gameOverRestartButton.onClick.AddListener(GameOverRestart);
        }

        #endregion

        #region UI 이벤트 및 동작

        private void PausePopUp()
        {
            // 메뉴 패널의 현재 활성 상태의 반대로 설정합니다.
            bool isMenuPanelBecomingActive = !m_menuPanel.activeSelf;
            m_menuPanel.SetActive(isMenuPanelBecomingActive);

            // isMenuPanelBecomingActive 값에 따라 게임의 Pause/Resume 상태를 설정합니다.
            m_gameManager.SetMenuPopupState(isMenuPanelBecomingActive);

            // 메뉴 패널이 활성화되면 조이스틱을 비활성화하고, 그 반대의 경우도 마찬가지입니다.
            m_joystickTransform.gameObject.SetActive(!isMenuPanelBecomingActive);

            // 메뉴 패널이 활성화될 때만 장신구 UI를 업데이트합니다.
            if (isMenuPanelBecomingActive) RefreshJuListDisplay();
        }

        private void GameOverExit()
        {
            // 게임 오버 패널을 비활성화하고, 로비 씬으로 이동
            m_gameOverPanel.SetActive(false);
            SceneLoader.Instance.LoadLobbyScene();
        }

        private void GameOverRestart()
        {
            // 게임 오버 패널을 비활성화하고, 게임을 다시 시작
            m_gameOverPanel.SetActive(false);
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
            m_gameOverPanel.SetActive(true);

            // 조이스틱 비활성화 및 위치/상태 초기화
            m_joystickTransform.gameObject.SetActive(false);
            if (m_variableJoystick != null)
            {
                m_variableJoystick.OnPointerUp(null); // 입력 해제
                m_variableJoystick.enabled = false; // 조이스틱 컴포넌트 비활성화
            }

            m_autoAttackToggle.isOn = false; // 자동 공격 토글 비활성화

            // 취소 토큰 소스가 있다면 취소합니다.
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// 게임 오버 UI의 텍스트들을 업데이트합니다. (메모리 최적화)
        /// </summary>
        private void UpdateGameOverUI()
        {
            m_gameOverText.text = "Game Over";
            m_gameOverCoinText.SetText("Coins: {0}", m_getCoinCount);
            m_gameOverWaveText.SetText("Wave: {0}", m_gameManager.MobSpawnWave());
            m_gameOverMobCountText.SetText("Kills: {0}", m_gameManager.Mob_Count());
        }

        #endregion

        #region UI 업데이트

        private async UniTask UpdateUI(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int currentWave = m_gameManager.MobSpawnWave();
                if (_lastWave != currentWave)
                {
                    _lastWave = currentWave;
                    // 문자열 할당은 Wave가 변경될 때만 발생하도록 최적화
                    await WaveTextFadeEffect($"Wave {currentWave}");
                }

                // SetText를 사용하여 숫자 업데이트 시 문자열 할당 방지
                m_coinText.SetText("{0}", m_gameManager.CoinCount());
                m_getCoinCount = m_gameManager.CoinCount();
                m_mobCountText.SetText("{0}", m_gameManager.Mob_Count());
                await UniTask.DelayFrame(1, PlayerLoopTiming.FixedUpdate, cancellationToken);
            }
        }

        // DOTween을 이용한 mobWaveText 페이드 인/아웃 효과
        private async UniTask WaveTextFadeEffect(string waveText, float holdDuration = 1.0f, float fadeDuration = 0.5f)
        {
            m_mobWaveText.text = waveText;
            m_mobWaveText.alpha = 0f;
            m_mobWaveText.gameObject.SetActive(true);
            await m_mobWaveText.DOFade(1f, fadeDuration).AsyncWaitForCompletion(); // 페이드 인
            await UniTask.Delay(TimeSpan.FromSeconds(holdDuration)); // 지정된 시간 동안 표시
            await m_mobWaveText.DOFade(0f, fadeDuration).AsyncWaitForCompletion(); // 페이드 아웃
            m_mobWaveText.gameObject.SetActive(false);
        }

        #endregion

        #region 플레이어 경험치 및 레벨 이벤트

        /// <summary>
        /// 플레이어 경험치가 변경되었을 때 호출되는 메서입니다.
        /// </summary>
        private void OnPlayerExpChanged(float currentExp, float maxExp)
        {
            float progress = (maxExp > 0) ? currentExp / maxExp : 0;
            UpdatePlayerExpUI(progress);
        }

        /// <summary>
        /// 플레이어 경험치 UI(슬라이더)를 업데이트합니다.
        /// </summary>
        private void UpdatePlayerExpUI(float progress)
        {
            _expSliderTween?.Kill(); // 기존 트윈 중지
            _expSliderTween = m_playerLevelSlider.DOValue(progress, 0.2f).SetEase(Ease.OutQuad);

            // 인게임 하단 경험치 슬라이더도 함께 업데이트
            if (m_expSlider != null)
                m_expSlider.DOValue(progress, 0.2f).SetEase(Ease.OutQuad);
        }

        /// <summary>
        /// 플레이어 레벨업 시 호출되는 메서드입니다.
        /// </summary>
        private void OnPlayerLevelUp(float newLevel)
        {
            UpdatePlayerLevelUI(newLevel);

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
        /// 플레이어 레벨 UI(텍스트)를 업데이트합니다.
        /// </summary>
        private void UpdatePlayerLevelUI(float level)
        {
            m_levelText.SetText("Lv. {0}", (int)level);
            m_playerLevelText_InGame.SetText("Lv. {0}", (int)level);
        }

        /// <summary>
        /// 레벨업 시 시각적 효과를 표시합니다.
        /// </summary>
        private void ShowLevelUpEffect(float newLevel)
        {
            // 레벨 텍스트에 간단한 효과 적용 (선택사항)
            if (m_levelText != null)
            {
                // 크기 변화 효과 (DOTween 사용)
                m_levelText.transform.localScale = Vector3.one * 1.2f;
                m_levelText.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);

                // 색상 변화 효과 (DOTween 사용)
                Color originalColor = m_levelText.color;
                m_levelText.color = Color.yellow;
                m_levelText.DOColor(originalColor, 1f);
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
            m_gameManager.SetMenuPopupState(true); // 게임 일시정지
            _isSkillSelectionActive = true;
            m_skillSelectionPanel.SetActive(true);
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

            m_countdownText.gameObject.SetActive(true);
            m_countDownSlider.gameObject.SetActive(true);
            m_countDownSlider.value = 1f;

            // 타이머가 진행 중이고, 취소 요청이 없을 때만 루프를 실행합니다.
            while (timer > 0.01f && !cancellationToken.IsCancellationRequested)
            {
                m_countdownText.text = Mathf.CeilToInt(timer).ToString();
                m_countDownSlider.value = timer / duration;

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
                m_countdownText.text = "0";
                m_countDownSlider.value = 0;
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
            var totalSkills = m_skillDatabase.allSkills.Count;
            int skillsToSelect = Mathf.Min(3, totalSkills);

            for (int i = 0; i < skillsToSelect; i++)
            {
                SkillData selectedSkill;
                do
                {
                    int randomIndex = Random.Range(0, totalSkills);
                    selectedSkill = m_skillDatabase.allSkills[randomIndex];
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
                    // 버튼을 재사용할 때, 부모를 다시 설정하여 문제를 방지합니다.
                    button.transform.SetParent(m_skillButtonContainer.transform, false);
                }
                else
                {
                    // 풀이 부족하면 새로 생성하고 추가
                    button = Instantiate(m_skillSelectionButtonPrefab, m_skillButtonContainer.transform);
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
            if (m_gameManager.SpawnedPlayer != null)
            {
                var playerRenderer = m_gameManager.SpawnedPlayer.GetComponent<SpriteRenderer>();
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
                m_skillSelectionPanel.SetActive(false);
                _isSkillSelectionActive = false;

                // 패널이 닫힐 때 카운트다운 UI를 비활성화합니다.
                if (m_countdownText != null)
                {
                    m_countdownText.gameObject.SetActive(false);
                }

                if (m_countDownSlider != null)
                {
                    m_countDownSlider.gameObject.SetActive(false);
                }

                m_gameManager.SetMenuPopupState(false); // 게임 재개
            }
        }

        /// <summary>
        /// 현재까지 획득한 장신구 목록을 기반으로 UI 디스플레이를 새로 고칩니다.
        /// </summary>
        private void RefreshJuListDisplay()
        {
            LogManager.Log("장신구 UI 목록을 새로 고칩니다.", LogManager.LogCategory.VamserLikeUI);
            // juListUIList와 _acquiredAccessorySkills 중 더 작은 크기를 기준으로 반복합니다.
            int displayCount = Mathf.Min(m_juListUIList.Count, _acquiredAccessorySkills.Count);

            for (int i = 0; i < m_juListUIList.Count; i++)
            {
                var targetSlot = m_juListUIList[i];
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