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
using InGame.Weapon.Base;

namespace InGame.UI.ViewModels
{
    // Recompile trigger
    /// <summary>
    /// 인게임 UI 상태를 관리하는 ViewModel입니다.
    /// GameManager와 PlayerBase로부터 데이터를 수집하여 View에 전달합니다.
    /// </summary>
    public class InGameViewModel : IDisposable
    {
        #region 프로퍼티

        // 상단 정보 데이터
        public ReadOnlyReactiveProperty<int> CurrentWave => m_currentWave;
        public ReadOnlyReactiveProperty<int> CoinCount => m_coinCount;
        public ReadOnlyReactiveProperty<int> KillCount => m_killCount;

        // 플레이어 상태 데이터
        public ReadOnlyReactiveProperty<int> PlayerLevel => m_playerLevel;
        public ReadOnlyReactiveProperty<float> ExpProgress => m_expProgress;
        public ReadOnlyReactiveProperty<IReadOnlyList<Sprite>> WeaponSprites => m_weaponSprites;
        public ReadOnlyReactiveProperty<IReadOnlyList<Sprite>> AccessorySprites => m_accessorySprites;
        
        // 이벤트
        public Observable<int> OnLevelUp => m_onLevelUpSubject;

        // 스킬 선택 관련 상태
        public ReadOnlyReactiveProperty<IReadOnlyList<SkillData>> SkillChoices => m_skillChoices;
        public ReadOnlyReactiveProperty<float> SelectionTimer => m_selectionTimer;
        public ReadOnlyReactiveProperty<bool> IsSkillSelectionActive => m_isSkillSelectionActive;
        public Observable<SkillData> OnAutoSelectSkill => m_onAutoSelectSkillSubject;

        #endregion

        #region 내부 상태 및 캐시

        private readonly ReactiveProperty<int> m_currentWave = new ReactiveProperty<int>(0);
        private readonly ReactiveProperty<int> m_coinCount = new ReactiveProperty<int>(0);
        private readonly ReactiveProperty<int> m_killCount = new ReactiveProperty<int>(0);
        private readonly ReactiveProperty<int> m_playerLevel = new ReactiveProperty<int>(1);
        private readonly ReactiveProperty<float> m_expProgress = new ReactiveProperty<float>(0f);
        private readonly ReactiveProperty<IReadOnlyList<Sprite>> m_weaponSprites = new ReactiveProperty<IReadOnlyList<Sprite>>(new List<Sprite>());
        private readonly ReactiveProperty<IReadOnlyList<Sprite>> m_accessorySprites = new ReactiveProperty<IReadOnlyList<Sprite>>(new List<Sprite>());
        private readonly Subject<int> m_onLevelUpSubject = new Subject<int>();

        // 스킬 선택 관련
        private readonly ReactiveProperty<IReadOnlyList<SkillData>> m_skillChoices = new ReactiveProperty<IReadOnlyList<SkillData>>(new List<SkillData>());
        private readonly ReactiveProperty<float> m_selectionTimer = new ReactiveProperty<float>(0f);
        private readonly ReactiveProperty<bool> m_isSkillSelectionActive = new ReactiveProperty<bool>(false);
        private readonly Subject<SkillData> m_onAutoSelectSkillSubject = new Subject<SkillData>();
        
        private readonly SkillDatabase m_skillDatabase;
        private CancellationTokenSource m_timerCts;

        private readonly CompositeDisposable m_disposables = new CompositeDisposable();
        private IDisposable m_updateLoop;

        #endregion

        #region 초기화

        public InGameViewModel(SkillDatabase skillDatabase)
        {
            m_skillDatabase = skillDatabase;
            InitializeSubscriptions();
            StartUpdateLoop();
        }

        private void InitializeSubscriptions()
        {
            // 플레이어 변경 시 초기화 및 재바인딩
            GameManager.OnPlayerChanged += HandlePlayerChanged;
            
            // 전역 이벤트 구독 (PlayStateManager 등)
            // 인게임에 진입한 시점의 초기 데이터 설정
            RefreshAllData();
        }

        private void StartUpdateLoop()
        {
            // GameManager에서 폴링 방식으로 가져오는 데이터들을 위해 루프 실행
            // R3의 Interval을 사용하여 FixedUpdate 타이밍에 갱신
            m_updateLoop = Observable.Interval(TimeSpan.FromSeconds(0.02)) // 약 50fps
                .Subscribe(_ => PollGameData())
                .AddTo(m_disposables);
        }

        #endregion

        #region 이벤트 핸들러

        private void PollGameData()
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                return;
            }

            m_currentWave.Value = gm.GetCurrentWave();
            m_coinCount.Value = gm.GetCoinCount();
            m_killCount.Value = gm.GetMobKillCount();
        }

        private void HandlePlayerChanged(PlayerBase player)
        {
            if (player == null)
            {
                return;
            }

            // PlayerBase의 기존 C# 이벤트를 R3 Observable로 변환하여 구독
            // Note: 기존 PlayerBase.OnExpChanged와 OnLevelUp은 static Action 이벤트임
            
            // 기존 static 이벤트는 직접 핸들러 등록
            PlayerBase.OnExpChanged += HandleExpChanged;
            PlayerBase.OnLevelUp += HandleLevelUp;
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

        #region 유틸리티

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
            if (gm == null || gm.SpawnedPlayer == null)
            {
                return;
            }

            // 무기 아이콘 추출
            var weapons = new List<Sprite>();
            foreach (var w in gm.SpawnedPlayer.Weapons)
            {
                if (w != null && w.Thumbnail != null)
                {
                    weapons.Add(w.Thumbnail);
                }
            }
            m_weaponSprites.Value = weapons;

            // 장신구(패시브) 아이콘 추출
            // UIManager에서 관리하던 m_acquiredAccessorySkills를 ViewModel에서 관리하도록 하거나,
            // InventoryDataManager 등을 참조해야 함.
            // 일단 InventoryDataManager를 통해 현재 인게임 스킬 리스트를 가져옴.
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



        #region 스킬 선택 로직

        public void StartSkillSelection()
        {
            if (m_skillDatabase == null)
            {
                return;
            }

            GenerateSkillChoices();
            m_isSkillSelectionActive.Value = true;
            
            // 타이머 시작
            StartSelectionTimer(6.0f).Forget();
        }

        public void EndSkillSelection()
        {
            m_timerCts?.Cancel();
            m_isSkillSelectionActive.Value = false;
        }

        private void GenerateSkillChoices()
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                return;
            }

            var choices = new List<SkillData>();
            var ownedWeapons = gm.SpawnedPlayer?.Weapons.ToDictionary(w => w.SkillCode) ??
                               new Dictionary<string, InGame.Weapon.Base.IWeaponController>();
            
            var acquiredAccessoryCodes = new HashSet<string>();
            if (InventoryDataManager.Instance != null)
            {
                foreach(var s in InventoryDataManager.Instance.InGameAcquiredSkills)
                {
                    if (s.skillType == SkillType.Passive)
                    {
                        acquiredAccessoryCodes.Add(s.skillCode);
                    }
                }
            }

            var availableSkills = new List<SkillData>();
            foreach(var skill in m_skillDatabase.allSkills)
            {
                if (skill.skillType == SkillType.Weapon)
                {
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
                    if(!acquiredAccessoryCodes.Contains(skill.skillCode))
                    {
                        availableSkills.Add(skill);
                    }
                }
            }

            int count = Mathf.Min(3, availableSkills.Count);
            while (choices.Count < count)
            {
                var skill = availableSkills[UnityEngine.Random.Range(0, availableSkills.Count)];
                if (!choices.Contains(skill))
                {
                    choices.Add(skill);
                }
            }

            m_skillChoices.Value = choices;
        }

        /// <summary>
        /// 현재 표시된 스킬 목록을 무작위로 다시 생성합니다.
        /// </summary>
        public void RefreshSkillChoices()
        {
            if (!m_isSkillSelectionActive.Value)
            {
                return;
            }

            GenerateSkillChoices();
            // 새로고침 시 타이머를 초기화할 수도 있지만, 일단 목록만 갱신합니다.
        }

        private async UniTaskVoid StartSelectionTimer(float duration)
        {
            m_timerCts?.Cancel();
            m_timerCts = new CancellationTokenSource();
            var token = m_timerCts.Token;

            float timer = duration;
            m_selectionTimer.Value = timer;

            try
            {
                while (timer > 0f)
                {
                    // 일시정지(TimeScale=0) 상태에서도 타이머가 흐르도록 ignoreTimeScale: true 설정
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    timer -= Time.unscaledDeltaTime; 
                    m_selectionTimer.Value = Mathf.Max(0f, timer);
                }

                if (!token.IsCancellationRequested && m_skillChoices.Value.Count > 0)
                {
                    // 시간 종료 시 랜덤 선택
                    var randomSkill = m_skillChoices.Value[UnityEngine.Random.Range(0, m_skillChoices.Value.Count)];
                    m_onAutoSelectSkillSubject.OnNext(randomSkill);
                }
            }
            catch (OperationCanceledException)
            {
                // 타이머 취소됨
            }
        }

        #endregion

        #region 소멸자 및 정리

        public void Dispose()
        {
            GameManager.OnPlayerChanged -= HandlePlayerChanged;
            PlayerBase.OnExpChanged -= HandleExpChanged;
            PlayerBase.OnLevelUp -= HandleLevelUp;
            
            m_timerCts?.Cancel();
            m_timerCts?.Dispose();
            m_updateLoop?.Dispose();
            m_disposables.Dispose();
            m_onLevelUpSubject.Dispose();
            
            m_skillChoices.Dispose();
            m_selectionTimer.Dispose();
            m_isSkillSelectionActive.Dispose();
            m_onAutoSelectSkillSubject.Dispose();
        }

        #endregion
    }
}
