using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon.Logic; // ShieldWeaponLogic 참조
using System;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 방어막 및 충격파(Shield) 무기의 공격 전략을 담당하는 클래스입니다.
    /// <br/> 비동기 루프를 사용하여 애니메이션과 이펙트 생성 타이밍을 제어합니다.
    /// </summary>
    public class ShieldStrategy : IWeaponStrategy
    {
        #region 1. 상수 및 해시 (Constants)

        private static readonly int k_AnimHashAttack = Animator.StringToHash("Attack");

        #endregion

        #region 2. 내부 변수 (Internal State)

        private WeaponDataSO m_data;
        private WeaponPoolManager m_poolManager;
        private ShieldWeaponLogic m_logic; // POCO 로직

        // 뷰(View) 인스턴스
        private GameObject m_viewInstance;
        private Animator m_animator;
        
        private Transform m_owner;
        private bool m_isAttacking;

        #endregion

        #region 3. 인터페이스 구현 (IWeaponStrategy Implementation)

        public void Init(WeaponDataSO data, WeaponPoolManager poolManager)
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
            if (m_viewInstance == null && m_data.ModelPrefab != null)
            {
                m_viewInstance = UnityEngine.Object.Instantiate(m_data.ModelPrefab, owner);
                m_viewInstance.transform.localPosition = Vector3.zero;
                m_animator = m_viewInstance.GetComponent<Animator>();
            }

            m_owner = owner;
            PerformAttackAsync().Forget();
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 방어막 전략은 발동 후 비동기 시퀀스로 동작하므로 프레임 업데이트 불필요
        }

        #endregion

        #region 4. 상세 공격 로직 (Attack Logic)

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
                if (m_animator != null)
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
                effect.transform.position = position;
                effect.Init(m_logic.AttackPower, m_logic.MobStunTime, m_logic.AttackSpeed, m_poolManager);
            }
        }

        /// <summary>
        /// 진화 시 플레이어 주변으로 부메랑 투사체를 사출합니다.
        /// </summary>
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