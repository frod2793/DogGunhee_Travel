using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using R3;
using UnityEngine;
using DG.Tweening;
using InGame.Player.Player_Base;
using UnityEngine.UI;
using InGame.UI.ViewModels;
using InGame.UI.Views;
using InGame.Core.Interfaces;

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
        [SerializeField, Tooltip("게임 클리어 팝업")] private GameClearPopupView m_gameClearPopup;
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
        
        // [수정]: 인터페이스 기반 의존성
        private IGameStateService m_gameState;
        private IPlayerContext m_playerCtx;
        private ICombatContext m_combatCtx;
        private IGameDataProvider m_dataProvider;
        private IInventoryContext m_inventoryCtx;

        private PlayerController m_playerController;
        private Services.ISoundManager m_soundManager;
        private ISceneLoader m_sceneLoader;
        private UI.IPopupService m_popupService;
        private IEffectService m_effectService;

        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        private int m_pendingSkillSelections;
        private bool m_isSkillSelectionActive;
        private bool m_isInitialized; // [추가]: 초기화 완료 여부 플래그
        private bool m_isCountdownActive; // [추가]: 카운트다운 중복 실행 방지 플래그

        private GameClearPopupViewModel m_gameClearPopupViewModel;

        private static readonly Vector2 k_DefaultJoystickPosition = new Vector2(300, 300);

        #endregion

        #region 유니티 생명주기

        private void Start()
        {
            // [수정]: Start()에서는 SubscribeToEvents()를 호출하지 않습니다.
            // 의존성 주입(Initialize)이 완료된 시점에서만 이벤트를 구독합니다.
            if (m_playerCtx != null)
            {
                m_playerController = m_playerCtx.PlayerController;
                m_variableJoystick = m_playerCtx.Joystick;
            }

            if (m_settingsData != null)
            {
                m_settingsData.LoadSettings();
                ApplyJoystickSettings();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();

            m_disposables.Dispose();
            m_viewModel?.Dispose();
            m_gameClearPopupViewModel?.Dispose();
        }

        #endregion

        #region 초기화 및 바인딩

        /// <summary>
        /// [설명]: 게임 시스템 및 사운드 매니저 등 외부 의존성을 주입합니다.
        /// </summary>
        public void Initialize(
            IGameStateService gameState,
            IPlayerContext playerContext,
            ICombatContext combatContext,
            IGameDataProvider dataProvider,
            IInventoryContext inventoryContext,
            Services.ISoundManager soundManager,
            ISceneLoader sceneLoader,
            UI.IPopupService popupService,
            IEffectService effectService)
        {
            if (m_isInitialized) return;
            
            m_gameState = gameState;
            m_playerCtx = playerContext;
            m_combatCtx = combatContext;
            m_dataProvider = dataProvider;
            m_inventoryCtx = inventoryContext;
            m_soundManager = soundManager;
            m_sceneLoader = sceneLoader;
            m_popupService = popupService;
            m_effectService = effectService;

            // 의존성이 주입된 시점에 즉시 이벤트 구독 시도
            SubscribeToEvents();

            // ViewModel 생성 및 지연 바인딩
            m_viewModel = new InGameViewModel(m_skillDatabase, m_gameState, m_playerCtx, m_combatCtx, m_dataProvider, m_inventoryCtx);
            InitializeViews();
            BindUIEvents();
            BindViewModel();

            m_isInitialized = true;
            LogManager.Log("[UIManager] 초기화 완료", LogManager.LogCategory.UIManager);
        }

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

            if (m_gameClearPopup != null)
            {
                m_gameClearPopupViewModel = new GameClearPopupViewModel();
                m_gameClearPopup.Bind(m_gameClearPopupViewModel);
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
                        if (m_gameState != null) m_gameState.OpenOptionPopup();
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

                    if (m_gameState != null)
                    {
                        m_gameState.SetMenuPopupState(isActive);
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

        /// <summary>
        /// [설명]: 전역 게임 상태 이벤트를 구독합니다.
        /// Initialize()에서만 호출되므로 m_gameState가 항상 유효합니다.
        /// </summary>
        private bool m_isEventsSubscribed;
        private void SubscribeToEvents()
        {
            if (m_isEventsSubscribed) return;
            m_isEventsSubscribed = true;

            if (m_gameState != null && m_gameState.State != null)
            {
                m_gameState.State.OnGameStart += OnGameStart;
                m_gameState.State.OnGamePause += OnGamePause;
                m_gameState.State.OnGameResume += OnGameResume;
                m_gameState.State.OnGameOver += OnGameOver;
                LogManager.Log("[UIManager] 게임 상태 이벤트 구독 성공", LogManager.LogCategory.UIManager);
            }
            else
            {
                LogManager.LogError("[UIManager] GameState가 null입니다! Initialize가 정상적으로 호출되지 않았습니다.", LogManager.LogCategory.UIManager);
            }

            PlayerBase.OnExpChanged += OnPlayerExpChanged;
            PlayerBase.OnLevelUp += OnPlayerLevelUp;
            
            if (m_playerCtx != null)
            {
                m_playerCtx.OnPlayerChanged += OnPlayerChanged;
            }

            SettingsData.OnSettingsChanged += ApplyJoystickSettings;
        }

        private void UnsubscribeFromEvents()
        {
            if (!m_isEventsSubscribed) return;
            m_isEventsSubscribed = false;

            if (m_gameState != null && m_gameState.State != null)
            {
                m_gameState.State.OnGameStart -= OnGameStart;
                m_gameState.State.OnGamePause -= OnGamePause;
                m_gameState.State.OnGameResume -= OnGameResume;
                m_gameState.State.OnGameOver -= OnGameOver;
            }

            PlayerBase.OnExpChanged -= OnPlayerExpChanged;
            PlayerBase.OnLevelUp -= OnPlayerLevelUp;
            if (m_playerCtx != null)
            {
                m_playerCtx.OnPlayerChanged -= OnPlayerChanged;
            }

            SettingsData.OnSettingsChanged -= ApplyJoystickSettings;
        }

        private void OnGameStart()
        {
            if (m_settingsData != null)
            {
                m_settingsData.LoadSettings();
            }

            ApplyJoystickSettings();

            if (m_soundManager != null)
            {
                m_soundManager.LoadSoundSetting();
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


        /// <summary>
        /// [설명]: 게임 종료 이벤트를 수신하여 결과 팝업을 출력합니다.
        /// </summary>
        private void OnGameOver()
        {
            LogManager.Log("[UIManager] OnGameOver 이벤트 수신", LogManager.LogCategory.UIManager);

            // [변경]: 이벤트발생 즉시(동기적으로) 렌더러 참조를 확보합니다.
            // GameManager가 플레이어 참조를 null로 밀어버리기 전에 참조를 따야 합니다.
            SpriteRenderer playerRenderer = null;
            if (m_playerCtx != null && m_playerCtx.SpawnedPlayer != null)
            {
                playerRenderer = m_playerCtx.SpawnedPlayer.GetComponentInChildren<SpriteRenderer>(true);
            }
            if (playerRenderer == null)
            {
                // GameManager에서 플레이어 참조를 이미 해제한 경우, 씬에서 직접 검색
                var playerBase = FindAnyObjectByType<PlayerBase>();
                if (playerBase != null)
                {
                    playerRenderer = playerBase.GetComponentInChildren<SpriteRenderer>(true);
                }
            }

            LogManager.Log($"[UIManager] OnGameOver: Renderer 확보 성공 ({playerRenderer?.name ?? "null"})", LogManager.LogCategory.UIManager);
            OnGameOverAsync(playerRenderer).Forget();
        }

        /// <summary>
        /// [설명]: 게임 종료 연출 및 팝업 출력을 비동기로 처리합니다.
        /// 플레이어 사망 시 애니메이션을 고려하여 지연 후 팝업을 표시합니다.
        /// </summary>
        private async UniTaskVoid OnGameOverAsync(SpriteRenderer playerRenderer)
        {
            // 죠이스틱 등 컨트롤 UI는 즉시 비활성화
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

            if (m_gameState != null && m_gameState.IsCleared)
            {
                // 게임 클리어 시의 처리
                if (m_gameClearPopup != null && m_gameClearPopupViewModel != null)
                {
                    int stars = 1;
                    if (m_playerCtx != null && m_playerCtx.SpawnedPlayer != null)
                    {
                        float currentHealth = m_playerCtx.SpawnedPlayer.CurrentHealth;
                        float maxHealth = m_playerCtx.SpawnedPlayer.MaxHealth;
                        float hpRatio = maxHealth > 0 ? (float)currentHealth / maxHealth : 0;
                        
                        if (hpRatio >= 0.8f) stars = 3;
                        else if (hpRatio >= 0.4f) stars = 2;
                    }

                    m_gameClearPopupViewModel.Show(
                        m_viewModel.CoinCount.CurrentValue,
                        m_viewModel.CurrentWave.CurrentValue,
                        m_viewModel.KillCount.CurrentValue,
                        stars,
                        RestartGame,
                        ExitToLobby
                    );
                }
            }
            else
            {
                // 플레이어 사망 시: 애니메이션이 재생될 시간을 벌기 위해 지연 후 팝업 출력
                try
                {
                    LogManager.Log("[UIManager] OnGameOverAsync: Delay 시작 (1.5s)", LogManager.LogCategory.UIManager);
                    await UniTask.Delay(TimeSpan.FromSeconds(1.5f), ignoreTimeScale: true, cancellationToken: this.GetCancellationTokenOnDestroy());
                    LogManager.Log("[UIManager] OnGameOverAsync: Delay 종료", LogManager.LogCategory.UIManager);
                    
                    // 지연 후, 애니메이션이 끝난 시점의 최종 스프라이트를 추출합니다.
                    Sprite finalDeadSprite = null;
                    if (playerRenderer != null)
                    {
                        finalDeadSprite = playerRenderer.sprite;
                        LogManager.Log($"[UIManager] OnGameOverAsync: 최종 스프라이트 추출 성공 ({(finalDeadSprite != null ? finalDeadSprite.name : "null")})", LogManager.LogCategory.UIManager);
                    }
                    else
                    {
                        LogManager.LogWarning("[UIManager] OnGameOverAsync: 렌더러 참조가 유효하지 않습니다.", LogManager.LogCategory.UIManager);
                    }

                    if (m_gameOverPopup != null)
                    {
                        LogManager.Log("[UIManager] OnGameOverAsync: GameOverPopup.Show() 호출", LogManager.LogCategory.UIManager);
                        m_gameOverPopup.Show(
                            m_viewModel.CoinCount.CurrentValue,
                            m_viewModel.CurrentWave.CurrentValue,
                            m_viewModel.KillCount.CurrentValue,
                            finalDeadSprite
                        );
                    }
                    else
                    {
                        LogManager.LogError("[UIManager] OnGameOverAsync: m_gameOverPopup이 null입니다!", LogManager.LogCategory.UIManager);
                    }
                }
                catch (OperationCanceledException)
                {
                    LogManager.Log("[UIManager] OnGameOverAsync: 비동기 작업 취소됨", LogManager.LogCategory.UIManager);
                    return;
                }
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

        private int m_lastProcessedLevel = -1;
        private void OnPlayerLevelUp(float newLevel)
        {
            int level = Mathf.FloorToInt(newLevel);
            if (level < 2 || level <= m_lastProcessedLevel)
            {
                return;
            }

            m_lastProcessedLevel = level;
            m_pendingSkillSelections++;
            
            if (!m_isSkillSelectionActive)
            {
                ProcessSkillSelectionQueue();
            }
        }

        #endregion

        #region 게임 로직 및 연출

        /// <summary>
        /// [설명]: 게임 시작 카운트다운을 표시하고 게임을 시작합니다.
        /// </summary>
        public async UniTaskVoid StartGameCountdown()
        {
            if (m_isCountdownActive)
            {
                LogManager.LogWarning("[UIManager] 이미 카운트다운이 진행 중입니다.", LogManager.LogCategory.UIManager);
                return;
            }

            // [방어 코드]: 초기화가 완료될 때까지 잠시 대기 (최대 3초)
            float waitTimeout = 3.0f;
            while (!m_isInitialized && waitTimeout > 0)
            {
                await UniTask.DelayFrame(1, cancellationToken: this.GetCancellationTokenOnDestroy());
                waitTimeout -= Time.unscaledDeltaTime;
            }

            if (m_mobWaveText == null)
            {
                if (m_gameState != null && m_gameState.State != null)
                {
                    m_gameState.State.StartGame();
                }
                return;
            }

            m_isCountdownActive = true;
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

                if (m_gameState != null && m_gameState.State != null)
                {
                    LogManager.Log("[UIManager] 카운트다운 종료 -> StartGame 호출", LogManager.LogCategory.UIManager);
                    m_gameState.State.StartGame();
                }
                else
                {
                    LogManager.LogError("[UIManager] m_gameState가 null이라 게임을 시작할 수 없습니다!", LogManager.LogCategory.UIManager);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[UIManager] 카운트다운 오류: {ex.Message}", LogManager.LogCategory.UIManager);
                if (m_joystickTransform != null)
                {
                    m_joystickTransform.gameObject.SetActive(true);
                }
                if (m_gameState != null && m_gameState.State != null)
                    m_gameState.State.StartGame();
            }
            finally
            {
                m_isCountdownActive = false;
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

            if (m_gameState != null && m_playerCtx != null && m_playerCtx.SpawnedPlayer != null)
            {
                var player = m_playerCtx.SpawnedPlayer;
                var renderer = player.GetComponent<SpriteRenderer>();

                if (selectedSkill.skillType == SkillType.Weapon)
                {
                    var ownedWeapon = player.Weapons.FirstOrDefault(w => w.SkillCode == selectedSkill.skillCode);
                    if (ownedWeapon != null)
                    {
                        ownedWeapon.LevelUp();
                    if (m_effectService != null)
                    {
                        m_effectService.PlayLevelUpEffect(renderer);
                    }
                    }
                    else
                    {
                        // [주의]: EquipNewWeapon은 GameManager에 특화된 로직일 수 있음. 확인 필요.
                        if (m_gameState is GameManager gm)
                        {
                            await gm.EquipNewWeapon(selectedSkill);
                        }
                    }
                }
                else
                {
                    TryUpgradeWeaponByPassive(selectedSkill.skillCode);
                    if (m_effectService != null)
                    {
                        m_effectService.PlayLevelUpEffect(renderer);
                    }
                }
            }

            if (m_inventoryCtx != null)
            {
                m_inventoryCtx.AddInGameSkill(selectedSkill);
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
            if (m_playerCtx == null || m_playerCtx.SpawnedPlayer == null)
            {
                return;
            }

            var weaponToUpgrade = m_playerCtx.SpawnedPlayer.Weapons
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

            if (m_gameState != null)
            {
                m_gameState.SetMenuPopupState(isActive);
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
            if (m_gameState != null)
            {
                await m_gameState.SaveGameResult();
            }

            if (m_sceneLoader != null)
            {
                m_sceneLoader.LoadLobbyScene();
            }
        }

        private void RestartGame()
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            UnityEngine.SceneManagement.SceneManager.LoadScene(currentScene);
        }

        #endregion
    }
}