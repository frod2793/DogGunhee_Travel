using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using InGame.Mob.MobBase;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon.Logic;
using InGame.Weapon.Strategies;

namespace InGame.Weapon
{
    /// <summary>
    /// 먹물(BlackWater) 무기의 지속 효과 영역을 관리하는 컴포넌트입니다.
    /// <br/> 영역 내 적에게 지속적인 피해(DoT)와 슬로우(Slow) 상태이상을 부여합니다.
    /// </summary>
    public class BlackWaterEffect : MonoBehaviour, IAuraEffect
    {
        #region 1. 내부 변수 및 컴포넌트 (Components & State)

        // 설정 데이터 (Inspector)
        [Header("1. 감지 설정")]
        [SerializeField] private LayerMask m_targetLayer;
        
        [Header("2. 컴포넌트 참조")]
        [SerializeField] private Animator m_animator;
        [SerializeField] private Collider2D m_collider2D;

        // 로직 (POCO)
        private BlackWaterLogic m_logic;

        // 런타임 상태
        private Vector3 m_originalScale;
        private CancellationTokenSource m_cts;
        private bool m_isEvolvedState = false;

        // 물리 연산 최적화
        private ContactFilter2D m_contactFilter;
        private readonly List<Collider2D> m_hitResults = new List<Collider2D>(20);

        // 애니메이션 해시
        private static readonly int k_AnimStateLevel1 = Animator.StringToHash("Level1");
        private static readonly int k_AnimTriggerLevel2 = Animator.StringToHash("Level2");

        #endregion

        #region 2. Unity 라이프사이클 (Lifecycle)

        private void Awake()
        {
            // 컴포넌트 캐싱 및 초기화
            if (m_collider2D == null) m_collider2D = GetComponentInChildren<Collider2D>();
            if (m_animator == null) m_animator = GetComponentInChildren<Animator>();

            if (m_collider2D != null)
            {
                m_collider2D.enabled = false;
                m_collider2D.isTrigger = true;
            }

            m_originalScale = transform.localScale;

            // 물리 필터 설정 (GC Alloc 방지)
            m_contactFilter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = true,
                layerMask = m_targetLayer
            };
        }

        private void OnDisable()
        {
            Cleanup();
        }

        #endregion

        #region 3. 인터페이스 구현 (IAuraEffect Implementation)

        /// <summary>
        /// 무기 스탯과 튜닝 데이터를 기반으로 효과를 초기화하고 활성화합니다.
        /// </summary>
        public void Init(WeaponRuntimeStats stats, WeaponPoolManager poolManager)
        {
            // 튜닝 데이터 추출 (View)
            BlackWaterTuningData? tuningData = null;
            if (poolManager != null)
            {
                // PoolManager나 연관된 객체에서 View 컴포넌트 검색
                // (BlackWaterView 클래스가 존재한다고 가정)
                var view = poolManager.GetComponent<Controllers.BlackWaterView>(); 
                if (view != null)
                {
                    tuningData = new BlackWaterTuningData
                    {
                        DamageTickInterval = view.DamageTickInterval,
                        SlowAmount = view.SlowAmount,
                        SlowDuration = view.SlowDuration
                    };
                }
            }

            // 로직(POCO) 생성
            m_logic = new BlackWaterLogic(stats, tuningData);
            
            // 상태 강제 동기화를 위해 현재 상태 반전 초기화
            m_isEvolvedState = !m_logic.IsEvolved;

            ActivateEffect();
        }

        /// <summary>
        /// 런타임 중 스탯 변경(레벨업 등) 시 호출됩니다.
        /// </summary>
        public void UpdateStats(WeaponRuntimeStats stats)
        {
            if (m_logic == null) return;

            m_logic.UpdateStats(stats);
            SyncWeaponVisuals();
        }

        /// <summary>
        /// 효과를 비활성화하고 정리합니다.
        /// </summary>
        public void Deactivate()
        {
            Cleanup();
            gameObject.SetActive(false);
        }

        private void Cleanup()
        {
            m_cts?.Cancel();
            m_cts?.Dispose();
            m_cts = null;

            transform.DOKill();

            if (m_collider2D != null)
            {
                m_collider2D.enabled = false;
            }
        }

        #endregion

        #region 4. 상세 효과 로직 (Effect Logic)

        /// <summary>
        /// 이펙트 활성화 연출(등장) 및 데미지 루프를 시작합니다.
        /// </summary>
        private void ActivateEffect()
        {
            // 이전 작업 취소
            m_cts?.Cancel();
            m_cts = new CancellationTokenSource();

            // 비주얼 및 콜라이더 설정
            SyncWeaponVisuals();
            if (m_collider2D != null) m_collider2D.enabled = true;

            // 등장 애니메이션 (Scale Up)
            transform.localScale = Vector3.zero;
            transform.DOScale(m_originalScale, 0.3f).SetEase(Ease.OutBack);

            // 데미지 틱 루프 시작
            TickDamageLoopAsync(m_cts.Token).Forget();
        }

        /// <summary>
        /// 일정 주기(Tick)마다 영역 내 적에게 데미지를 입히는 비동기 루프입니다.
        /// </summary>
        private async UniTaskVoid TickDamageLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && m_logic != null)
                {
                    // 1. 데미지 처리
                    ProcessTickDamage();

                    // 2. 다음 틱까지 대기 (공격 속도 반영)
                    float tickDelay = m_logic.GetAdjustedTickDelay();
                    await UniTask.Delay(System.TimeSpan.FromSeconds(tickDelay), cancellationToken: token);
                }
            }
            catch (System.OperationCanceledException)
            {
                // 정상 취소
            }
            finally
            {
                if (m_collider2D != null) m_collider2D.enabled = false;
            }
        }

        /// <summary>
        /// 진화 상태에 따라 애니메이션과 비주얼을 동기화합니다.
        /// </summary>
        private void SyncWeaponVisuals()
        {
            if (m_logic == null) return;

            // 스케일 복구 (애니메이션 등에 의해 변경되었을 수 있음)
            if (transform.localScale == Vector3.zero)
            {
                transform.localScale = m_originalScale;
            }

            if (m_animator != null)
            {
                // 진화 상태가 변경되었을 때만 애니메이션 트리거
                if (m_logic.IsEvolved != m_isEvolvedState)
                {
                    m_isEvolvedState = m_logic.IsEvolved;
                    
                    if (m_isEvolvedState)
                    {
                        m_animator.SetTrigger(k_AnimTriggerLevel2);
                    }
                    else
                    {
                        m_animator.Play(k_AnimStateLevel1);
                    }
                }
            }
        }

        /// <summary>
        /// 물리 충돌 검사를 수행하고, 감지된 적에게 데미지와 슬로우를 적용합니다.
        /// </summary>
        private void ProcessTickDamage()
        {
            if (m_collider2D == null || m_logic == null) return;

            // Non-Alloc 충돌 검사
            int hitCount = m_collider2D.Overlap(m_contactFilter, m_hitResults);

            for (int i = 0; i < hitCount; i++)
            {
                var target = m_hitResults[i];
                if (target == null) continue;

                // MobBase 컴포넌트 확인
                if (target.TryGetComponent(out MobBase mob) && !mob.IsDead)
                {
                    // 데미지 적용
                    mob.TakeDamage(m_logic.AttackPower);

                    // 진화 시 슬로우 효과 추가
                    if (m_logic.IsEvolved)
                    {
                        mob.ApplySlow(m_logic.SlowAmount, m_logic.SlowDuration);
                    }
                }
            }
        }

        #endregion
    }
}