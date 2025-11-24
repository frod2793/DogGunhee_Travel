using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using R3;
using UnityEngine;
using UnityEngine.Serialization;
using DG.Tweening;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 인게임 UI(HUD, 팝업, 조이스틱 등)를 총괄하는 클래스입니다. (메서드명 수정됨)
    /// </summary>
    public class VamserLikeUI : MonoBehaviour
    {
        #region 필드 및 변수 (인스펙터 연결)

        [Header("유저 정보 UI")]
        [FormerlySerializedAs("LevelText")] 
        [SerializeField] private TMP_Text m_levelText;
        
        [FormerlySerializedAs("playerLevelSlider")] 
        [SerializeField] private Slider m_playerLevelSlider;

        [Header("HUD 텍스트 UI")]
        [FormerlySerializedAs("mobWaveText")] 
        [SerializeField] private TMP_Text m_mobWaveText;
        
        [FormerlySerializedAs("coinText")] 
        [SerializeField] private TMP_Text m_coinText;
        
        [FormerlySerializedAs("mobCountText")] 
        [SerializeField] private TMP_Text m_mobCountText;
        
        [FormerlySerializedAs("playerLevelText")] 
        [SerializeField] private TMP_Text m_playerLevelText_InGame;
        
        [FormerlySerializedAs("expSlider")] 
        [SerializeField] private Slider m_expSlider;

        [Header("메뉴 UI")]
        [FormerlySerializedAs("menuBtn")] [SerializeField] private Button m_menuButton;
        [FormerlySerializedAs("menuPanel")] [SerializeField] private GameObject m_menuPanel;
        [FormerlySerializedAs("settingBtn")] [SerializeField] private Button m_settingButton;
        [FormerlySerializedAs("exitBtn")] [SerializeField] private Button m_exitButton;
        
        [FormerlySerializedAs("weaponUIList")] public List<GameObject> m_weaponUIList = new List<GameObject>();
        [FormerlySerializedAs("juListUIList")] public List<Image> m_juListUIList = new List<Image>();

        [Header("게임 오버 UI")]
        [FormerlySerializedAs("gameOverPanel")] [SerializeField] private GameObject m_gameOverPanel;
        [FormerlySerializedAs("gameOverExitBtn")] [SerializeField] private Button m_gameOverExitButton;
        [FormerlySerializedAs("gameOverRestartBtn")] [SerializeField] private Button m_gameOverRestartButton;
        [FormerlySerializedAs("gameOverText")] [SerializeField] private TMP_Text m_gameOverText;
        [FormerlySerializedAs("gameOverCoinText")] [SerializeField] private TMP_Text m_gameOverCoinText;
        [FormerlySerializedAs("gameOverWaveText")] [SerializeField] private TMP_Text m_gameOverWaveText;
        [FormerlySerializedAs("gameOverMobCountText")] [SerializeField] private TMP_Text m_gameOverMobCountText;

        [Header("조작계 UI")]
        [FormerlySerializedAs("variableJoystick")] [SerializeField] private VariableJoystick m_variableJoystick;
        [FormerlySerializedAs("joystickTransform")] [SerializeField] private RectTransform m_joystickTransform;
        [FormerlySerializedAs("autoAttackToggle")] [SerializeField] private Toggle m_autoAttackToggle;

        [Header("설정 데이터")]
        [FormerlySerializedAs("settingsData")] [SerializeField] private SettingsData m_settingsData;

        [Header("스킬 선택 UI")]
        [FormerlySerializedAs("skillSelectionPanel")] [SerializeField] private GameObject m_skillSelectionPanel;
        [FormerlySerializedAs("refreshButton")] [SerializeField] private Button m_refreshButton;
        [FormerlySerializedAs("skillSelectionButtonPrefab")] [SerializeField] private SelectSkillBtnPrefab m_skillSelectionButtonPrefab;
        [FormerlySerializedAs("skillButtonContainer")] [SerializeField] private GameObject m_skillButtonContainer;
        [FormerlySerializedAs("countdownText")] [SerializeField] private TMP_Text m_countdownText;
        [FormerlySerializedAs("countDownslider")] [SerializeField] private Slider m_countDownSlider;

        [Header("데이터")]
        [FormerlySerializedAs("skillDatabase")] [SerializeField] private SkillDatabase m_skillDatabase;

        #endregion

        #region 내부 상태 변수

        private VamserLikeGameManager m_gameManager;
        private VamPlayerControll m_playerController;
        
        // 비동기 제어
        private CancellationTokenSource m_uiUpdateCts;
        private CancellationTokenSource m_skillSelectionTimerCts;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();
        private Tween m_expSliderTween;

        // 최적화를 위한 캐시 변수 (Dirty Check용)
        private int m_lastWave = -1;
        private int m_lastCoin = -1;
        private int m_lastMobCount = -1;

        // 스킬 선택 관련
        private readonly List<SelectSkillBtnPrefab> m_skillButtonPool = new List<SelectSkillBtnPrefab>();
        private readonly List<SkillData> m_skillChoices = new List<SkillData>(3);
        private readonly List<SkillData> m_acquiredAccessorySkills = new List<SkillData>();
        
        private int m_pendingSkillSelections = 0;
        private bool m_isSkillSelectionActive = false;

        private static readonly Vector2 k_DefaultJoystickPosition = new Vector2(300, 300);

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_gameManager = VamserLikeGameManager.Instance;
            BindUIEvents();

#if UNITY_STANDALONE || UNITY_WEBGL || UNITY_STANDALONE_OSX
            Screen.SetResolution(720, 1280, false);
#endif
        }

        private void Start()
        {
            m_playerController = m_gameManager.PlayerController;
            m_variableJoystick = m_gameManager.Joystick;

            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            m_disposables.Dispose();
            
            m_uiUpdateCts?.Cancel();
            m_uiUpdateCts?.Dispose();
            m_skillSelectionTimerCts?.Cancel();
            m_skillSelectionTimerCts?.Dispose();
            m_expSliderTween?.Kill();
        }

        #endregion

        #region 초기화 및 이벤트 관리

        private void SubscribeToEvents()
        {
            PlayStateManager.OnGameStart += OnGameStart;
            PlayStateManager.OnGamePause += OnGamePause;
            PlayStateManager.OnGameResume += OnGameResume;
            PlayStateManager.OnGameOver += OnGameOver;

            PlayerBase.OnExpChanged += OnPlayerExpChanged;
            PlayerBase.OnLevelUp += OnPlayerLevelUp;
            SettingsData.OnSettingsChanged += ApplyJoystickSettings;
        }

        private void UnsubscribeFromEvents()
        {
            PlayStateManager.OnGameStart -= OnGameStart;
            PlayStateManager.OnGamePause -= OnGamePause;
            PlayStateManager.OnGameResume -= OnGameResume;
            PlayStateManager.OnGameOver -= OnGameOver;

            PlayerBase.OnExpChanged -= OnPlayerExpChanged;
            PlayerBase.OnLevelUp -= OnPlayerLevelUp;
            SettingsData.OnSettingsChanged -= ApplyJoystickSettings;
        }

        private void BindUIEvents()
        {
            // 메뉴 버튼
            m_menuButton.OnClickAsObservable().Subscribe(_ => TogglePauseMenu()).AddTo(m_disposables);
            m_exitButton.OnClickAsObservable().Subscribe(_ => TogglePauseMenu()).AddTo(m_disposables);
            m_settingButton.OnClickAsObservable().Subscribe(_ => m_gameManager.OpenOptionPopup()).AddTo(m_disposables);

            // 게임 오버 버튼
            m_gameOverExitButton.OnClickAsObservable().Subscribe(_ => ExitToLobby()).AddTo(m_disposables);
            m_gameOverRestartButton.OnClickAsObservable().Subscribe(_ => RestartGame()).AddTo(m_disposables);

            // 스킬 선택 버튼
            if (m_refreshButton != null)
            {
                m_refreshButton.OnClickAsObservable().Subscribe(_ => GenerateSkillChoices()).AddTo(m_disposables);
            }

            // 자동 공격 토글
            if (m_autoAttackToggle != null)
            {
                m_autoAttackToggle.OnValueChangedAsObservable().Subscribe(OnAutoAttackToggleChanged).AddTo(m_disposables);
            }
        }

        private void InitializeUI()
        {
            m_lastWave = -1;
            m_lastCoin = -1;
            m_lastMobCount = -1;

            // [수정] PlayerLevel() -> GetPlayerLevel()
            UpdatePlayerLevelUI(m_gameManager.GetPlayerLevel());
            UpdatePlayerExpUI(m_gameManager.GetPlayerExpProgress());
            
            foreach (var image in m_juListUIList)
            {
                if (image == null) continue;
                var color = image.color;
                color.a = 0f;
                image.color = color;
                image.sprite = null;
            }
            m_acquiredAccessorySkills.Clear();
        }

        #endregion

        #region 게임 상태 핸들러

        private void OnGameStart()
        {
            m_settingsData.LoadSettings();
            ApplyJoystickSettings();
            SoundManager.Instance.LoadSoundSetting();
            
            InitializeUI();

            m_uiUpdateCts?.Cancel();
            m_uiUpdateCts = new CancellationTokenSource();
            UpdateUILoopAsync(m_uiUpdateCts.Token).Forget();
        }

        private void OnGamePause() => m_joystickTransform.gameObject.SetActive(false);

        private void OnGameResume()
        {
            m_joystickTransform.gameObject.SetActive(true);
            ApplyJoystickSettings();
        }

        private void OnGameOver()
        {
            UpdateGameOverUI();
            m_gameOverPanel.SetActive(true);

            m_joystickTransform.gameObject.SetActive(false);
            if (m_variableJoystick != null)
            {
                m_variableJoystick.OnPointerUp(null);
                m_variableJoystick.enabled = false;
            }

            if (m_autoAttackToggle != null) m_autoAttackToggle.isOn = false;
            
            m_uiUpdateCts?.Cancel();
        }

        public async void StartGameCountdown()
        {
            if (m_mobWaveText == null)
            {
                PlayStateManager.instance.StartGame();
                return;
            }

            try
            {
                m_mobWaveText.gameObject.SetActive(true);
                await ShowWaveTextEffect("3..", 0.5f, 0.2f);
                await ShowWaveTextEffect("2..", 0.5f, 0.2f);
                await ShowWaveTextEffect("1..", 0.5f, 0.2f);
                await ShowWaveTextEffect("Game Start!");
                
                PlayStateManager.instance.StartGame();
            }
            catch (Exception ex)
            {
                LogManager.LogError($"카운트다운 오류: {ex.Message}", LogManager.LogCategory.VamserLikeUI);
                PlayStateManager.instance.StartGame();
            }
        }

        #endregion

        #region UI 업데이트 루프

        private async UniTaskVoid UpdateUILoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // [수정] MobSpawnWave() -> GetCurrentWave()
                int currentWave = m_gameManager.GetCurrentWave();
                if (m_lastWave != currentWave)
                {
                    m_lastWave = currentWave;
                    ShowWaveTextEffect($"Wave {currentWave}").Forget();
                }

                // [수정] CoinCount() -> GetCoinCount()
                int currentCoin = m_gameManager.GetCoinCount();
                if (m_lastCoin != currentCoin)
                {
                    m_lastCoin = currentCoin;
                    m_coinText.SetText("{0}", currentCoin);
                }

                // [수정] Mob_Count() -> GetMobKillCount()
                int currentMobCount = m_gameManager.GetMobKillCount();
                if (m_lastMobCount != currentMobCount)
                {
                    m_lastMobCount = currentMobCount;
                    m_mobCountText.SetText("{0}", currentMobCount);
                }

                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, token);
            }
        }

        private async UniTask ShowWaveTextEffect(string text, float holdDuration = 1.0f, float fadeDuration = 0.5f)
        {
            if (m_mobWaveText == null) return;
            
            m_mobWaveText.text = text;
            m_mobWaveText.alpha = 0f;
            m_mobWaveText.gameObject.SetActive(true);
            
            await m_mobWaveText.DOFade(1f, fadeDuration).AsyncWaitForCompletion();
            await UniTask.Delay(TimeSpan.FromSeconds(holdDuration));
            await m_mobWaveText.DOFade(0f, fadeDuration).AsyncWaitForCompletion();
            
            m_mobWaveText.gameObject.SetActive(false);
        }

        #endregion

        #region 플레이어 이벤트

        private void OnPlayerExpChanged(float currentExp, float maxExp)
        {
            float progress = (maxExp > 0) ? currentExp / maxExp : 0;
            UpdatePlayerExpUI(progress);
        }

        private void OnPlayerLevelUp(float newLevel)
        {
            UpdatePlayerLevelUI(newLevel);
            ShowLevelUpEffect();

            if (newLevel >= 2)
            {
                m_pendingSkillSelections++;
                if (!m_isSkillSelectionActive)
                {
                    ProcessSkillSelectionQueue();
                }
            }
        }

        private void UpdatePlayerExpUI(float progress)
        {
            m_expSliderTween?.Kill();
            m_expSliderTween = m_playerLevelSlider.DOValue(progress, 0.2f).SetEase(Ease.OutQuad);
            
            if (m_expSlider != null)
                m_expSlider.DOValue(progress, 0.2f).SetEase(Ease.OutQuad);
        }

        private void UpdatePlayerLevelUI(float level)
        {
            m_levelText.SetText("Lv. {0}", (int)level);
            m_playerLevelText_InGame.SetText("Lv. {0}", (int)level);
        }

        private void ShowLevelUpEffect()
        {
            if (m_levelText != null)
            {
                m_levelText.transform.DOScale(Vector3.one * 1.2f, 0.15f).SetLoops(2, LoopType.Yoyo);
            }
        }

        #endregion

        #region 설정 및 조작

        private void OnAutoAttackToggleChanged(bool isOn)
        {
            if (m_playerController != null)
                m_playerController.AutoAttackEnabledByToggle = isOn;
        }

        private void ApplyJoystickSettings()
        {
            if (m_variableJoystick == null || m_settingsData == null) return;

            // [오류 수정] ScriptableObject의 프로퍼티(PascalCase)를 사용합니다.
            m_joystickTransform.localScale = Vector3.one * m_settingsData.JoystickSize;
            m_variableJoystick.SetMode((JoystickType)m_settingsData.JoystickType);

            if (IsJoystickVisible(m_settingsData.JoystickPos))
            {
                m_joystickTransform.anchoredPosition = m_settingsData.JoystickPos;
            }
            else
            {
                m_joystickTransform.anchoredPosition = k_DefaultJoystickPosition;
            }
        }

        private bool IsJoystickVisible(Vector2 pos)
        {
            var canvas = m_joystickTransform.GetComponentInParent<Canvas>();
            if (canvas == null) return false;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Rect joystickRect = new Rect(
                pos.x - (m_joystickTransform.rect.width * 0.5f),
                pos.y - (m_joystickTransform.rect.height * 0.5f),
                m_joystickTransform.rect.width,
                m_joystickTransform.rect.height
            );

            return canvasRect.rect.Overlaps(joystickRect);
        }

        private void TogglePauseMenu()
        {
            bool isActive = !m_menuPanel.activeSelf;
            m_menuPanel.SetActive(isActive);
            m_gameManager.SetMenuPopupState(isActive);
            m_joystickTransform.gameObject.SetActive(!isActive);

            if (isActive) RefreshJuListDisplay();
        }

        #endregion

        #region 스킬 선택 시스템

        private void ProcessSkillSelectionQueue()
        {
            if (m_pendingSkillSelections > 0)
            {
                ShowSkillSelectionPanel();
            }
        }

        private void ShowSkillSelectionPanel()
        {
            m_gameManager.SetMenuPopupState(true);
            m_isSkillSelectionActive = true;
            m_skillSelectionPanel.SetActive(true);
            GenerateSkillChoices();
        }

        private void StartAutoSelectionTimer()
        {
            m_skillSelectionTimerCts?.Cancel();
            m_skillSelectionTimerCts = new CancellationTokenSource();
            CountdownAndAutoSelect(m_skillSelectionTimerCts.Token).Forget();
        }

        private async UniTaskVoid CountdownAndAutoSelect(CancellationToken token)
        {
            const float duration = 6.0f;
            float timer = duration;

            m_countdownText.gameObject.SetActive(true);
            m_countDownSlider.gameObject.SetActive(true);

            while (timer > 0f && !token.IsCancellationRequested)
            {
                m_countdownText.text = Mathf.CeilToInt(timer).ToString();
                m_countDownSlider.value = timer / duration;
                
                await UniTask.NextFrame(token);
                timer -= Time.deltaTime;
            }

            if (!token.IsCancellationRequested)
            {
                await SelectRandomSkill();
            }
        }

        private async UniTask SelectRandomSkill()
        {
            if (m_skillChoices.Count > 0)
            {
                int randomIndex = Random.Range(0, m_skillChoices.Count);
                var randomSkill = m_skillChoices[randomIndex];

                var targetBtn = m_skillButtonPool.FirstOrDefault(b => b.gameObject.activeSelf && b.GetCurrentSkillData() == randomSkill);
                if (targetBtn != null)
                {
                    await targetBtn.PlaySelectionAnimation();
                }
                
                OnSkillSelected(randomSkill);
            }
        }

        private void GenerateSkillChoices()
        {
            StartAutoSelectionTimer();

            foreach (var btn in m_skillButtonPool) btn.gameObject.SetActive(false);
            m_skillChoices.Clear();

            var totalSkills = m_skillDatabase.allSkills.Count;
            int count = Mathf.Min(3, totalSkills);
            
            while (m_skillChoices.Count < count)
            {
                var skill = m_skillDatabase.allSkills[Random.Range(0, totalSkills)];
                if (!m_skillChoices.Contains(skill)) 
                    m_skillChoices.Add(skill);
            }

            for (int i = 0; i < m_skillChoices.Count; i++)
            {
                SelectSkillBtnPrefab btn;
                if (i < m_skillButtonPool.Count)
                {
                    btn = m_skillButtonPool[i];
                    btn.transform.SetParent(m_skillButtonContainer.transform, false);
                }
                else
                {
                    btn = Instantiate(m_skillSelectionButtonPrefab, m_skillButtonContainer.transform);
                    m_skillButtonPool.Add(btn);
                }

                btn.gameObject.SetActive(true);
                btn.Setup(m_skillChoices[i], OnSkillSelected);
            }
        }

        private void OnSkillSelected(SkillData selectedSkill)
        {
            m_skillSelectionTimerCts?.Cancel();

            if (m_gameManager.SpawnedPlayer != null)
            {
                EffectManager.Instance.PlayLevelUpEffect(m_gameManager.SpawnedPlayer.GetComponent<SpriteRenderer>());
            }
            DogGuns_Games.Lobby.InventoryDataManagerDontdestory.Instance.AddInGameSkill(selectedSkill);
            m_acquiredAccessorySkills.Add(selectedSkill);

            m_pendingSkillSelections--;

            if (m_pendingSkillSelections > 0)
            {
                GenerateSkillChoices();
            }
            else
            {
                CloseSkillSelection();
            }
        }

        private void CloseSkillSelection()
        {
            m_skillSelectionTimerCts?.Cancel();
            m_skillSelectionPanel.SetActive(false);
            m_isSkillSelectionActive = false;
            
            if (m_countdownText) m_countdownText.gameObject.SetActive(false);
            if (m_countDownSlider) m_countDownSlider.gameObject.SetActive(false);

            m_gameManager.SetMenuPopupState(false);
        }

        private void RefreshJuListDisplay()
        {
            int count = Mathf.Min(m_juListUIList.Count, m_acquiredAccessorySkills.Count);

            for (int i = 0; i < m_juListUIList.Count; i++)
            {
                var slot = m_juListUIList[i];
                if (slot == null) continue;

                if (i < count)
                {
                    slot.enabled = true;
                    slot.sprite = m_acquiredAccessorySkills[i].skillIcon;
                    var c = slot.color; c.a = 1f; slot.color = c;
                }
                else
                {
                    slot.enabled = false;
                    var c = slot.color; c.a = 0f; slot.color = c;
                }
            }
        }

        #endregion

        #region 게임 종료 처리

        private void UpdateGameOverUI()
        {
            m_gameOverText.text = "Game Over";
            // [수정] CoinCount() -> GetCoinCount()
            m_gameOverCoinText.SetText("Coins: {0}", m_gameManager.GetCoinCount());
            // [수정] MobSpawnWave() -> GetCurrentWave()
            m_gameOverWaveText.SetText("Wave: {0}", m_gameManager.GetCurrentWave());
            // [수정] Mob_Count() -> GetMobKillCount()
            m_gameOverMobCountText.SetText("Kills: {0}", m_gameManager.GetMobKillCount());
        }

        private void ExitToLobby()
        {
            m_gameOverPanel.SetActive(false);
            SceneLoader.Instance.LoadLobbyScene();
        }

        private void RestartGame()
        {
            m_gameOverPanel.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        #endregion
    }
}