using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Core.Interfaces;
using InGame.Weapon.Logic;
using InGame.Managers;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// [설명]: 방어막 및 충격파(Shield) 무기의 공격 전략을 담당하는 클래스입니다.
    /// 비동기 루프를 사용하여 애니메이션과 이펙트 생성 타이밍을 제어합니다.
    /// </summary>
    public class ShieldStrategy : IWeaponStrategy
    {
        #region 상수 및 해시

        private static readonly int k_AnimHashAttack = Animator.StringToHash("Drop_shield");

        #endregion

        #region 내부 변수

        private WeaponDataSO m_data;
        private WeaponPoolManager m_poolManager;
        private ShieldWeaponLogic m_logic; // POCO 로직

        // 뷰(View) 인스턴스
        private GameObject m_viewInstance;
        private Animator m_animator;
        
        private Transform m_owner;
        private bool m_isAttacking;

        #endregion

        #region 인터페이스 구현

        public void Init(
            WeaponDataSO data, 
            WeaponPoolManager poolManager,
            IGameStateService gameState,
            ICombatContext combatContext,
            IPlayerContext playerContext)
        {
            m_data = data;
            m_poolManager = poolManager;

            if (m_poolManager == null) return;

            // 1. 충격파 이펙트 풀 등록
            if (data.EffectPrefab != null)
            {
                m_poolManager.GetOrAddPool<ShieldShockwave>(
                    createFunc: () => UnityEngine.Object.Instantiate(data.EffectPrefab).GetComponent<ShieldShockwave>(),
                    actionOnGet: p => p.gameObject.SetActive(true),
                    actionOnRelease: p => p.gameObject.SetActive(false),
                    actionOnDestroy: p => UnityEngine.Object.Destroy(p.gameObject),
                    defaultCapacity: 2,
                    maxSize: 5
                );
            }

            // 2. 부메랑 투사체 풀 등록 (진화 시 사용)
            if (data.ProjectilePrefab != null)
            {
                m_poolManager.GetOrAddPool<ShieldProjectile>(
                    createFunc: () => UnityEngine.Object.Instantiate(data.ProjectilePrefab).GetComponent<ShieldProjectile>(),
                    actionOnGet: p => p.gameObject.SetActive(true),
                    actionOnRelease: p => p.gameObject.SetActive(false),
                    actionOnDestroy: p => UnityEngine.Object.Destroy(p.gameObject),
                    defaultCapacity: 5,
                    maxSize: 15
                );
            }
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            if (m_isAttacking) return;

            // 로직(POCO) 초기화 및 업데이트
            if (m_logic == null) m_logic = new ShieldWeaponLogic(stats);
            else m_logic.UpdateStats(stats);

            // 뷰 인스턴스 지연 생성 (Lazy Init)
            InitializeView(owner, stats);

            m_owner = owner;
            PerformAttackAsync().Forget();
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 방어막 전략은 발동 후 비동기 시퀀스로 동작하므로 프레임 업데이트 불필요
        }

        #endregion

        #region 상세 공격 로직

        /// <summary>
        /// 뷰 인스턴스를 생성하거나 초기화하고, 로직에 필요한 튜닝 데이터를 전달합니다.
        /// </summary>
        private void InitializeView(Transform owner, WeaponRuntimeStats stats)
        {
            if (m_viewInstance == null && m_data.ModelPrefab != null)
            {
                m_viewInstance = UnityEngine.Object.Instantiate(m_data.ModelPrefab, owner);
                m_viewInstance.transform.localPosition = Vector3.zero;
                
                m_animator = m_viewInstance.GetComponentInChildren<Animator>();
                
                if (m_animator != null)
                {
                    m_animator.enabled = true;
                }
                else
                {
                    LogManager.LogError($"[ShieldStrategy] Animator를 찾을 수 없습니다! Prefab: {m_viewInstance.name}", LogManager.LogCategory.Weapon);
                }

                // 뷰 컴포넌트 데이터 로직에 전달
                UpdateLogicWithViewData(stats);
            }
            else if (m_viewInstance != null)
            {
                UpdateLogicWithViewData(stats);
            }
        }

        private void UpdateLogicWithViewData(WeaponRuntimeStats stats)
        {
            Controllers.ShieldWeaponView viewComponent = null;

            if (m_viewInstance != null)
            {
                viewComponent = m_viewInstance.GetComponentInChildren<Controllers.ShieldWeaponView>();
            }
            
            // 무기 인스턴스에 없으면 PoolManager에서 시도
            if (viewComponent == null && m_poolManager != null)
            {
                viewComponent = m_poolManager.GetComponent<Controllers.ShieldWeaponView>();
            }

            if (viewComponent != null)
            {
                var tuningData = new ShieldWeaponTuningData
                {
                    ImpactTriggerTime = viewComponent.ImpactTriggerTime,
                    FollowThroughDelay = viewComponent.FollowThroughDelay,
                    BoomerangSpeed = viewComponent.BoomerangSpeed,
                    ReturnDelay = viewComponent.ReturnDelay,
                    RotationsPerSecond = viewComponent.RotationsPerSecond,
                    ShockwaveOffset = viewComponent.ShockwaveOffset
                };
                m_logic.UpdateStats(stats, tuningData);
            }
            else
            {
                m_logic.UpdateStats(stats);
            }
        }

        /// <summary>
        /// 비동기 방식으로 애니메이션 재생 및 충격파/부메랑 스폰을 처리합니다.
        /// </summary>
        private async UniTaskVoid PerformAttackAsync()
        {
            m_isAttacking = true;
            var token = m_owner.GetCancellationTokenOnDestroy();

            try
            {
                // 애니메이션 시각화
                if (m_animator != null && m_viewInstance != null)
                {
                    m_viewInstance.SetActive(true);
                    m_animator.speed = m_logic.AttackSpeed;
                    m_animator.SetTrigger(k_AnimHashAttack);
                }

                // 애니메이션 타격(내려찍기) 페이즈까지 대기
                float impactDelay = m_logic.GetAdjustedWaitTime(m_logic.ImpactTriggerTime);
                await UniTask.Delay(TimeSpan.FromSeconds(impactDelay), cancellationToken: token);

                // 충격파 스폰
                SpawnShockwave(m_owner.position);

                // 진화 시 추가 부메랑 발사
                if (m_logic.IsEvolved)
                {
                    LaunchBoomerangs(m_owner);
                }

                // 애니메이션 후딜레이(Follow Through) 대기
                float followDelay = m_logic.GetAdjustedWaitTime(m_logic.FollowThroughDelay);
                await UniTask.Delay(TimeSpan.FromSeconds(followDelay), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                // 공격 취소 시 정상 종료
            }
            finally
            {
                if (m_viewInstance != null) m_viewInstance.SetActive(false);
                m_isAttacking = false;
            }
        }

        private void SpawnShockwave(Vector3 position)
        {
            if (m_poolManager == null) return;
            
            var effect = m_poolManager.Get<ShieldShockwave>();
            if (effect != null)
            {
                // 위치 설정 (오프셋 반영)
                Vector3 offset = m_owner.TransformDirection(m_logic.ShockwaveOffset);
                effect.transform.position = position + offset;

                effect.Init(m_logic.AttackPower, m_logic.MobStunTime, m_logic.AttackSpeed, m_poolManager);
            }
        }

        private void LaunchBoomerangs(Transform owner)
        {
            if (m_poolManager == null) return;

            for (int i = 0; i < m_logic.BoomerangCount; i++)
            {
                // POCO 로직을 이용해 발사 방향 계산
                var (direction, rotation) = m_logic.CalculateBoomerangLaunchInfo(i);

                var boomerang = m_poolManager.Get<ShieldProjectile>();
                if (boomerang != null)
                {
                    boomerang.transform.SetPositionAndRotation(owner.position, rotation);

                    // POCO 로직의 프로퍼티를 사용하여 초기화
                    boomerang.Init(
                        m_logic.AttackPower,
                        m_logic.MobStunTime,
                        owner,
                        m_poolManager,
                        direction,
                        m_logic.BoomerangSpeed,
                        m_logic.BoomerangDistance,
                        m_logic.ReturnDelay,
                        m_logic.RotationsPerSecond
                    );
                }
            }
        }

        #endregion
    }
}