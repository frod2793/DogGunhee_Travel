using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using System;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 방어막 및 충격파(Shield) 무기의 공격 전략을 담당하는 클래스입니다.
    /// </summary>
    public class ShieldStrategy : IWeaponStrategy
    {
        #region 상수

        private static readonly int k_AnimHashAttack = Animator.StringToHash("Attack");

        #endregion

        #region 내부 상태 및 변수

        private WeaponDataSO m_data;
        private GameObject m_viewInstance;
        private Animator m_animator;
        private Transform m_owner;
        private bool m_isAttacking;

        #endregion

        #region IWeaponStrategy 구현

        public void Init(WeaponDataSO data)
        {
            m_data = data;

            // 1. 충격파 이펙트 풀 등록
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

            // 2. 부메랑 투사체 풀 등록 (진화 시 사용)
            if (data.ProjectilePrefab != null) 
            {
                WeaponPoolManager.Instance.GetOrAddPool<ShieldProjectile>(
                    () => UnityEngine.Object.Instantiate(data.ProjectilePrefab).GetComponent<ShieldProjectile>(),
                    p => p.gameObject.SetActive(true),
                    p => p.gameObject.SetActive(false),
                    p => UnityEngine.Object.Destroy(p.gameObject),
                    defaultCapacity: 5,
                    maxSize: 15
                );
            }
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            if (m_isAttacking)
            {
                return;
            }

            // 뷰 인스턴스 지연 생성
            if (m_viewInstance == null && m_data.ModelPrefab != null)
            {
                m_viewInstance = UnityEngine.Object.Instantiate(m_data.ModelPrefab, owner);
                m_viewInstance.transform.localPosition = Vector3.zero;
                m_animator = m_viewInstance.GetComponent<Animator>();
            }

            m_owner = owner;
            PerformAttackAsync(stats).Forget();
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 방어막 전략은 별도의 프레임 업데이트가 필요 없음
        }

        #endregion

        #region 상세 공격 로직

        /// <summary>
        /// 비동기 방식으로 애니메이션 재생 및 충격파/부메랑 스폰을 처리합니다.
        /// </summary>
        private async UniTaskVoid PerformAttackAsync(WeaponRuntimeStats stats)
        {
            m_isAttacking = true;
            var token = m_owner.GetCancellationTokenOnDestroy();

            try
            {
                float attackSpeed = stats.CurrentAttackSpeed > 0 ? stats.CurrentAttackSpeed : 1f;

                // 애니메이션 시각화
                if (m_animator != null)
                {
                    m_viewInstance.SetActive(true);
                    m_animator.speed = attackSpeed;
                    m_animator.SetTrigger(k_AnimHashAttack);
                }

                // 애니메이션 타격 페이즈까지 대기 (약 1.07초 기준 보정)
                float waitTime = 1.07f / attackSpeed;
                await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: token);

                // 충격파 스폰
                SpawnShockwave(stats, m_owner.position);

                // 진화 시 추가 부메랑 발사
                if (stats.IsEvolved)
                {
                    LaunchBoomerangs(stats, m_owner);
                }

                // 애니메이션 연출 종료 대기
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f / attackSpeed), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                // 취소 시 처리
            }
            finally
            {
                if (m_viewInstance != null)
                {
                    m_viewInstance.SetActive(false);
                }
                m_isAttacking = false;
            }
        }

        /// <summary>
        /// 오브젝트 풀에서 충격파를 가져와 초기화합니다.
        /// </summary>
        private void SpawnShockwave(WeaponRuntimeStats stats, Vector3 position)
        {
            var effect = WeaponPoolManager.Instance.Get<ShieldShockwave>();
            if (effect != null)
            {
                effect.transform.position = position;
                effect.Init(stats.CurrentAttackPower, stats.MobStunTime, stats.CurrentAttackSpeed);
            }
        }

        /// <summary>
        /// 진화 시 플레이어 주변으로 부메랑 투사체를 사출합니다.
        /// </summary>
        private void LaunchBoomerangs(WeaponRuntimeStats stats, Transform owner)
        {
            int count = stats.CurrentProjectileCount > 0 ? stats.CurrentProjectileCount : 5;
            float angleStep = 360f / count;
            
            for (int i = 0; i < count; i++)
            {
                var boomerang = WeaponPoolManager.Instance.Get<ShieldProjectile>();
                if (boomerang != null)
                {
                    float angle = i * angleStep;
                    Quaternion rotation = Quaternion.Euler(0, 0, angle);
                    Vector3 dir = rotation * Vector3.up;

                    boomerang.transform.SetPositionAndRotation(owner.position, rotation);
                    
                    boomerang.Init(
                        stats.CurrentAttackPower, 
                        stats.MobStunTime, 
                        owner, 
                        dir, 
                        5f * stats.CurrentAttackSpeed, 
                        3f * stats.CurrentAttackRange, 
                        0.1f, 
                        2.5f
                    );
                }
            }
        }

        #endregion
    }
}
