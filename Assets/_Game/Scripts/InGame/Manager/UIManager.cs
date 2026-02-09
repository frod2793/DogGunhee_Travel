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
        #region 설정 데이터

        [Header("하위 View 및 팝업")] [SerializeField]
        private InGameHUDView m_hudView;

        [SerializeField] private InGameSkillView m_skillView;
        [SerializeField] private GameOverPopup m_gameOverPopup;
        [SerializeField] private GameObject m_menuPanel;

        [Header("메뉴 및 설정")] [SerializeField] private Button m_menuButton;
        [SerializeField] private Button m_settingButton;
        [SerializeField] private Button m_exitButton;
        [SerializeField] private SettingsData m_settingsData;

        [Header("조작계")] [SerializeField] private VariableJoystick m_variableJoystick;
        [SerializeField] private RectTransform m_joystickTransform;
        [SerializeField] private Toggle m_autoAttackToggle;

        [Header("데이터")] [SerializeField] private SkillDatabase m_skillDatabase;
        [SerializeField] private TMP_Text m_mobWaveText; // 카운트다운용으로 유지

        #endregion

        #region 내부 상태 및 캐시

        private InGameViewModel m_viewModel;

        private GameManager m_gameManager;
        private PlayerController m_playerController;
        private CancellationTokenSource m_skillSelectionTimerCts;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        private readonly List<SkillData> m_skillChoices = new List<SkillData>(3);

        private int m_pendingSkillSelections = 0;
        private bool m_isSkillSelectionActive = false;

        private static readonly Vector2 k_DefaultJoystickPosition = new Vector2(300, 300);

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_gameManager = GameManager.Instance;
            // [Refactoring] ViewModel에 의존성 주입
            m_viewModel = new InGameViewModel(m_skillDatabase);

            InitializeViews();
            BindUIEvents();
            BindViewModel();
        }

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
        }

        #endregion

        #region 초기화

        private void SubscribeToEvents()
        {
            if (GameManager.Instance.State == null)
            {
                return;
            }

            GameManager.Instance.State.OnGameStart += OnGameStart;
            GameManager.Instance.State.OnGamePause += OnGamePause;
            GameManager.Instance.State.OnGameResume += OnGameResume;
            GameManager.Instance.State.OnGameOver += OnGameOver;
            PlayerBase.OnExpChanged += OnPlayerExpChanged;
            PlayerBase.OnLevelUp += OnPlayerLevelUp;
            SettingsData.OnSettingsChanged += ApplyJoystickSettings;
            GameManager.OnPlayerChanged += OnPlayerChanged;
        }

        private void UnsubscribeFromEvents()
        {
            if (GameManager.Instance.State == null)
            {
                return;
            }

            GameManager.Instance.State.OnGameStart -= OnGameStart;
            GameManager.Instance.State.OnGamePause -= OnGamePause;
            GameManager.Instance.State.OnGameResume -= OnGameResume;
            GameManager.Instance.State.OnGameOver -= OnGameOver;
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

        private void BindViewModel()
        {
            // 스킬 선택 활성화 상태 동기화
            m_viewModel.IsSkillSelectionActive
                .Subscribe(isActive =>
                {
                    m_gameManager.SetMenuPopupState(isActive); // 게임 일시정지 연동
                    if (m_skillView != null)
                    {
                        m_skillView.Show(isActive);
                    }
                })
                .AddTo(m_disposables);

            // 스킬 목록 갱신
            m_viewModel.SkillChoices
                .Subscribe(choices =>
                {
                    if (m_skillView != null)
                        m_skillView.RefreshSkillChoices(choices.ToList(), skill => OnSkillSelected(skill).Forget());
                })
                .AddTo(m_disposables);

            // 타이머 갱신
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

            // 자동 선택 이벤트
            m_viewModel.OnAutoSelectSkill
                .Subscribe(async skill =>
                {
                    if (m_skillView != null)
                    {
                        // 애니메이션 재생 완료 대기
                        await m_skillView.PlaySelectionAnimation(skill);
                    }

                    // 애니메이션 이후 실제 선택 로직 실행
                    OnSkillSelected(skill).Forget();
                })
                .AddTo(m_disposables);
        }

        private void InitializeUI()
        {
            // ViewModel 데이터 갱신 유도
            m_viewModel.UpdateIconLists();
        }

        #endregion

        #region 이벤트 핸들러

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
                m_gameOverPopup.Show(m_viewModel.CoinCount.CurrentValue, m_viewModel.CurrentWave.CurrentValue,
                    m_viewModel.KillCount.CurrentValue);
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
                GameManager.Instance.State.StartGame();
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

                GameManager.Instance.State.StartGame();
            }
            catch (Exception ex)
            {
                LogManager.LogError($"카운트다운 오류: {ex.Message}", LogManager.LogCategory.VamserLikeUI);
                if (m_joystickTransform != null) m_joystickTransform.gameObject.SetActive(true); // 오류 시에도 활성화 보장
                GameManager.Instance.State.StartGame();
            }
        }

        #endregion

        // UpdateUILoopAsync는 ViewModel이 내부적으로 R3 Interval로 처리하므로 제거 가능

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
            if (m_variableJoystick == null || m_settingsData == null)
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
            var canvas = m_joystickTransform.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return false;
            }

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
                // ViewModel에 선택 시작 요청
                m_viewModel.StartSkillSelection();
                // IsSkillSelectionActive 구독에서 SetMenuPopupState(true)가 호출됨
            }
        }

        private async UniTask OnSkillSelected(SkillData selectedSkill)
        {
            // ViewModel의 타이머 중지 요청
            m_viewModel.EndSkillSelection();

            if (selectedSkill.skillType == SkillType.Weapon)
            {
                var ownedWeapon =
                    m_gameManager.SpawnedPlayer?.Weapons.FirstOrDefault(w => w.SkillCode == selectedSkill.skillCode);
                if (ownedWeapon != null)
                {
                    // 이미 보유한 무기 -> 레벨업
                    ownedWeapon.LevelUp();
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
                ProcessSkillSelectionQueue();
            }
            else
            {
                CloseSkillSelection();
            }
        }

        private void TryUpgradeWeapon(string passiveItemCode)
        {
            if (m_gameManager.SpawnedPlayer == null)
            {
                return;
            }

            var weaponToUpgrade =
                m_gameManager.SpawnedPlayer.Weapons.FirstOrDefault(w =>
                    w.SkillData?.upgradeItemCode == passiveItemCode);
            if (weaponToUpgrade != null)
            {
                weaponToUpgrade.LevelUp();
            }
        }

        private void CloseSkillSelection()
        {
            // 모든 선택 종료
            m_viewModel.EndSkillSelection();
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
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()
                .name);
        }

        #endregion
    }
}