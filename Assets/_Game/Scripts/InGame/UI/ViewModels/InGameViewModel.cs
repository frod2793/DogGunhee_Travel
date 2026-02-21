using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using InGame.Managers;
using InGame.Player.Player_Base;
using InGame.Lobby;
using InGame.Core.Interfaces;

namespace InGame.UI.ViewModels
{
    /// <summary>
    /// [설명]: 인게임 UI 전반(HUD, 경험치, 코인, 웨이브 정보, 스킬 선택 등)의 상태와 비즈니스 로직을 총괄하는 핵심 ViewModel입니다.
    /// GameManager를 비롯한 하위 도메인 시스템으로부터 데이터를 공급받아 가공한 뒤, ReadOnlyReactiveProperty를 통해 View에 최신 상태를 투영합니다.
    /// </summary>
    public class InGameViewModel : IDisposable
    {
        #region 공개 프로퍼티 (View 바인딩용)

        #region 게임 진행 정보

        /// <summary> [설명]: 현재 진행 중인 웨이브 번호 (읽기 전용) </summary>
        public ReadOnlyReactiveProperty<int> CurrentWave => m_currentWave;

        /// <summary> [설명]: 현재 스테이지의 고유 ID (읽기 전용) </summary>
        public ReadOnlyReactiveProperty<int> CurrentStageId => m_currentStageId;

        /// <summary> [설명]: 이번 플레이 세션에서 획득한 실시간 코인 수 (읽기 전용) </summary>
        public ReadOnlyReactiveProperty<int> CoinCount => m_coinCount;

        /// <summary> [설명]: 이번 플레이 세션에서 처치한 실시간 몬스터 수 (읽기 전용) </summary>
        public ReadOnlyReactiveProperty<int> KillCount => m_killCount;

        #endregion

        #region 플레이어 상태

        /// <summary> [설명]: 플레이어의 현재 레벨 (읽기 전용) </summary>
        public ReadOnlyReactiveProperty<int> PlayerLevel => m_playerLevel;

        /// <summary> [설명]: 현재 레벨 내의 경험치 충전 비율 (0~1, 읽기 전용) </summary>
        public ReadOnlyReactiveProperty<float> ExpProgress => m_expProgress;

        /// <summary> [설명]: 현재 장착 중인 무기들의 아이콘 목록 (읽기 전용) </summary>
        public ReadOnlyReactiveProperty<IReadOnlyList<Sprite>> WeaponSprites => m_weaponSprites;

        /// <summary> [설명]: 현재 장착 중인 장신구들의 아이콘 목록 (읽기 전용) </summary>
        public ReadOnlyReactiveProperty<IReadOnlyList<Sprite>> AccessorySprites => m_accessorySprites;

        #endregion

        #region 이벤트 스트림

        /// <summary> [설명]: 플레이어 레벨업 발생 시 수치를 전달하는 이벤트 스트림 </summary>
        public Observable<int> OnLevelUp => m_onLevelUpSubject;

        /// <summary> [설명]: 새로운 웨이브가 물리적으로 시작되었을 때 발생하는 이벤트 스트림 </summary>
        public Observable<WaveData> OnWaveStarted => m_onWaveStartedSubject;

        /// <summary> [설명]: 현재 웨이브의 모든 조건을 달성하고 완료되었을 때 발생하는 이벤트 스트림 </summary>
        public Observable<WaveData> OnWaveCompleted => m_onWaveCompletedSubject;

        #endregion

        #region 스킬 선택 인터페이스

        /// <summary> [설명]: 레벨업 시 무작위로 추첨된 선택 가능한 스킬 리스트 (읽기 전용) </summary>
        public ReadOnlyReactiveProperty<IReadOnlyList<SkillData>> SkillChoices => m_skillChoices;

        /// <summary> [설명]: 스킬 선택 팝업의 남은 제한 시간 (초 단위, 읽기 전용) </summary>
        public ReadOnlyReactiveProperty<float> SelectionTimer => m_selectionTimer;

        /// <summary> [설명]: 현재 스킬 선택 화면이 활성화되어야 하는지 여부 (읽기 전용) </summary>
        public ReadOnlyReactiveProperty<bool> IsSkillSelectionActive => m_isSkillSelectionActive;

        /// <summary> [설명]: 제한 시간 종료 등으로 인해 시스템이 스킬을 자동 선택했을 때 발생하는 이벤트 스트림 </summary>
        public Observable<SkillData> OnAutoSelectSkill => m_onAutoSelectSkillSubject;

        #endregion

        #region 하위 뷰모델 참조

        /// <summary> [설명]: 확인 팝업 제어를 위한 전용 뷰모델 인스턴스 </summary>
        public ConfirmPopupViewModel ConfirmPopupViewModel { get; } = new ConfirmPopupViewModel();

        #endregion

        #endregion

        #region 내부 상태 필드

        // 리액티브 상태 변수
        private readonly ReactiveProperty<int> m_currentWave = new(0);
        private readonly ReactiveProperty<int> m_currentStageId = new(0);
        private readonly ReactiveProperty<int> m_coinCount = new(0);
        private readonly ReactiveProperty<int> m_killCount = new(0);
        private readonly ReactiveProperty<int> m_playerLevel = new(1);
        private readonly ReactiveProperty<float> m_expProgress = new(0f);
        private readonly ReactiveProperty<IReadOnlyList<Sprite>> m_weaponSprites = new(new List<Sprite>());
        private readonly ReactiveProperty<IReadOnlyList<Sprite>> m_accessorySprites = new(new List<Sprite>());
        private readonly ReactiveProperty<IReadOnlyList<SkillData>> m_skillChoices = new(new List<SkillData>());
        private readonly ReactiveProperty<float> m_selectionTimer = new(0f);
        private readonly ReactiveProperty<bool> m_isSkillSelectionActive = new(false);

        // 이벤트 서브젝트
        private readonly Subject<int> m_onLevelUpSubject = new();
        private readonly Subject<SkillData> m_onAutoSelectSkillSubject = new();
        private readonly Subject<WaveData> m_onWaveStartedSubject = new();
        private readonly Subject<WaveData> m_onWaveCompletedSubject = new();

        // 관리 및 데이터 리소스
        private readonly SkillDatabase m_skillDatabase;
        private readonly CompositeDisposable m_disposables = new();
        private CancellationTokenSource m_timerCts;
        private bool m_isWaveSubscribed;

        // 주입된 의존성
        private readonly IGameStateService m_gameState;
        private readonly IPlayerContext m_playerCtx;
        private readonly ICombatContext m_combatCtx;
        private readonly IGameDataProvider m_dataProvider;
        private readonly IInventoryContext m_inventoryCtx;

        #endregion

        #region 초기화 및 생명주기

        /// <summary>
        /// [설명]: 외부 데이터베이스를 주입받아 초기 구독 환경을 구성하고 업데이트 루프를 가동합니다.
        /// </summary>
        public InGameViewModel(
            SkillDatabase skillDatabase,
            IGameStateService gameState,
            IPlayerContext playerContext,
            ICombatContext combatContext,
            IGameDataProvider dataProvider,
            IInventoryContext inventoryContext)
        {
            m_skillDatabase = skillDatabase;
            m_gameState = gameState;
            m_playerCtx = playerContext;
            m_combatCtx = combatContext;
            m_dataProvider = dataProvider;
            m_inventoryCtx = inventoryContext;

            InitializeSubscriptions();
            StartUpdateLoop();
        }

        /// <summary>
        /// [설명]: 시스템 외부 이벤트(GameManager 등)와의 연결 고리를 설정하고 초기 데이터를 로드합니다.
        /// </summary>
        private void InitializeSubscriptions()
        {
            // 플레이어 실시간 교체 대응
            if (m_playerCtx != null)
            {
                m_playerCtx.OnPlayerChanged += HandlePlayerChanged;
            }

            // 현시점 기준 모든 데이터 즉시 동기화
            RefreshAllData();

            // 웨이브 매니저 이벤트 기동 시도
            SubscribeWaveEvents();
        }

        /// <summary>
        /// [설명]: 리액티브 속성이 아닌 일반 멤버 변수값들의 변화를 감지하기 위해 폴링 루프를 시작합니다.
        /// </summary>
        private void StartUpdateLoop()
        {
            // 주기적으로 GameManager 데이터를 확인하여 ViewModel에 반영
            Observable.Interval(TimeSpan.FromSeconds(0.02))
                .Subscribe(_ => PollGameData())
                .AddTo(m_disposables);
        }

        /// <summary>
        /// [설명]: ViewModel 소멸 시 모든 이벤트 핸들러 해제, 비동기 작업 중단, 리액티브 자원 반납을 일괄 처리합니다.
        /// </summary>
        public void Dispose()
        {
            // 1. 순수 C# 이벤트 정적 해제
            if (m_playerCtx != null)
            {
                m_playerCtx.OnPlayerChanged -= HandlePlayerChanged;
            }
            PlayerBase.OnExpChanged -= HandleExpChanged;
            PlayerBase.OnLevelUp -= HandleLevelUp;
            UnsubscribeWaveEvents();

            // 2. 비동기 타이머 로직 강제 중단
            if (m_timerCts != null)
            {
                m_timerCts.Cancel();
                m_timerCts.Dispose();
            }

            // 3. R3 구독 목록 파기
            m_disposables.Dispose();

            // 4. 모든 상태 프로퍼티 메모리 해제
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

            // 5. 모든 이벤트 스트림 파기
            m_onLevelUpSubject.Dispose();
            m_onAutoSelectSkillSubject.Dispose();
            m_onWaveStartedSubject.Dispose();
            m_onWaveCompletedSubject.Dispose();

            // 6. 독립 뷰모델 자원 해제
            ConfirmPopupViewModel.Dispose();
        }

        #endregion

        #region 데이터 폴링 및 상세 갱신

        /// <summary>
        /// [설명]: GameManager의 실시간 수치(코인, 킬, 웨이브)를 확인하여 리액티브 속성을 갱신합니다.
        /// </summary>
        private void PollGameData()
        {
            if (m_dataProvider == null)
            {
                return;
            }

            m_currentWave.Value = m_dataProvider.GetCurrentWave();
            m_currentStageId.Value = m_dataProvider.GetCurrentStageId();
            m_coinCount.Value = m_dataProvider.GetCoinCount();
            m_killCount.Value = m_dataProvider.GetMobKillCount();

            // 웨이브 관리 시스템 로드 시점 조율
            if (!m_isWaveSubscribed)
            {
                SubscribeWaveEvents();
            }
        }

        /// <summary>
        /// [설명]: 플레이어의 현재 레벨, 경험치, 장착 아이콘 등 모든 가시 데이터를 강제 새로고침합니다.
        /// </summary>
        private void RefreshAllData()
        {
            PollGameData();
            UpdateIconLists();

            if (m_dataProvider != null)
            {
                m_playerLevel.Value = (int)m_dataProvider.GetPlayerLevel();
                m_expProgress.Value = m_dataProvider.GetPlayerExpProgress();
            }
        }

        /// <summary>
        /// [설명]: 플레이어 인벤토리의 무기와 장신구 리스트를 순회하여 UI용 썸네일 스프라이트 목록을 생성합니다.
        /// </summary>
        public void UpdateIconLists()
        {
            if (m_playerCtx == null || m_playerCtx.SpawnedPlayer == null)
            {
                return;
            }

            // 1. 무기 계열 추출
            var weapons = new List<Sprite>();
            if (m_playerCtx.SpawnedPlayer.Weapons != null)
            {
                foreach (var w in m_playerCtx.SpawnedPlayer.Weapons)
                {
                    if (w != null && w.Thumbnail != null)
                    {
                        weapons.Add(w.Thumbnail);
                    }
                }
            }
            m_weaponSprites.Value = weapons;

            // 2. 패시브/장신구 계열 추출
            if (m_inventoryCtx != null)
            {
                var accessories = new List<Sprite>();
                var acquired = m_inventoryCtx.InGameAcquiredSkills;
                if (acquired != null)
                {
                    foreach (var skill in acquired)
                    {
                        if (skill.skillType == SkillType.Passive && skill.skillIcon != null)
                        {
                            accessories.Add(skill.skillIcon);
                        }
                    }
                }
                m_accessorySprites.Value = accessories;
            }
        }

        #endregion

        #region 이벤트 연동 핸들러

        /// <summary>
        /// [설명]: 웨이브 스폰 시스템의 물리적 시작/종료 이벤트를 ViewModel 서브젝트와 연결합니다.
        /// </summary>
        private void SubscribeWaveEvents()
        {
            if (m_combatCtx != null && m_combatCtx.ObjectPoolSpawner != null)
            {
                m_combatCtx.ObjectPoolSpawner.OnWaveStarted += HandleWaveStartedEvent;
                m_combatCtx.ObjectPoolSpawner.OnWaveCompleted += HandleWaveCompletedEvent;
                m_isWaveSubscribed = true;
            }
        }

        /// <summary>
        /// [설명]: 시스템 연결 파기 시 웨이브 이벤트 구독을 명시적으로 취소합니다.
        /// </summary>
        private void UnsubscribeWaveEvents()
        {
            if (m_combatCtx != null && m_combatCtx.ObjectPoolSpawner != null)
            {
                m_combatCtx.ObjectPoolSpawner.OnWaveStarted -= HandleWaveStartedEvent;
                m_combatCtx.ObjectPoolSpawner.OnWaveCompleted -= HandleWaveCompletedEvent;
            }
            m_isWaveSubscribed = false;
        }

        /// <summary> [설명]: 웨이브 시작 이벤트 핸들러 </summary>
        private void HandleWaveStartedEvent(WaveData wave) => m_onWaveStartedSubject.OnNext(wave);

        /// <summary> [설명]: 웨이브 완료 이벤트 핸들러 </summary>
        private void HandleWaveCompletedEvent(WaveData wave) => m_onWaveCompletedSubject.OnNext(wave);

        /// <summary>
        /// [설명]: 게임 도중 플레이어 객체가 재성공 등으로 교체될 때 새로운 객체로부터 경험치/레벨업 이벤트를 재등록합니다.
        /// </summary>
        private void HandlePlayerChanged(PlayerBase player)
        {
            // 기존 전역 정적 이벤트 핸들러 정리
            PlayerBase.OnExpChanged -= HandleExpChanged;
            PlayerBase.OnLevelUp -= HandleLevelUp;

            if (player == null)
            {
                return;
            }

            // 새로운 인스턴스 전용 핸들러 등록
            PlayerBase.OnExpChanged += HandleExpChanged;
            PlayerBase.OnLevelUp += HandleLevelUp;

            RefreshAllData();
        }

        /// <summary> [설명]: 경험치 변경 이벤트 핸들러 </summary>
        private void HandleExpChanged(float currentExp, float maxExp)
        {
            m_expProgress.Value = (maxExp > 0) ? currentExp / maxExp : 0f;
        }

        /// <summary> [설명]: 레벨업 이벤트 핸들러 </summary>
        private void HandleLevelUp(float level)
        {
            int intLevel = (int)level;
            m_playerLevel.Value = intLevel;
            m_onLevelUpSubject.OnNext(intLevel);
        }

        #endregion

        #region 스킬 선택 핵심 로직

        /// <summary>
        /// [설명]: 스킬 선택 시퀀스를 시작합니다. 후보군을 추첨하고 시간 제한 타이머를 가동합니다.
        /// </summary>
        public void StartSkillSelection()
        {
            if (m_skillDatabase == null)
            {
                return;
            }

            GenerateSkillChoices();
            m_isSkillSelectionActive.Value = true;

            // 기본 6초 카운트다운 가시화 루틴 기동
            StartSelectionTimer(6.0f).Forget();
        }

        /// <summary>
        /// [설명]: 스킬 선택이 완료(혹은 취소)되었을 때 모든 타이머 루틴을 중단하고 선택 패널을 비활성화합니다.
        /// </summary>
        public void EndSkillSelection()
        {
            if (m_timerCts != null)
            {
                m_timerCts.Cancel();
            }
            
            m_isSkillSelectionActive.Value = false;
        }

        /// <summary>
        /// [설명]: 현재 제시된 선택지가 마음에 들지 않을 때 리롤을 요청하여 목록을 재구성합니다.
        /// </summary>
        public void RefreshSkillChoices()
        {
            if (!m_isSkillSelectionActive.Value)
            {
                return;
            }
            
            GenerateSkillChoices();
        }

        /// <summary>
        /// [설명]: 무기 보유 상태 및 패시브 획득 정보를 대조하여 획득 가능한 최적의 스킬 후보 3개를 무작위 추첨합니다.
        /// </summary>
        private void GenerateSkillChoices()
        {
            if (m_playerCtx == null)
            {
                return;
            }

            // 1. 현재 보유 중인 무기 맵 구성
            Dictionary<string, Weapon.Base.IWeaponController> ownedWeapons;
            if (m_playerCtx.SpawnedPlayer != null && m_playerCtx.SpawnedPlayer.Weapons != null)
            {
                ownedWeapons = m_playerCtx.SpawnedPlayer.Weapons.ToDictionary(w => w.SkillCode);
            }
            else
            {
                ownedWeapons = new Dictionary<string, Weapon.Base.IWeaponController>();
            }

            // 2. 획득한 패시브(장신구) 코드 셋 구성
            var acquiredAccessoryCodes = new HashSet<string>();
            if (m_inventoryCtx != null && m_inventoryCtx.InGameAcquiredSkills != null)
            {
                foreach (var s in m_inventoryCtx.InGameAcquiredSkills)
                {
                    if (s.skillType == SkillType.Passive)
                    {
                        acquiredAccessoryCodes.Add(s.skillCode);
                    }
                }
            }

            // 3. 전체 DB 중 등장 가능 조건(레벨 여유 등)을 충족하는 목록 필터링
            var availableSkills = new List<SkillData>();
            foreach (var skill in m_skillDatabase.allSkills)
            {
                if (skill.skillType == SkillType.Weapon)
                {
                    // 무기: 미보유 상태이거나, 보유 중이나 아직 성장 가능성이 있는 경우
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
                else // Passive (Accessory) 계열
                {
                    // 패시브: 중복 획득 불가 조건에 따라 미보유 시에만 등장
                    if (!acquiredAccessoryCodes.Contains(skill.skillCode))
                    {
                        availableSkills.Add(skill);
                    }
                }
            }

            // 4. 필터링된 목록에서 최종 3개 무작위 선택
            var choices = new List<SkillData>();
            int selectCount = Mathf.Min(3, availableSkills.Count);

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

        /// <summary>
        /// [설명]: 스킬 선택 시간 제한을 수동으로 계산하여 UI에 전달하고, 시간 만료 시 자동 선택 로직을 트리거합니다.
        /// </summary>
        private async UniTaskVoid StartSelectionTimer(float duration)
        {
            // 병렬 실행 방지를 위한 토큰 갱신
            if (m_timerCts != null)
            {
                m_timerCts.Cancel();
                m_timerCts.Dispose();
            }
            
            m_timerCts = new CancellationTokenSource();
            var token = m_timerCts.Token;

            float timer = duration;
            m_selectionTimer.Value = timer;

            try
            {
                while (timer > 0f)
                {
                    // 일시정지 상태 차단 방지 및 Update 루틴 대기
                    await UniTask.Yield(PlayerLoopTiming.Update, token);

                    // 스케일되지 않은 실제 시간 기준으로 차감 (UI 전용)
                    timer -= Time.unscaledDeltaTime;
                    m_selectionTimer.Value = Mathf.Max(0f, timer);
                }

                // 자연 만료 시 첫 번째 후보군 강제 자동 선택 처리
                if (!token.IsCancellationRequested && m_skillChoices.Value.Count > 0)
                {
                    int randomIndex = UnityEngine.Random.Range(0, m_skillChoices.Value.Count);
                    m_onAutoSelectSkillSubject.OnNext(m_skillChoices.Value[randomIndex]);
                }
            }
            catch (OperationCanceledException)
            {
                // 선택 완료로 인한 의도된 중단
            }
        }

        #endregion
    }
}