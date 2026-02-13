using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.Manager;
using InGame.Mob.Data;
using InGame.Mob.Systems;

namespace InGame.Mob
{
    /// <summary>
    /// Behavior Tree 기반의 AI를 가진 일반 몬스터 클래스입니다.
    /// <br/> 플레이어 감지 시 추적(Chase)하고, 그렇지 않으면 배회(Wander)합니다.
    /// </summary>
    public class NormalMob : MobBase.MobBase
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("1. 데이터 설정")]
        [SerializeField, Tooltip("몬스터 스탯 데이터 (ScriptableObject)")]
        private MobStatsData m_statsData;

        #endregion

        #region 2. 내부 변수 및 컴포넌트
 
        [SerializeField] private MobView m_view;
        private Bounds m_mapBounds;
 
        #endregion

        #region 3. 유니티 생명주기

        private void Awake()
        {
            if (m_view == null) m_view = GetComponent<MobView>();
            if (m_view == null) m_view = gameObject.AddComponent<MobView>();
        }

        public override void OnEnable()
        {
            InitializeMapBounds();
            InitializeLogicAndBrain();

            base.OnEnable(); 

            StartAILoopAsync().Forget();
        }
 
        private void Update()
        {
            if (m_logic == null || IsDead) return;
 
            m_logic.Update(Time.deltaTime);
            m_view.UpdatePosition(m_logic.Position, immediate: true);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        #endregion

        #region 4. 초기화 로직

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

        #region 5. AI 루프 (Brain Delegation)

        public override void Init(MobManager mobManager)
        {
            base.Init(mobManager);

            if (m_logic != null)
            {
                m_logic.SyncPosition(transform.position);
            }
        }

        private async UniTaskVoid StartAILoopAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();

            // 1. 플레이어 생성 대기
            await UniTask.WaitUntil(() => m_player != null, cancellationToken: token);

            if (m_brain is NormalMobBrain normalBrain)
            {
                normalBrain.SetPlayerTransform(m_playerTransform);
            }

            // 2. 맵 이탈 복귀
            if (!m_mapBounds.Contains(transform.position))
            {
                await ReturnToMapAsync(token);
            }

            // 3. 메인 루프 (Brain에 위임)
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

        #endregion
        


        #region 7. 이동 및 유틸리티

        private async UniTask ReturnToMapAsync(System.Threading.CancellationToken token)
        {
            Vector3 safePos = m_mapBounds.ClosestPoint(transform.position);
            safePos.z = 0; // 2D 게임 가정
 
            m_logic.SetState(MobState.Move);
            m_logic.SetTargetPosition(safePos);
 
            float duration = Vector3.Distance(transform.position, safePos) / (m_logic.MoveSpeed * 2f); // 빠르게 복귀
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: token);
            m_logic.SetState(MobState.Idle);
        }

        #endregion

        #region 8. 전투 처리 (Override)

        public override void PlayDamageEffect(Color? color = null)
        {
            m_view.PlayDamageEffect(color);
        }

        public override void TakeDamage(float damage, float stunTime = 0f)
        {
            if (m_logic.CurrentState == MobState.Die) return;
 
            // 1. 피격 연출 (View)
            m_view.PlayDamageEffect();
 
            // 2. 로직 및 공통 전투 처리 (Base에서 Stun 관리됨)
            base.TakeDamage(damage, stunTime);
 
            // 3. 사운드
            if (CanPlayHitSound())
            {
                SoundManager.PlaySound(Sound.SFX, SoundKeys.Enemyhit);
            }
        }

        public override void ApplySlow(float slowAmount, float duration)
        {
            // TODO: MobLogic에 슬로우 로직 위임 (Stats 조작)
        }

        protected override void OnDie()
        {
            if (IsDead) return;
 
            SoundManager.PlaySound(Sound.SFX, SoundKeys.EnemyDeth);
            base.OnDie();
        }

        private async UniTaskVoid ResetHitFlagAsync()
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            IsHit = false;
        }

        #endregion

        #region 9. 디버그 (Debug)

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (m_statsData == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, m_statsData.SearchRange);
        }
#endif

        #endregion
    }
}