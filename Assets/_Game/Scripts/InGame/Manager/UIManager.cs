using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using R3;
using UnityEngine;
using DG.Tweening;
using InGame.Player.Player_Base;
using UnityEngine.UI;
using InGame.Lobby;
using InGame.UI.ViewModels;
using InGame.UI.Views;

namespace InGame.Managers
{
    /// <summary>
    /// [설명]: 인게임의 모든 UI 요소(HUD, 팝업, 메뉴)를 총괄 관리하는 매니저 클래스입니다.
    /// ViewModel과 View 사이를 연결(Binding)하고, 사용자 입력 및 게임 이벤트를 처리합니다.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        #region 에디터 설정

        [Header("하위 View 및 팝업")]
        [SerializeField, Tooltip("인게임 HUD (체력, 경험치 등)")] private InGameHUDView m_hudView;
        [SerializeField, Tooltip("스킬 선택 팝업 View")] private InGameSkillView m_skillView;
        [SerializeField, Tooltip("게임 오버 팝업")] private GameOverPopup m_gameOverPopup;
        [SerializeField, Tooltip("일시정지 메뉴 패널")] private GameObject m_menuPanel;
        [SerializeField, Tooltip("확인/취소 공용 팝업")] private ConfirmPopup m_confirmPopup;

        [Header("메뉴 및 버튼")]
        [SerializeField, Tooltip("일시정지/메뉴 버튼")] private Button m_menuButton;
        [SerializeField, Tooltip("설정 팝업 열기 버튼")] private Button m_settingButton;
        [SerializeField, Tooltip("로비로 나가기 버튼")] private Button m_exitButton;
        [SerializeField, Tooltip("메뉴 닫기 버튼")] private Button m_closeButton;

        [Header("조작계 (Joystick & Input)")]
        [SerializeField, Tooltip("가상 조이스틱 컴포넌트")] private VariableJoystick m_variableJoystick;
        [SerializeField, Tooltip("조이스틱 UI 트랜스폼")] private RectTransform m_joystickTransform;
        [SerializeField, Tooltip("자동 공격 토글")] private Toggle m_autoAttackToggle;

        [Header("데이터 및 기타")]
        [SerializeField, Tooltip("설정 데이터 (저장/로드)")] private SettingsData m_settingsData;
        [SerializeField, Tooltip("스킬 데이터베이스")] private SkillDatabase m_skillDatabase;
        [SerializeField, Tooltip("웨이브 시작 카운트다운 텍스트")] private TMP_Text m_mobWaveText;

        #endregion

        #region 내부 필드 및 상태

        private InGameViewModel m_viewModel;
        private GameManager m_gameManager;
        private PlayerController m_playerController;

        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        private int m_pendingSkillSelections;
        private bool m_isSkillSelectionActive;

        private static readonly Vector2 k_DefaultJoystickPosition = new Vector2(300, 300);

        #endregion

        #region 유니티 생명주기

        private void Awake()
        {
            m_gameManager = GameManager.Instance;

            // ViewModel 생성 (MVVM 패턴에 따라 UIManager가 소유)
            m_viewModel = new InGameViewModel(m_skillDatabase);

            InitializeViews();
            BindUIEvents();
            BindViewModel();
        }

        private void Start()
        {
            if (m_gameManager != null)
            {
                m_playerController = m_gameManager.PlayerController;
                m_variableJoystick = m_gameManager.Joystick;
            }

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
        }

        #endregion

        #region 초기화 및 바인딩

        /// <summary>
        /// [설명]: 하위 뷰들을 초기화하고 뷰모델과 연결합니다.
        /// </summary>
        private void InitializeViews()
        {
            if (m_hudView != null)
            {
                m_hudView.Bind(m_viewModel);
            }

            if (m_skillView != null)
            {
                m_skillView.Initialize(() => m_viewModel.RefreshSkillChoices());
            }

            if (m_gameOverPopup != null)
            {
                m_gameOverPopup.Setup(RestartGame, ExitToLobby);
            }

            if (m_confirmPopup != null)
            {
                m_confirmPopup.Bind(m_viewModel.ConfirmPopupViewModel);
            }

            m_viewModel.UpdateIconLists();
        }

        /// <summary>
        /// [설명]: UI 요소들의 클릭 이벤트를 구독합니다.
        /// </summary>
        private void BindUIEvents()
        {
            if (m_menuButton != null)
            {
                m_menuButton.OnClickAsObservable()
                    .Subscribe(_ => TogglePauseMenu())
                    .AddTo(m_disposables);
            }

            if (m_exitButton != null)
            {
                m_exitButton.OnClickAsObservable()
                    .Subscribe(_ => ShowExitConfirmPopup())
                    .AddTo(m_disposables);
            }

            if (m_closeButton != null)
            {
                m_closeButton.OnClickAsObservable()
                    .Subscribe(_ => TogglePauseMenu())
                    .AddTo(m_disposables);
            }

            if (m_settingButton != null)
            {
                m_settingButton.OnClickAsObservable()
                    .Subscribe(_ =>
                    {
                        if (m_gameManager != null) m_gameManager.OpenOptionPopup();
                    })
                    .AddTo(m_disposables);
            }

            if (m_autoAttackToggle != null)
            {
                m_autoAttackToggle.OnValueChangedAsObservable()
                    .Subscribe(OnAutoAttackToggleChanged)
                    .AddTo(m_disposables);
            }
        }

        /// <summary>
        /// [설명]: 뷰모델의 ReactiveProperty 상태 변화를 구독하여 뷰를 업데이트합니다.
        /// </summary>
        private void BindViewModel()
        {
            // 1. 스킬 선택 활성화 상태 동기화
            m_viewModel.IsSkillSelectionActive
                .Subscribe(isActive =>
                {
                    m_isSkillSelectionActive = isActive;

                    if (m_gameManager != null)
                    {
                        m_gameManager.SetMenuPopupState(isActive);
                    }

                    if (m_skillView != null)
                    {
                        m_skillView.Show(isActive);
                    }
                })
                .AddTo(m_disposables);

            // 2. 스킬 목록 데이터 바인딩
            m_viewModel.SkillChoices
                .Subscribe(choices =>
                {
                    if (m_skillView != null)
                    {
                        m_skillView.RefreshSkillChoices(choices.ToList(), skill => OnSkillSelectedAsync(skill).Forget());
                    }
                })
                .AddTo(m_disposables);

            // 3. 타이머 바인딩
            m_viewModel.SelectionTimer
                .Subscribe(time =>
                {
                    const float maxTime = 6.0f;
                    if (m_skillView != null)
                    {
                        m_skillView.UpdateTimer(time / maxTime, Mathf.CeilToInt(time));
                    }
                })
                .AddTo(m_disposables);

            // 4. 자동 선택 이벤트 처리
            m_viewModel.OnAutoSelectSkill
                .Subscribe(async skill =>
                {
                    if (m_skillView != null)
                    {
                        await m_skillView.PlaySelectionAnimation(skill);
                    }
                    OnSkillSelectedAsync(skill).Forget();
                })
                .AddTo(m_disposables);
        }

        #endregion

        #region 이벤트 핸들러

        private void SubscribeToEvents()
        {
            if (GameManager.Instance == null || GameManager.Instance.State == null)
            {
                return;
            }

            GameManager.Instance.State.OnGameStart += OnGameStart;
            GameManager.Instance.State.OnGamePause += OnGamePause;
            GameManager.Instance.State.OnGameResume += OnGameResume;
            GameManager.Instance.State.OnGameOver += OnGameOver;

            PlayerBase.OnExpChanged += OnPlayerExpChanged;
            PlayerBase.OnLevelUp += OnPlayerLevelUp;
            GameManager.OnPlayerChanged += OnPlayerChanged;

            SettingsData.OnSettingsChanged += ApplyJoystickSettings;
        }

        private void UnsubscribeFromEvents()
        {
            if (GameManager.Instance == null || GameManager.Instance.State == null)
            {
                return;
            }

            GameManager.Instance.State.OnGameStart -= OnGameStart;
            GameManager.Instance.State.OnGamePause -= OnGamePause;
            GameManager.Instance.State.OnGameResume -= OnGameResume;
            GameManager.Instance.State.OnGameOver -= OnGameOver;

            PlayerBase.OnExpChanged -= OnPlayerExpChanged;
            PlayerBase.OnLevelUp -= OnPlayerLevelUp;
            GameManager.OnPlayerChanged -= OnPlayerChanged;

            SettingsData.OnSettingsChanged -= ApplyJoystickSettings;
        }

        private void OnGameStart()
        {
            if (m_settingsData != null)
            {
                m_settingsData.LoadSettings();
            }

            ApplyJoystickSettings();

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.LoadSoundSetting();
            }

            InitializeViews();
        }

        private void OnGamePause()
        {
            if (m_joystickTransform != null)
            {
                m_joystickTransform.gameObject.SetActive(false);
            }
        }

        private void OnGameResume()
        {
            if (m_joystickTransform != null)
            {
                m_joystickTransform.gameObject.SetActive(true);
            }

            ApplyJoystickSettings();
        }

        private void OnGameOver()
        {
            if (m_gameOverPopup != null)
            {
                m_gameOverPopup.Show(
                    m_viewModel.CoinCount.CurrentValue,
                    m_viewModel.CurrentWave.CurrentValue,
                    m_viewModel.KillCount.CurrentValue
                );
            }

            if (m_joystickTransform != null)
            {
                m_joystickTransform.gameObject.SetActive(false);
            }

            if (m_variableJoystick != null)
            {
                m_variableJoystick.OnPointerUp(null);
                m_variableJoystick.enabled = false;
            }

            if (m_autoAttackToggle != null)
            {
                m_autoAttackToggle.isOn = false;
            }
        }

        private void OnPlayerChanged(PlayerBase player)
        {
            m_viewModel.UpdateIconLists();
        }

        private void OnPlayerExpChanged(float currentExp, float maxExp)
        {
            // MVVM 바인딩에 의해 자동 처리됩니다.
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

        #region 게임 로직 및 연출

        /// <summary>
        /// [설명]: 게임 시작 카운트다운을 표시하고 게임을 시작합니다.
        /// </summary>
        public async UniTaskVoid StartGameCountdown()
        {
            if (m_mobWaveText == null)
            {
                GameManager.Instance.State.StartGame();
                return;
            }

            try
            {
                if (m_joystickTransform != null)
                {
                    m_joystickTransform.gameObject.SetActive(false);
                }

                m_mobWaveText.gameObject.SetActive(true);
                await ShowWaveTextEffect("3..", 0.5f, 0.2f);
                await ShowWaveTextEffect("2..", 0.5f, 0.2f);
                await ShowWaveTextEffect("1..", 0.5f, 0.2f);
                await ShowWaveTextEffect("게임 시작!");

                if (m_joystickTransform != null)
                {
                    m_joystickTransform.gameObject.SetActive(true);
                }

                GameManager.Instance.State.StartGame();
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[UIManager] 카운트다운 오류: {ex.Message}", LogManager.LogCategory.UIManager);
                if (m_joystickTransform != null)
                {
                    m_joystickTransform.gameObject.SetActive(true);
                }
                GameManager.Instance.State.StartGame();
            }
        }

        private async UniTask ShowWaveTextEffect(string text, float holdDuration = 1.0f, float fadeDuration = 0.5f)
        {
            if (m_mobWaveText == null)
            {
                return;
            }

            m_mobWaveText.text = text;
            m_mobWaveText.alpha = 0f;
            m_mobWaveText.gameObject.SetActive(true);

            await m_mobWaveText.DOFade(1f, fadeDuration).AsyncWaitForCompletion();
            await UniTask.Delay(TimeSpan.FromSeconds(holdDuration));
            await m_mobWaveText.DOFade(0f, fadeDuration).AsyncWaitForCompletion();

            m_mobWaveText.gameObject.SetActive(false);
        }

        #endregion

        #region 스킬 선택 시스템

        private void ProcessSkillSelectionQueue()
        {
            if (m_pendingSkillSelections > 0)
            {
                m_viewModel.StartSkillSelection();
            }
        }

        private async UniTaskVoid OnSkillSelectedAsync(SkillData selectedSkill)
        {
            m_viewModel.EndSkillSelection();

            if (m_gameManager != null && m_gameManager.SpawnedPlayer != null)
            {
                var player = m_gameManager.SpawnedPlayer;
                var renderer = player.GetComponent<SpriteRenderer>();

                if (selectedSkill.skillType == SkillType.Weapon)
                {
                    var ownedWeapon = player.Weapons.FirstOrDefault(w => w.SkillCode == selectedSkill.skillCode);
                    if (ownedWeapon != null)
                    {
                        ownedWeapon.LevelUp();
                        EffectManager.Instance.PlayLevelUpEffect(renderer);
                    }
                    else
                    {
                        await m_gameManager.EquipNewWeapon(selectedSkill);
                    }
                }
                else
                {
                    TryUpgradeWeaponByPassive(selectedSkill.skillCode);
                    EffectManager.Instance.PlayLevelUpEffect(renderer);
                }
            }

            if (InventoryDataManager.Instance != null)
            {
                InventoryDataManager.Instance.AddInGameSkill(selectedSkill);
            }

            m_viewModel.UpdateIconLists();

            m_pendingSkillSelections--;
            if (m_pendingSkillSelections > 0)
            {
                ProcessSkillSelectionQueue();
            }
            else
            {
                m_viewModel.EndSkillSelection();
            }
        }

        private void TryUpgradeWeaponByPassive(string passiveItemCode)
        {
            if (m_gameManager == null || m_gameManager.SpawnedPlayer == null)
            {
                return;
            }

            var weaponToUpgrade = m_gameManager.SpawnedPlayer.Weapons
                .FirstOrDefault(w => w.SkillData?.upgradeItemCode == passiveItemCode);

            if (weaponToUpgrade != null)
            {
                weaponToUpgrade.LevelUp();
            }
        }

        #endregion

        #region 설정 및 조작

        private void OnAutoAttackToggleChanged(bool isOn)
        {
            if (m_playerController != null)
            {
                m_playerController.AutoAttackEnabledByToggle = isOn;
            }
        }

        private void ApplyJoystickSettings()
        {
            if (m_variableJoystick == null || m_settingsData == null || m_joystickTransform == null)
            {
                return;
            }

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
            if (m_joystickTransform == null)
            {
                return false;
            }

            var canvas = m_joystickTransform.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return false;
            }

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                return false;
            }

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
            if (m_menuPanel == null)
            {
                return;
            }

            bool isActive = !m_menuPanel.activeSelf;
            m_menuPanel.SetActive(isActive);

            if (m_gameManager != null)
            {
                m_gameManager.SetMenuPopupState(isActive);
            }

            if (m_joystickTransform != null)
            {
                m_joystickTransform.gameObject.SetActive(!isActive);
            }

            if (isActive)
            {
                m_viewModel.UpdateIconLists();
            }
        }

        #endregion

        #region 팝업 및 종료 처리

        private void ShowExitConfirmPopup()
        {
            if (m_confirmPopup != null)
            {
                m_viewModel.ConfirmPopupViewModel.ShowPopup(
                    "게임 종료",
                    "로비로 돌아가시겠습니까?\n진행 상황이 저장됩니다.",
                    onConfirm: ExitToLobby
                );
            }
            else
            {
                ExitToLobby();
            }
        }

        private async void ExitToLobby()
        {
            if (GameManager.Instance != null)
            {
                await GameManager.Instance.SaveGameResult();
            }

            SceneLoader.Instance.LoadLobbyScene();
        }

        private void RestartGame()
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            UnityEngine.SceneManagement.SceneManager.LoadScene(currentScene);
        }

        #endregion
    }
}