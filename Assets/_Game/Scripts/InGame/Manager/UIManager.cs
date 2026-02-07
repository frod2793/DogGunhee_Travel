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
using Random = UnityEngine.Random;
using InGame.Lobby;
using InGame.vamsir;
using InGame.Weapon.Base;
using InGame.UI.ViewModels;
using InGame.UI.Views;

namespace InGame.Manager
{
    public class UIManager : MonoBehaviour
    {
        [Header("하위 View 및 팝업")]
        [SerializeField] private InGameHUDView m_hudView;
        [SerializeField] private InGameSkillView m_skillView;
        [SerializeField] private GameOverPopup m_gameOverPopup;
        [SerializeField] private GameObject m_menuPanel;

        [Header("메뉴 및 설정")]
        [SerializeField] private Button m_menuButton;
        [SerializeField] private Button m_settingButton;
        [SerializeField] private Button m_exitButton;
        [SerializeField] private SettingsData m_settingsData;

        [Header("조작계")]
        [SerializeField] private VariableJoystick m_variableJoystick;
        [SerializeField] private RectTransform m_joystickTransform;
        [SerializeField] private Toggle m_autoAttackToggle;

        [Header("데이터")]
        [SerializeField] private SkillDatabase m_skillDatabase;
        [SerializeField] private TMP_Text m_mobWaveText; // 카운트다운용으로 유지

        private InGameViewModel m_viewModel;

        private GameManager m_gameManager;
        private PlayerControll m_playerController;
        private CancellationTokenSource m_skillSelectionTimerCts;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        private readonly List<SkillData> m_skillChoices = new List<SkillData>(3);

        private int m_pendingSkillSelections = 0;
        private bool m_isSkillSelectionActive = false;

        private static readonly Vector2 k_DefaultJoystickPosition = new Vector2(300, 300);

        #region Unity 라이프사이클

        private void Awake()
        {
            m_gameManager = GameManager.Instance;
            m_viewModel = new InGameViewModel();
            
            InitializeViews();
            BindUIEvents();
        }

        private void InitializeViews()
        {
            if (m_hudView != null) m_hudView.Bind(m_viewModel);
            if (m_skillView != null) m_skillView.Initialize(() => GenerateSkillChoices());
            if (m_gameOverPopup != null) m_gameOverPopup.Setup(RestartGame, ExitToLobby);
        }

        private void Start()
        {
            m_playerController = m_gameManager.PlayerController;
            m_variableJoystick = m_gameManager.Joystick;

            if (m_settingsData != null)
            {
                m_settingsData.LoadSettings();
                ApplyJoystickSettings();
            }

            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            m_disposables.Dispose();
            m_viewModel?.Dispose();
            m_skillSelectionTimerCts?.Cancel();
            m_skillSelectionTimerCts?.Dispose();
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

            if (m_autoAttackToggle != null)
            {
                m_autoAttackToggle.OnValueChangedAsObservable().Subscribe(OnAutoAttackToggleChanged)
                    .AddTo(m_disposables);
            }
        }

        private void InitializeUI()
        {
            // ViewModel 데이터 갱신 유도
            m_viewModel.UpdateIconLists();
        }

        #endregion

        #region 게임 상태 핸들러

        private void OnGameStart()
        {
            m_settingsData.LoadSettings();
            ApplyJoystickSettings();
            SoundManager.Instance.LoadSoundSetting();
            InitializeUI();
        }

        private void OnGamePause() => m_joystickTransform.gameObject.SetActive(false);

        private void OnGameResume()
        {
            m_joystickTransform.gameObject.SetActive(true);
            ApplyJoystickSettings();
        }

        private void OnGameOver()
        {
            if (m_gameOverPopup != null)
            {
                // R3 ReadOnlyReactiveProperty의 현재 값 접근자가 Value가 아닐 경우 CurrentValue를 사용해봅니다.
                m_gameOverPopup.Show(m_viewModel.CoinCount.CurrentValue, m_viewModel.CurrentWave.CurrentValue, m_viewModel.KillCount.CurrentValue);
            }
            
            m_joystickTransform.gameObject.SetActive(false);
            if (m_variableJoystick != null)
            {
                m_variableJoystick.OnPointerUp(null);
                m_variableJoystick.enabled = false;
            }

            if (m_autoAttackToggle != null) m_autoAttackToggle.isOn = false;
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
                // 카운트다운 시작 시 조이스틱 비활성화
                if (m_joystickTransform != null) m_joystickTransform.gameObject.SetActive(false);

                m_mobWaveText.gameObject.SetActive(true);
                await ShowWaveTextEffect("3..", 0.5f, 0.2f);
                await ShowWaveTextEffect("2..", 0.5f, 0.2f);
                await ShowWaveTextEffect("1..", 0.5f, 0.2f);
                await ShowWaveTextEffect("게임 시작!");
                
                // 카운트다운 종료 후 조이스틱 활성화
                if (m_joystickTransform != null) m_joystickTransform.gameObject.SetActive(true);

                PlayStateManager.instance.StartGame();
            }
            catch (Exception ex)
            {
                LogManager.LogError($"카운트다운 오류: {ex.Message}", LogManager.LogCategory.VamserLikeUI);
                if (m_joystickTransform != null) m_joystickTransform.gameObject.SetActive(true); // 오류 시에도 활성화 보장
                PlayStateManager.instance.StartGame();
            }
        }

        #endregion

        // UpdateUILoopAsync는 ViewModel이 내부적으로 R3 Interval로 처리하므로 제거 가능

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

        #region 플레이어 이벤트

        private void OnPlayerChanged(PlayerBase player)
        {
            m_viewModel.UpdateIconLists();
        }

        private void OnPlayerExpChanged(float currentExp, float maxExp)
        {
            // ViewModel이 처리함
        }

        private void OnPlayerLevelUp(float newLevel)
        {
            if (newLevel >= 2)
            {
                m_pendingSkillSelections++;
                if (!m_isSkillSelectionActive)
                {
                    ProcessSkillSelectionQueue();
                }
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
                m_viewModel.UpdateIconLists();
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
            if (m_skillView != null) m_skillView.Show(true);
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
            while (timer > 0f && !token.IsCancellationRequested)
            {
                if (m_skillView != null)
                    m_skillView.UpdateTimer(timer / duration, Mathf.CeilToInt(timer));
                
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
                int randomIndex = UnityEngine.Random.Range(0, m_skillChoices.Count);
                var randomSkill = m_skillChoices[randomIndex];
                
                if (m_skillView != null)
                    await m_skillView.PlaySelectionAnimation(randomSkill);

                await OnSkillSelected(randomSkill);
            }
        }

        private void GenerateSkillChoices()
        {
            StartAutoSelectionTimer();
            m_skillChoices.Clear();

            var ownedWeapons = m_gameManager.SpawnedPlayer?.Weapons.ToDictionary(w => w.skillCode) ??
                               new Dictionary<string, WeaponBase>();
            
            // InventoryDataManager에서 획득한 스킬을 확인
            var acquiredAccessoryCodes = new HashSet<string>();
            if (InventoryDataManager.Instance != null)
            {
                foreach(var s in InventoryDataManager.Instance.InGameAcquiredSkills)
                {
                    if (s.skillType == SkillType.Passive) acquiredAccessoryCodes.Add(s.skillCode);
                }
            }

            var availableSkills = m_skillDatabase.allSkills.Where(skill =>
            {
                if (skill.skillType == SkillType.Weapon)
                {
                    if (ownedWeapons.TryGetValue(skill.skillCode, out var weapon))
                    {
                        // 보유 중인 무기는 최대 레벨 및 진화가 아닐 때만 레벨업 대상으로 포함
                        return weapon.CurrentLevel < WeaponBase.k_MaxLevel ||
                               (weapon.CurrentLevel == WeaponBase.k_MaxLevel && !weapon.isEvolved);
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

            if (m_skillView != null)
                m_skillView.RefreshSkillChoices(m_skillChoices, skill => OnSkillSelected(skill).Forget());
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
                // m_acquiredAccessorySkills 대신 직접 InventoryDataManager에 추가 (이후 ViewModel의 UpdateIconLists에서 반영)
                // InventoryDataManager.Instance.AddInGameSkill(selectedSkill)은 아래에서 공통으로 처리됨
                TryUpgradeWeapon(selectedSkill.skillCode);
                if (m_gameManager.SpawnedPlayer != null)
                {
                    EffectManager.Instance.PlayLevelUpEffect(m_gameManager.SpawnedPlayer
                        .GetComponent<SpriteRenderer>());
                }
            }

            InventoryDataManager.Instance.AddInGameSkill(selectedSkill);
            m_viewModel.UpdateIconLists();
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
            if (m_skillView != null) m_skillView.Show(false);
            m_isSkillSelectionActive = false;
            m_gameManager.SetMenuPopupState(false);
        }

        #endregion

        #region 게임 종료 처리

        private void ResumeGame()
        {
            m_gameManager.SetMenuPopupState(false);
            m_menuPanel.SetActive(false);
            m_joystickTransform.gameObject.SetActive(true);
        }

        private void ExitToLobby()
        {
            SceneLoader.Instance.LoadLobbyScene();
        }

        private void RestartGame()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        #endregion
    }
}