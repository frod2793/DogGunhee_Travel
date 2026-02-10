using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using InGame.Manager;
using InGame.Player.Player_Base;
using InGame.Lobby;

namespace InGame.UI.ViewModels
{
    /// <summary>
    /// 인게임 UI 전반의 상태(HUD, 팝업, 웨이브 정보)를 관리하는 ViewModel입니다.
    /// <br/> GameManager(데이터 원본)와 View(UI 표현) 사이의 중개자 역할을 수행합니다.
    /// </summary>
    public class InGameViewModel : IDisposable
    {
        #region 1. 프로퍼티 및 상태 (Properties & Fields)

        // 1. 게임 진행 정보
        public ReadOnlyReactiveProperty<int> CurrentWave => m_currentWave;
        public ReadOnlyReactiveProperty<int> CurrentStageId => m_currentStageId;
        public ReadOnlyReactiveProperty<int> CoinCount => m_coinCount;
        public ReadOnlyReactiveProperty<int> KillCount => m_killCount;

        // 2. 플레이어 상태
        public ReadOnlyReactiveProperty<int> PlayerLevel => m_playerLevel;
        public ReadOnlyReactiveProperty<float> ExpProgress => m_expProgress;
        public ReadOnlyReactiveProperty<IReadOnlyList<Sprite>> WeaponSprites => m_weaponSprites;
        public ReadOnlyReactiveProperty<IReadOnlyList<Sprite>> AccessorySprites => m_accessorySprites;

        // 3. 이벤트 스트림
        public Observable<int> OnLevelUp => m_onLevelUpSubject;
        public Observable<WaveData> OnWaveStarted => m_onWaveStartedSubject;
        public Observable<WaveData> OnWaveCompleted => m_onWaveCompletedSubject;

        // 4. 스킬 선택 관련
        public ReadOnlyReactiveProperty<IReadOnlyList<SkillData>> SkillChoices => m_skillChoices;
        public ReadOnlyReactiveProperty<float> SelectionTimer => m_selectionTimer;
        public ReadOnlyReactiveProperty<bool> IsSkillSelectionActive => m_isSkillSelectionActive;
        public Observable<SkillData> OnAutoSelectSkill => m_onAutoSelectSkillSubject;

        // 5. 하위 ViewModel
        public ConfirmPopupViewModel ConfirmPopupViewModel { get; } = new ConfirmPopupViewModel();


        // --- Private Mutable Fields (내부 상태 관리용) ---

        // 게임 데이터
        private readonly ReactiveProperty<int> m_currentWave = new(0);
        private readonly ReactiveProperty<int> m_currentStageId = new(0);
        private readonly ReactiveProperty<int> m_coinCount = new(0);
        private readonly ReactiveProperty<int> m_killCount = new(0);
        private readonly ReactiveProperty<int> m_playerLevel = new(1);
        private readonly ReactiveProperty<float> m_expProgress = new(0f);

        // 인벤토리 아이콘
        private readonly ReactiveProperty<IReadOnlyList<Sprite>> m_weaponSprites = new(new List<Sprite>());
        private readonly ReactiveProperty<IReadOnlyList<Sprite>> m_accessorySprites = new(new List<Sprite>());

        // 스킬 선택 상태
        private readonly ReactiveProperty<IReadOnlyList<SkillData>> m_skillChoices = new(new List<SkillData>());
        private readonly ReactiveProperty<float> m_selectionTimer = new(0f);
        private readonly ReactiveProperty<bool> m_isSkillSelectionActive = new(false);

        // 이벤트 서브젝트
        private readonly Subject<int> m_onLevelUpSubject = new();
        private readonly Subject<SkillData> m_onAutoSelectSkillSubject = new();
        private readonly Subject<WaveData> m_onWaveStartedSubject = new();
        private readonly Subject<WaveData> m_onWaveCompletedSubject = new();

        // 시스템 및 리소스
        private readonly SkillDatabase m_skillDatabase;
        private readonly CompositeDisposable m_disposables = new();
        private CancellationTokenSource m_timerCts;
        private bool m_isWaveSubscribed;

        #endregion

        #region 2. 초기화 및 생명주기 (Init & Lifecycle)

        public InGameViewModel(SkillDatabase skillDatabase)
        {
            m_skillDatabase = skillDatabase;

            InitializeSubscriptions();
            StartUpdateLoop();
        }

        private void InitializeSubscriptions()
        {
            // 1. 플레이어 변경 감지 (재시작, 캐릭터 교체 등)
            GameManager.OnPlayerChanged += HandlePlayerChanged;

            // 2. 초기 데이터 로드
            RefreshAllData();

            // 3. 웨이브 이벤트 구독 시도
            SubscribeWaveEvents();
        }

        private void StartUpdateLoop()
        {
            // GameManager의 데이터(코인, 킬 수 등)는 ReactiveProperty가 아니므로 
            // 일정 간격(약 50fps)으로 폴링하여 ViewModel 상태를 동기화합니다.
            Observable.Interval(TimeSpan.FromSeconds(0.02))
                .Subscribe(_ => PollGameData())
                .AddTo(m_disposables);
        }

        public void Dispose()
        {
            // 1. C# 네이티브 이벤트 해제
            GameManager.OnPlayerChanged -= HandlePlayerChanged;
            PlayerBase.OnExpChanged -= HandleExpChanged;
            PlayerBase.OnLevelUp -= HandleLevelUp;
            UnsubscribeWaveEvents();

            // 2. 비동기 작업 취소
            m_timerCts?.Cancel();
            m_timerCts?.Dispose();

            // 3. R3 리소스 해제
            m_disposables.Dispose();
            
            // 4. ReactiveProperty 해제
            m_currentWave.Dispose();
            m_currentStageId.Dispose();
            m_coinCount.Dispose();
            m_killCount.Dispose();
            m_playerLevel.Dispose();
            m_expProgress.Dispose();
            m_weaponSprites.Dispose();
            m_accessorySprites.Dispose();
            
            m_skillChoices.Dispose();
            m_selectionTimer.Dispose();
            m_isSkillSelectionActive.Dispose();
            
            // 5. Subject 해제
            m_onLevelUpSubject.Dispose();
            m_onAutoSelectSkillSubject.Dispose();
            m_onWaveStartedSubject.Dispose();
            m_onWaveCompletedSubject.Dispose();

            // 6. 하위 VM 정리
            ConfirmPopupViewModel.Dispose();
        }

        #endregion

        #region 3. 게임 데이터 동기화 (Polling & Events)

        /// <summary>
        /// GameManager에서 최신 게임 상태를 가져와 ReactiveProperty에 반영합니다.
        /// </summary>
        private void PollGameData()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // 값 변경 시에만 알림이 가도록 ReactiveProperty가 내부적으로 처리함
            m_currentWave.Value = gm.GetCurrentWave();
            m_currentStageId.Value = gm.GetCurrentStageId();
            m_coinCount.Value = gm.GetCoinCount();
            m_killCount.Value = gm.GetMobKillCount();

            // 웨이브 시스템이 늦게 초기화될 경우를 대비한 지연 구독
            if (!m_isWaveSubscribed)
            {
                SubscribeWaveEvents();
            }
        }

        private void RefreshAllData()
        {
            PollGameData();
            UpdateIconLists();
            
            var gm = GameManager.Instance;
            if (gm != null)
            {
                m_playerLevel.Value = (int)gm.GetPlayerLevel();
                m_expProgress.Value = gm.GetPlayerExpProgress();
            }
        }

        public void UpdateIconLists()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.SpawnedPlayer == null) return;

            // 1. 무기 아이콘 갱신
            var weapons = new List<Sprite>();
            foreach (var w in gm.SpawnedPlayer.Weapons)
            {
                if (w != null && w.Thumbnail != null)
                {
                    weapons.Add(w.Thumbnail);
                }
            }
            m_weaponSprites.Value = weapons;

            // 2. 장신구(패시브) 아이콘 갱신
            if (InventoryDataManager.Instance != null)
            {
                var accessories = new List<Sprite>();
                foreach (var skill in InventoryDataManager.Instance.InGameAcquiredSkills)
                {
                    if (skill.skillType == SkillType.Passive && skill.skillIcon != null)
                    {
                        accessories.Add(skill.skillIcon);
                    }
                }
                m_accessorySprites.Value = accessories;
            }
        }

        #endregion

        #region 4. 이벤트 핸들러 (Event Handlers)

        private void SubscribeWaveEvents()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.ObjectPoolSpawner != null)
            {
                gm.ObjectPoolSpawner.OnWaveStarted += HandleWaveStartedEvent;
                gm.ObjectPoolSpawner.OnWaveCompleted += HandleWaveCompletedEvent;
                m_isWaveSubscribed = true;
            }
        }

        private void UnsubscribeWaveEvents()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.ObjectPoolSpawner != null)
            {
                gm.ObjectPoolSpawner.OnWaveStarted -= HandleWaveStartedEvent;
                gm.ObjectPoolSpawner.OnWaveCompleted -= HandleWaveCompletedEvent;
            }
            m_isWaveSubscribed = false;
        }

        private void HandleWaveStartedEvent(WaveData wave) => m_onWaveStartedSubject.OnNext(wave);
        private void HandleWaveCompletedEvent(WaveData wave) => m_onWaveCompletedSubject.OnNext(wave);

        // --- 플레이어 이벤트 ---
        private void HandlePlayerChanged(PlayerBase player)
        {
            if (player == null) return;

            // 기존 이벤트 핸들러 제거 (중복 구독 방지)
            PlayerBase.OnExpChanged -= HandleExpChanged;
            PlayerBase.OnLevelUp -= HandleLevelUp;

            // 새 핸들러 등록
            PlayerBase.OnExpChanged += HandleExpChanged;
            PlayerBase.OnLevelUp += HandleLevelUp;
            
            // 아이콘 등 데이터 즉시 갱신
            RefreshAllData();
        }

        private void HandleExpChanged(float currentExp, float maxExp)
        {
            m_expProgress.Value = (maxExp > 0) ? currentExp / maxExp : 0f;
        }

        private void HandleLevelUp(float level)
        {
            int intLevel = (int)level;
            m_playerLevel.Value = intLevel;
            m_onLevelUpSubject.OnNext(intLevel);
        }

        #endregion

        #region 5. 스킬 선택 로직 (Skill Selection)

        public void StartSkillSelection()
        {
            if (m_skillDatabase == null) return;

            GenerateSkillChoices();
            m_isSkillSelectionActive.Value = true;

            // 6초 카운트다운 시작
            StartSelectionTimer(6.0f).Forget();
        }

        public void EndSkillSelection()
        {
            m_timerCts?.Cancel();
            m_isSkillSelectionActive.Value = false;
        }

        /// <summary>
        /// 현재 표시된 스킬 목록을 무작위로 다시 생성합니다. (리롤)
        /// </summary>
        public void RefreshSkillChoices()
        {
            if (!m_isSkillSelectionActive.Value) return;
            GenerateSkillChoices();
        }

        private void GenerateSkillChoices()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

         
            Dictionary<string, Weapon.Base.IWeaponController> ownedWeapons;
    
            if (gm.SpawnedPlayer != null && gm.SpawnedPlayer.Weapons != null)
            {
                ownedWeapons = gm.SpawnedPlayer.Weapons.ToDictionary(w => w.SkillCode);
            }
            else
            {
                ownedWeapons = new Dictionary<string, Weapon.Base.IWeaponController>();
            }
            
            var acquiredAccessoryCodes = new HashSet<string>();
            if (InventoryDataManager.Instance != null)
            {
                foreach (var s in InventoryDataManager.Instance.InGameAcquiredSkills)
                {
                    if (s.skillType == SkillType.Passive)
                    {
                        acquiredAccessoryCodes.Add(s.skillCode);
                    }
                }
            }

            // 2. 등장 가능한 스킬 필터링
            var availableSkills = new List<SkillData>();
            foreach (var skill in m_skillDatabase.allSkills)
            {
                if (skill.skillType == SkillType.Weapon)
                {
                    // 무기: 미보유 or (보유 중 & 레벨업 가능 & 미진화 상태)
                    if (ownedWeapons.TryGetValue(skill.skillCode, out var weapon))
                    {
                        if (weapon.CurrentLevel < weapon.MaxLevel ||
                            (weapon.CurrentLevel == weapon.MaxLevel && !weapon.IsEvolved))
                        {
                            availableSkills.Add(skill);
                        }
                    }
                    else
                    {
                        availableSkills.Add(skill);
                    }
                }
                else // Passive
                {
                    // 패시브: 미보유 상태일 때만 등장 (중복 획득 불가 가정)
                    // TODO: 패시브 레벨업 기획이 있다면 로직 수정 필요
                    if (!acquiredAccessoryCodes.Contains(skill.skillCode))
                    {
                        availableSkills.Add(skill);
                    }
                }
            }

            // 3. 랜덤 선택 (최대 3개)
            var choices = new List<SkillData>();
            int selectCount = Mathf.Min(3, availableSkills.Count);

            // 안전장치: 선택 가능한 스킬이 없으면 빈 리스트 반환
            if (selectCount == 0)
            {
                m_skillChoices.Value = choices;
                return;
            }

            while (choices.Count < selectCount)
            {
                var skill = availableSkills[UnityEngine.Random.Range(0, availableSkills.Count)];
                if (!choices.Contains(skill))
                {
                    choices.Add(skill);
                }
            }

            m_skillChoices.Value = choices;
        }

        private async UniTaskVoid StartSelectionTimer(float duration)
        {
            // 기존 타이머 취소
            m_timerCts?.Cancel();
            m_timerCts = new CancellationTokenSource();
            var token = m_timerCts.Token;

            float timer = duration;
            m_selectionTimer.Value = timer;

            try
            {
                while (timer > 0f)
                {
                    // TimeScale이 0이어도(일시정지) 타이머가 흘러가야 한다면 ignoreTimeScale: true
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    
                    // UI 표시용 타이머 갱신 (unscaledDeltaTime 사용)
                    timer -= Time.unscaledDeltaTime;
                    m_selectionTimer.Value = Mathf.Max(0f, timer);
                }

                // 시간 종료 시 자동 선택 처리
                if (!token.IsCancellationRequested && m_skillChoices.Value.Count > 0)
                {
                    var randomSkill = m_skillChoices.Value[UnityEngine.Random.Range(0, m_skillChoices.Value.Count)];
                    m_onAutoSelectSkillSubject.OnNext(randomSkill);
                }
            }
            catch (OperationCanceledException)
            {
                // 타이머 취소됨 (스킬 선택 완료 등)
            }
        }

        #endregion
    }
}