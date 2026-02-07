using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    public class PearlStrategy : IWeaponStrategy
    {
        private WeaponDataSO m_data;

        public void Initialize(WeaponDataSO data)
        {
            m_data = data;

            if (data.ProjectilePrefab != null)
            {
                // PearlProjectile은 단 하나만 존재 (Singleton 성격)
                WeaponPoolManager.Instance.GetOrAddPool<PearlProjectile>(
                    () => Object.Instantiate(data.ProjectilePrefab).GetComponent<PearlProjectile>(),
                    p => p.gameObject.SetActive(true),
                    p => p.gameObject.SetActive(false),
                    p => Object.Destroy(p.gameObject),
                    defaultCapacity: 1,
                    maxSize: 1
                );
            }
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            // 이미 존재하면 스탯만 갱신하고 리턴
            if (PearlProjectile.Instance != null)
            {
                PearlProjectile.Instance.UpdateState(
                    stats.CurrentAttackPower,
                    stats.MobStunTime,
                    stats.CurrentAttackSpeed,
                    stats.IsEvolved
                );
                return;
            }

            // 존재하지 않으면 생성
            var pearl = WeaponPoolManager.Instance.Get<PearlProjectile>();
            if (pearl != null)
            {
                pearl.transform.position = owner.position;
                
                if (direction == Vector3.zero) 
                    direction = Random.insideUnitCircle.normalized;

                float speed = stats.CurrentAttackSpeed > 0 ? stats.CurrentAttackSpeed : 1f;

                pearl.Initialize(
                    stats.CurrentAttackPower,
                    stats.MobStunTime,
                    speed,
                    stats.IsEvolved,
                    direction * speed
                );
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 매 프레임 스탯 동기화 (선택 사항, Attack에서 해도 충분할 수 있으나 반응성을 위해)
            if (PearlProjectile.Instance != null)
            {
                PearlProjectile.Instance.UpdateState(
                    stats.CurrentAttackPower,
                    stats.MobStunTime,
                    stats.CurrentAttackSpeed,
                    stats.IsEvolved
                );
            }
        }
    }
}
