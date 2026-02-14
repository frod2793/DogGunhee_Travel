using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.Managers;
using InGame.Mob.Data;
using InGame.Mob.Systems;

namespace InGame.Mob
{
    /// <summary>
    /// [설명]: Behavior Tree 기반의 AI를 가진 일반 몬스터 클래스입니다.
    /// 플레이어 감지 시 추적(Chase)하고, 그렇지 않으면 배회(Wander)하는 핵심 루틴을 가집니다.
    /// </summary>
    public class NormalMob : MobBase.MobBase
    {
        #region 에디터 설정

        [Header("1. 데이터 설정")]
        [SerializeField, Tooltip("몬스터 스탯 데이터 (ScriptableObject)")]
        private MobStatsData m_statsData;

        #endregion

        #region 내부 상속 및 참조 필드

        [Header("시각적 컴포넌트")]
        [SerializeField]
        private MobView m_view;

        /// <summary> 랜덤 배회 가능 영역 </summary>
        private Bounds m_mapBounds;

        #endregion

        #region 유니티 생명주기

        /// <summary>
        /// [설명]: 오브젝트 생성 시 시각적 컴포넌트(MobView)를 캐싱하거나 생성합니다.
        /// </summary>
        private void Awake()
        {
            if (m_view == null)
            {
                m_view = GetComponent<MobView>();
            }

            if (m_view == null)
            {
                m_view = gameObject.AddComponent<MobView>();
            }
        }

        /// <summary>
        /// [설명]: 활성화 시 맵 경계와 로직/브레인을 초기화하고 AI 루틴을 시작합니다.
        /// </summary>
        public override void OnEnable()
        {
            InitializeMapBounds();
            InitializeLogicAndBrain();

            base.OnEnable();

            StartAILoopAsync().Forget();
        }

        /// <summary>
        /// [설명]: 비즈니스 로직에 따른 위치/상태 업데이트를 매 프레임 수행하고 뷰에 반영합니다.
        /// </summary>
        private void Update()
        {
            if (m_logic == null || IsDead)
            {
                return;
            }

            m_logic.Update(Time.deltaTime);
            m_view.UpdatePosition(m_logic.Position, immediate: true);
        }

        /// <summary>
        /// [설명]: 비활성화 시 기본 리소스 및 이벤트를 정리합니다.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();
        }

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 인게임 관리자로부터 전역 설정을 주입받고 위치를 동기화합니다.
        /// </summary>
        public override void Init(MobManager mobManager)
        {
            base.Init(mobManager);

            if (m_logic != null)
            {
                m_logic.SyncPosition(transform.position);
            }
        }

        /// <summary>
        /// [설명]: 몬스터의 인게임 로직(MobLogic)과 지능(MobBrain) 객체를 생성하고 이벤트를 바인딩합니다.
        /// </summary>
        private void InitializeLogicAndBrain()
        {
            if (m_statsData == null)
            {
                Debug.LogError($"[NormalMob] {gameObject.name}에 MobStatsData가 할당되지 않았습니다.");
                return;
            }

            // 1. 스탯 구조체 생성 (기존 시스템 호환용)
            var stats = new MobBase.MobStats(
                m_statsData.MaxHp,
                m_statsData.MoveSpeed,
                m_statsData.AttackDamage,
                m_statsData.AttackSpeed,
                m_statsData.AttackRange,
                m_statsData.StunResistance
            );

            // 2. 로직 및 브레인 생성 (DI)
            m_logic = new MobLogic(stats, transform.position, new LinearMovementStrategy());
            m_brain = new NormalMobBrain(m_logic, m_view, m_statsData, m_mapBounds);
            m_brain.Initialize();

            // 3. 브레인에 필요한 참조 설정
            if (m_brain is NormalMobBrain normalBrain)
            {
                normalBrain.SetPlayerTransform(m_playerTransform);
            }

            // 4. 뷰 이벤트 연결
            m_logic.OnStateChanged += m_view.OnStateChanged;
            m_logic.OnDie += OnDie;

            m_view.OnStateChanged(m_logic.CurrentState);
        }

        /// <summary>
        /// [설명]: 게임 매니저로부터 현재 맵의 월드 경계 데이터를 가져옵니다.
        /// </summary>
        private void InitializeMapBounds()
        {
            if (GameManager.Instance != null)
            {
                m_mapBounds = GameManager.Instance.MapBounds;
            }
            else
            {
                m_mapBounds = new Bounds(Vector3.zero, Vector3.one * 50f);
            }
        }

        #endregion

        #region AI 결정 루틴

        /// <summary>
        /// [설명]: 비동기로 동작하는 메인 AI 루프입니다. 플레이어 탐색 후 행동 트리 평가를 반복합니다.
        /// </summary>
        private async UniTaskVoid StartAILoopAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();

            // 1. 플레이어 생성 대기
            await UniTask.WaitUntil(() => m_player != null, cancellationToken: token);

            if (m_brain is NormalMobBrain normalBrain)
            {
                normalBrain.SetPlayerTransform(m_playerTransform);
            }

            // 2. 맵 이탈 시 복귀 시도
            if (!m_mapBounds.Contains(transform.position))
            {
                await ReturnToMapAsync(token);
            }

            // 3. 메인 사고 루프 (Brain에 위임)
            while (!IsDead && isActiveAndEnabled)
            {
                if (!IsMoveEnabled)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                    continue;
                }

                await m_brain.EvaluateAsync();
                await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: token);
            }
        }

        /// <summary>
        /// [설명]: 몬스터가 맵 경계 밖에서 스폰되었거나 밀려난 경우, 가장 가까운 맵 내부로 복귀합니다.
        /// </summary>
        private async UniTask ReturnToMapAsync(System.Threading.CancellationToken token)
        {
            Vector3 safePos = m_mapBounds.ClosestPoint(transform.position);
            safePos.z = 0; // 2D 평면 고정

            m_logic.SetState(MobState.Move);
            m_logic.SetTargetPosition(safePos);

            float duration = Vector3.Distance(transform.position, safePos) / (m_logic.MoveSpeed * 2f); // 평소보다 빠른 속도로 복귀
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: token);
            m_logic.SetState(MobState.Idle);
        }

        #endregion

        #region 전투 및 피격 처리

        /// <summary>
        /// [설명]: 피격 시 시각적 효과를 재생하도록 뷰에 요청합니다.
        /// </summary>
        public override void PlayDamageEffect(Color? color = null)
        {
            m_view.PlayDamageEffect(color);
        }

        /// <summary>
        /// [설명]: 데미지를 입었을 때의 처리를 수행합니다. (연출, 로직 갱신, 사운드 재생)
        /// </summary>
        public override void TakeDamage(float damage, float stunTime = 0f)
        {
            if (m_logic.CurrentState == MobState.Die)
            {
                return;
            }

            // 1. 피격 연출 (View)
            m_view.PlayDamageEffect();

            // 2. 로직 및 공통 전투 처리 (Base에서 체력 감소 및 Stun 상태 타이머 관리됨)
            base.TakeDamage(damage, stunTime);

            // 3. 타격 사운드 (전역 쿨타임 체크)
            if (CanPlayHitSound())
            {
                SoundManager.PlaySound(Sound.SFX, SoundKeys.Enemyhit);
            }
        }

        /// <summary>
        /// [설명]: 이동 속도 감소(슬로우) 효과를 적용합니다. (추후 구현 예정)
        /// </summary>
        public override void ApplySlow(float slowAmount, float duration)
        {
            // TODO: MobLogic에 슬로우 로직 위임 (Stats 조작 로직 추가 필요)
        }

        /// <summary>
        /// [설명]: 사망 시 연출을 수행하고 전역 통계 등을 갱신합니다.
        /// </summary>
        protected override void OnDie()
        {
            if (IsDead)
            {
                return;
            }

            SoundManager.PlaySound(Sound.SFX, SoundKeys.EnemyDeth);
            base.OnDie();
        }

        /// <summary>
        /// [설명]: 피격 플래그를 한 프레임 뒤에 초기화하는 헬퍼 메서드입니다.
        /// </summary>
        private async UniTaskVoid ResetHitFlagAsync()
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            IsHit = false;
        }

        #endregion

        #region 에디터 지원 (Debug)

#if UNITY_EDITOR
        /// <summary>
        /// [설명]: 선택된 몬스터의 인식 범위를 에디터 상에서 시각화합니다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (m_statsData == null)
            {
                return;
            }
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, m_statsData.SearchRange);
        }
#endif

        #endregion
    }
}