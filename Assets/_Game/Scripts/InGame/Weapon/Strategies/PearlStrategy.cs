using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon.Logic;
using InGame.Weapon.Controllers;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// PearlWeaponLogic과 PearlWeaponView를 사용하는 전략 클래스입니다.
    /// </summary>
    public class PearlStrategy : IWeaponStrategy
    {
        private PearlWeaponLogic m_logic;
        private PearlWeaponView m_view;

        public void Initialize(WeaponDataSO data)
        {
            // 1. View 추출 (중앙 제어: WeaponPoolManager 오브젝트에 부착됨)
            if (WeaponPoolManager.Instance != null)
            {
                m_view = WeaponPoolManager.Instance.GetComponent<PearlWeaponView>();
            }

            if (m_view == null)
            {
                Debug.LogWarning("[PearlStrategy] WeaponPoolManager에 PearlWeaponView가 없습니다. 기본값을 생성합니다.");
                var go = (WeaponPoolManager.Instance != null) ? WeaponPoolManager.Instance.gameObject : new GameObject("PearlWeaponView_Default");
                m_view = go.GetComponent<PearlWeaponView>() ?? go.AddComponent<PearlWeaponView>();
            }

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
            // 로직 초기화 또는 갱신
            if (m_logic == null)
            {
                var tuningData = new PearlTuningData
                {
                    HitCooldown = m_view.HitCooldown
                };
                m_logic = new PearlWeaponLogic(stats, tuningData);
            }
            else
            {
                m_logic.UpdateStats(stats);
            }

            // 이미 존재하면 스탯만 갱신하고 리턴
            if (PearlProjectile.Instance != null)
            {
                PearlProjectile.Instance.UpdateState(); // Logic이 이미 주입되어 있음
                return;
            }

            // 존재하지 않으면 생성
            var pearl = WeaponPoolManager.Instance.Get<PearlProjectile>();
            if (pearl != null)
            {
                pearl.transform.position = owner.position;
                
                if (direction == Vector3.zero) 
                    direction = Random.insideUnitCircle.normalized;

                float speed = m_logic.AttackSpeed;
                Vector3 velocity = direction.normalized * speed;

                pearl.Initialize(m_logic, m_view, velocity);
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            if (m_logic != null)
            {
                m_logic.UpdateStats(stats);
            }

            // 매 프레임 스탯 동기화
            if (PearlProjectile.Instance != null)
            {
                PearlProjectile.Instance.UpdateState();
            }
        }
    }
}
