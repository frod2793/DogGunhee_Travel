using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using R3;
using UnityEngine;
using DG.Tweening;
using InGame.Player.Player_Base;
using UnityEngine.UI;
using InGame;
using Random = UnityEngine.Random;
using InGame.Lobby;
using InGame.vamsir;
using InGame.Weaphon.Base;

namespace InGame.Manager
{
    public class UIManager : MonoBehaviour
    {
        #region 필드 및 변수 (인스펙터 연결)

        [Header("유저 정보 UI")] [SerializeField] private TMP_Text m_levelText;
        [SerializeField] private Slider m_playerLevelSlider;

        [Header("HUD 텍스트 UI")] [SerializeField]
        private TMP_Text m_mobWaveText;

        [SerializeField] private TMP_Text m_coinText;
        [SerializeField] private TMP_Text m_mobCountText;
        [SerializeField] private TMP_Text m_playerLevelText_InGame;
        [SerializeField] private Slider m_expSlider;

        [Header("메뉴 UI")] [SerializeField] private Button m_menuButton;
        [SerializeField] private GameObject m_menuPanel;
        [SerializeField] private Button m_settingButton;
        [SerializeField] private Button m_exitButton;

        public List<Image> m_weaponUIList = new List<Image>();
        public List<Image> m_juListUIList = new List<Image>();

        [Header("게임 오버 UI")] [SerializeField] private GameObject m_gameOverPanel;
        [SerializeField] private Button m_gameOverExitButton;
        [SerializeField] private Button m_gameOverRestartButton;
        [SerializeField] private TMP_Text m_gameOverText;
        [SerializeField] private TMP_Text m_gameOverCoinText;
        [SerializeField] private TMP_Text m_gameOverWaveText;
        [SerializeField] private TMP_Text m_gameOverMobCountText;

        [Header("조작계 UI")] [SerializeField] private VariableJoystick m_variableJoystick;
        [SerializeField] private RectTransform m_joystickTransform;
        [SerializeField] private Toggle m_autoAttackToggle;

        [Header("설정 데이터")] [SerializeField] private SettingsData m_settingsData;

        [Header("스킬 선택 UI")] [SerializeField] private GameObject m_skillSelectionPanel;
        [SerializeField] private Button m_refreshButton;
        [SerializeField] private SelectSkillBtnPrefab m_skillSelectionButtonPrefab;
        [SerializeField] private GameObject m_skillButtonContainer;
        [SerializeField] private TMP_Text m_countdownText;
        [SerializeField] private Slider m_countDownSlider;

        [Header("데이터")] [SerializeField] private SkillDatabase m_skillDatabase;

        #endregion

        #region 내부 상태 변수

        private GameManager m_gameManager;
        private PlayerControll m_playerController;

        private CancellationTokenSource m_uiUpdateCts;
        private CancellationTokenSource m_skillSelectionTimerCts;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();
        private Tween m_expSliderTween;

        private int m_lastWave = -1;
        private int m_lastCoin = -1;
        private int m_lastMobCount = -1;

        private readonly List<SelectSkillBtnPrefab> m_skillButtonPool = new List<SelectSkillBtnPrefab>();
        private readonly List<SkillData> m_skillChoices = new List<SkillData>(3);
        private readonly List<SkillData> m_acquiredAccessorySkills = new List<SkillData>();

        private readonly List<Sprite> m_weaponThumbnails = new List<Sprite>();
        private readonly List<Sprite> m_accessoryIcons = new List<Sprite>();

        private int m_pendingSkillSelections = 0;
        private bool m_isSkillSelectionActive = false;

        private static readonly Vector2 k_DefaultJoystickPosition = new Vector2(300, 300);

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_gameManager = GameManager.Instance;
            BindUIEvents();
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
            GameManager.OnPlayerChanged += OnPlayerChanged;
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
            GameManager.OnPlayerChanged -= OnPlayerChanged;
        }

        private void BindUIEvents()
        {
            m_menuButton.OnClickAsObservable().Subscribe(_ => TogglePauseMenu()).AddTo(m_disposables);
            m_exitButton.OnClickAsObservable().Subscribe(_ => TogglePauseMenu()).AddTo(m_disposables);
            m_settingButton.OnClickAsObservable().Subscribe(_ => m_gameManager.OpenOptionPopup()).AddTo(m_disposables);
            m_gameOverExitButton.OnClickAsObservable().Subscribe(_ => ExitToLobby()).AddTo(m_disposables);
            m_gameOverRestartButton.OnClickAsObservable().Subscribe(_ => RestartGame()).AddTo(m_disposables);
            if (m_refreshButton != null)
            {
                m_refreshButton.OnClickAsObservable().Subscribe(_ => GenerateSkillChoices()).AddTo(m_disposables);
            }

            if (m_autoAttackToggle != null)
            {
                m_autoAttackToggle.OnValueChangedAsObservable().Subscribe(OnAutoAttackToggleChanged)
                    .AddTo(m_disposables);
            }
        }

        private void InitializeUI()
        {
            m_lastWave = -1;
            m_lastCoin = -1;
            m_lastMobCount = -1;
            UpdatePlayerLevelUI(m_gameManager.GetPlayerLevel());
            UpdatePlayerExpUI(m_gameManager.GetPlayerExpProgress());
            m_acquiredAccessorySkills.Clear();
            UpdateCachedItemLists();
            RefreshWeaponDisplay();
            RefreshJuListDisplay();
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
                await ShowWaveTextEffect("게임 시작!");
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
                int currentWave = m_gameManager.GetCurrentWave();
                if (m_lastWave != currentWave)
                {
                    m_lastWave = currentWave;
                    ShowWaveTextEffect($"웨이브 {currentWave}").Forget();
                }

                int currentCoin = m_gameManager.GetCoinCount();
                if (m_lastCoin != currentCoin)
                {
                    m_lastCoin = currentCoin;
                    m_coinText.SetText("{0}", currentCoin);
                }

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

        private void OnPlayerChanged(PlayerBase player)
        {
            UpdateCachedItemLists();
            RefreshWeaponDisplay();
        }

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
            Rect joystickRect = new Rect(pos.x - (m_joystickTransform.rect.width * 0.5f),
                pos.y - (m_joystickTransform.rect.height * 0.5f), m_joystickTransform.rect.width,
                m_joystickTransform.rect.height);
            return canvasRect.rect.Overlaps(joystickRect);
        }

        private void TogglePauseMenu()
        {
            bool isActive = !m_menuPanel.activeSelf;
            m_menuPanel.SetActive(isActive);
            m_gameManager.SetMenuPopupState(isActive);
            m_joystickTransform.gameObject.SetActive(!isActive);
            if (isActive)
            {
                UpdateCachedItemLists();
                RefreshWeaponDisplay();
                RefreshJuListDisplay();
            }
        }

        #endregion

        #region 스킬 선택 및 UI 갱신

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
                var targetBtn = m_skillButtonPool.FirstOrDefault(b =>
                    b.gameObject.activeSelf && b.GetCurrentSkillData() == randomSkill);
                if (targetBtn != null)
                {
                    await targetBtn.PlaySelectionAnimation();
                }

                await OnSkillSelected(randomSkill);
            }
        }

        private void GenerateSkillChoices()
        {
            StartAutoSelectionTimer();
            foreach (var btn in m_skillButtonPool) btn.gameObject.SetActive(false);
            m_skillChoices.Clear();

            var ownedWeapons = m_gameManager.SpawnedPlayer?.Weapons.ToDictionary(w => w.skillCode) ??
                               new Dictionary<string, WeaphonBase>();
            var acquiredAccessoryCodes = new HashSet<string>(m_acquiredAccessorySkills.Select(s => s.skillCode));

            var availableSkills = m_skillDatabase.allSkills.Where(skill =>
            {
                if (skill.skillType == SkillType.Weapon)
                {
                    if (ownedWeapons.TryGetValue(skill.skillCode, out var weapon))
                    {
                        // 보유 중인 무기는 최대 레벨 및 진화가 아닐 때만 레벨업 대상으로 포함
                        return weapon.CurrentLevel < WeaphonBase.k_MaxLevel ||
                               (weapon.CurrentLevel == WeaphonBase.k_MaxLevel && !weapon.isEvolved);
                    }

                    return true; // 미보유 무기는 항상 포함
                }
                else // Passive
                {
                    return !acquiredAccessoryCodes.Contains(skill.skillCode);
                }
            }).ToList();

            int count = Mathf.Min(3, availableSkills.Count);
            while (m_skillChoices.Count < count)
            {
                var skill = availableSkills[Random.Range(0, availableSkills.Count)];
                if (!m_skillChoices.Contains(skill))
                    m_skillChoices.Add(skill);
            }

            for (int i = 0; i < m_skillChoices.Count; i++)
            {
                SelectSkillBtnPrefab btn;
                if (i < m_skillButtonPool.Count)
                {
                    btn = m_skillButtonPool[i];
                }
                else
                {
                    btn = Instantiate(m_skillSelectionButtonPrefab, m_skillButtonContainer.transform);
                    m_skillButtonPool.Add(btn);
                }

                btn.gameObject.SetActive(true);
                btn.Setup(m_skillChoices[i], skill => OnSkillSelected(skill).Forget());
            }
        }

        private async UniTask OnSkillSelected(SkillData selectedSkill)
        {
            m_skillSelectionTimerCts?.Cancel();

            if (selectedSkill.skillType == SkillType.Weapon)
            {
                var ownedWeapon =
                    m_gameManager.SpawnedPlayer?.Weapons.FirstOrDefault(w => w.skillCode == selectedSkill.skillCode);
                if (ownedWeapon != null)
                {
                    // 이미 보유한 무기 -> 레벨업
                    ownedWeapon.UpgradeLevel();
                    EffectManager.Instance.PlayLevelUpEffect(m_gameManager.SpawnedPlayer
                        .GetComponent<SpriteRenderer>());
                }
                else
                {
                    // 새로운 무기 -> 장착
                    await m_gameManager.EquipNewWeapon(selectedSkill);
                }
            }
            else // Passive
            {
                m_acquiredAccessorySkills.Add(selectedSkill);
                TryUpgradeWeapon(selectedSkill.skillCode);
                if (m_gameManager.SpawnedPlayer != null)
                {
                    EffectManager.Instance.PlayLevelUpEffect(m_gameManager.SpawnedPlayer
                        .GetComponent<SpriteRenderer>());
                }
            }

            InventoryDataManagerDontdestory.Instance.AddInGameSkill(selectedSkill);
            UpdateCachedItemLists();
            RefreshWeaponDisplay();
            RefreshJuListDisplay();
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

        private void TryUpgradeWeapon(string passiveItemCode)
        {
            if (m_gameManager.SpawnedPlayer == null) return;
            var weaponToUpgrade =
                m_gameManager.SpawnedPlayer.Weapons.FirstOrDefault(w => w.upgradeItemCode == passiveItemCode);
            if (weaponToUpgrade != null)
            {
                weaponToUpgrade.UpgradeLevel();
            }
        }

        private void CloseSkillSelection()
        {
            m_skillSelectionTimerCts?.Cancel();
            m_skillSelectionPanel.SetActive(false);
            m_isSkillSelectionActive = false;
            if (m_countdownText != null) m_countdownText.gameObject.SetActive(false);
            if (m_countDownSlider != null) m_countDownSlider.gameObject.SetActive(false);
            m_gameManager.SetMenuPopupState(false);
        }

        private void UpdateCachedItemLists()
        {
            m_weaponThumbnails.Clear();
            if (m_gameManager.SpawnedPlayer != null)
            {
                foreach (var weapon in m_gameManager.SpawnedPlayer.Weapons)
                {
                    if (weapon != null)
                    {
                        m_weaponThumbnails.Add(weapon.Thumnail);
                    }
                }
            }

            m_accessoryIcons.Clear();
            foreach (var skill in m_acquiredAccessorySkills)
            {
                m_accessoryIcons.Add(skill.skillIcon);
            }
        }

        private void RefreshWeaponDisplay()
        {
            for (int i = 0; i < m_weaponUIList.Count; i++)
            {
                var slotImage = m_weaponUIList[i];
                if (slotImage == null) continue;
                if (i < m_weaponThumbnails.Count && m_weaponThumbnails[i] != null)
                {
                    slotImage.gameObject.SetActive(true);
                    slotImage.sprite = m_weaponThumbnails[i];
                }
                else
                {
                    slotImage.gameObject.SetActive(false);
                }
            }
        }

        private void RefreshJuListDisplay()
        {
            for (int i = 0; i < m_juListUIList.Count; i++)
            {
                var slot = m_juListUIList[i];
                if (slot == null) continue;
                if (i < m_accessoryIcons.Count && m_accessoryIcons[i] != null)
                {
                    slot.gameObject.SetActive(true);
                    slot.sprite = m_accessoryIcons[i];
                }
                else
                {
                    slot.gameObject.SetActive(false);
                }
            }
        }

        #endregion

        #region 게임 종료 처리

        private void UpdateGameOverUI()
        {
            m_gameOverText.text = "게임 종료";
            m_gameOverCoinText.SetText("코인: {0}", m_gameManager.GetCoinCount());
            m_gameOverWaveText.SetText("웨이브: {0}", m_gameManager.GetCurrentWave());
            m_gameOverMobCountText.SetText("처치 수: {0}", m_gameManager.GetMobKillCount());
        }

        private void ExitToLobby()
        {
            m_gameOverPanel.SetActive(false);
            SceneLoader.Instance.LoadLobbyScene();
        }

        private void RestartGame()
        {
            m_gameOverPanel.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()
                .name);
        }

        #endregion
    }
}