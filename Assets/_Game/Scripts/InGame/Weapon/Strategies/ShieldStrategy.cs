using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using System;

namespace InGame.Weapon.Strategies
{
    public class ShieldStrategy : IWeaponStrategy
    {
        private GameObject m_viewInstance;
        private Animator m_animator;
        private static readonly int k_AnimHashAttack = Animator.StringToHash("Attack");
        
        private Transform m_owner;
        private bool m_isAttacking;

        private WeaponDataSO m_data; // 데이터 캐싱

        public void Initialize(WeaponDataSO data)
        {
            m_data = data;

            // 1. 모델(View) 생성
            if (data.ModelPrefab != null)
            {
                // Owner는 Attack 호출 시 받지만, 초기화 시점에는 아직 모를 수 있음.
                // 하지만 Factory에서 Init(data, owner...) 호출 시점에 Strategy.Initialize(data)를 호출한다면?
                // WeaponController.Init에서 m_owner를 설정한 후 m_strategy.Initialize(data)를 호출함.
                // 그러나 Strategy.Initialize 서명은 (WeaponDataSO data)임. Owner 정보가 없음.
                // 해결: WeaponController.Init 수정 or Strategy.Initialize 서명 변경 필요.
                // 현재 WeaponController.Init에서 m_strategy.Initialize(data)를 호출하고 있음.
                // Owner에 붙이려면 Owner를 알아야 함.
                // 임시: Attack에서 생성하거나, Initialize 서명을 변경해야 함.
                
                // 전략 인터페이스 변경이 부담스럽다면? 
                // WeaponController에서 Owner를 주입받는 메서드를 따로 두거나, 
                // Factory에서 생성자 주입을 통해 Owner를 전달할 수 있음.
                // 하지만 Factory는 Owner를 알 수 있음.
                
                // 여기서는 Initialize 호출 시점이 WeaponController.Init 내부이므로, 
                // WeaponController.Init에서 m_strategy.Initialize(data) 호출 전/후에 
                // SetOwner 같은 걸 호출해주면 됨. 
                // 하지만 인터페이스에는 Initialize(data)만 있음.
                
                // 가장 깔끔한 방법: Initialize에 owner 파라미터 추가.
            }
            
            // 2. 풀 등록
            if (data.EffectPrefab != null)
            {
                WeaponPoolManager.Instance.GetOrAddPool<ShieldShockwave>(
                    () => UnityEngine.Object.Instantiate(data.EffectPrefab).GetComponent<ShieldShockwave>(),
                    p => p.gameObject.SetActive(true),
                    p => p.gameObject.SetActive(false),
                    p => UnityEngine.Object.Destroy(p.gameObject),
                    defaultCapacity: 2,
                    maxSize: 5
                );
            }

            if (data.ProjectilePrefab != null) // 부메랑
            {
                WeaponPoolManager.Instance.GetOrAddPool<shieldProjectile>(
                    () => UnityEngine.Object.Instantiate(data.ProjectilePrefab).GetComponent<shieldProjectile>(),
                    p => p.gameObject.SetActive(true),
                    p => p.gameObject.SetActive(false),
                    p => UnityEngine.Object.Destroy(p.gameObject),
                    defaultCapacity: 5,
                    maxSize: 15
                );
            }
        }

        // Owner 주입을 위한 메서드 (인터페이스에 없으므로 캐스팅해서 사용하거나 인터페이스 수정)
        // 여기서는 Attack 메서드에서 최초 1회 생성하는 방식으로 처리 (Lazy Init)
        
        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            if (m_isAttacking) return;

            // View Lazy Initialization
            if (m_viewInstance == null && m_data.ModelPrefab != null)
            {
                m_viewInstance = UnityEngine.Object.Instantiate(m_data.ModelPrefab, owner);
                m_viewInstance.transform.localPosition = Vector3.zero;
                m_animator = m_viewInstance.GetComponent<Animator>();
            }

            m_owner = owner;
            AttackAsync(stats).Forget();
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime) { }

        private async UniTaskVoid AttackAsync(WeaponRuntimeStats stats)
        {
            m_isAttacking = true;
            var token = m_owner.GetCancellationTokenOnDestroy();

            try
            {
                float attackSpeed = stats.CurrentAttackSpeed > 0 ? stats.CurrentAttackSpeed : 1f;

                // 애니메이션 재생
                if (m_animator != null)
                {
                    m_viewInstance.SetActive(true);
                    m_animator.speed = attackSpeed;
                    m_animator.SetTrigger(k_AnimHashAttack);
                }

                // 타격 타이밍 대기 (약 1.07초 / 공속)
                float waitTime = 1.07f / attackSpeed;
                await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: token);

                // 충격파 생성
                SpawnShockwave(stats, m_owner.position);

                // 진화 시 부메랑 발사
                if (stats.IsEvolved)
                {
                    LaunchBoomerangs(stats, m_owner);
                }

                // 후딜레이 (남은 애니메이션 시간 등)
                // 단순히 쿨타임은 Controller가 관리하므로, 여기서는 애니메이션 종료 대기 정도만
                // 혹은 뷰를 끄기 위해 대기
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f / attackSpeed), cancellationToken: token);
            }
            finally
            {
                if (m_viewInstance != null)
                {
                    m_viewInstance.SetActive(false); // 평소엔 숨김
                }
                m_isAttacking = false;
            }
        }

        private void SpawnShockwave(WeaponRuntimeStats stats, Vector3 position)
        {
            var effect = WeaponPoolManager.Instance.Get<ShieldShockwave>();
            if (effect != null)
            {
                effect.transform.position = position;
                effect.Initialize(stats.CurrentAttackPower, stats.MobStunTime, stats.CurrentAttackSpeed);
            }
        }

        private void LaunchBoomerangs(WeaponRuntimeStats stats, Transform owner)
        {
            // 부메랑 개수 등은 stats.ProjectileCount 사용 권장 (기본값 설정 필요)
            int count = stats.CurrentProjectileCount > 0 ? stats.CurrentProjectileCount : 5;
            float angleStep = 360f / count;
            
            for (int i = 0; i < count; i++)
            {
                var boomerang = WeaponPoolManager.Instance.Get<shieldProjectile>();
                if (boomerang != null)
                {
                    float angle = i * angleStep;
                    Quaternion rotation = Quaternion.Euler(0, 0, angle);
                    Vector3 dir = rotation * Vector3.up;

                    boomerang.transform.SetPositionAndRotation(owner.position, rotation);
                    
                    // 매직 넘버들은 추후 데이터화 필요
                    boomerang.Initialize(
                        stats.CurrentAttackPower, 
                        stats.MobStunTime, 
                        owner, 
                        dir, 
                        5f * stats.CurrentAttackSpeed, // Speed 
                        3f * stats.CurrentAttackRange, // Distance 
                        0.1f, // Return Delay
                        2.5f // Rotation Speed
                    );
                }
            }
        }
    }
}
