using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon.Logic;
using InGame.Weapon.Controllers;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 진주(Pearl) 무기의 전략 클래스입니다.
    /// <br/> 화면에 단 하나의 진주만 유지하며, 스탯 변경 시 이를 동기화합니다.
    /// </summary>
    public class PearlStrategy : IWeaponStrategy
    {
        #region 1. 내부 변수 (Internal State)

        private PearlWeaponLogic m_logic;
        private PearlWeaponView m_view;
        private WeaponPoolManager m_poolManager;
        
        // 현재 활성화된 진주 (Single Instance)
        private PearlProjectile m_activePearl;

        #endregion

        #region 2. 인터페이스 구현 (IWeaponStrategy Implementation)

        public void Init(WeaponDataSO data, WeaponPoolManager poolManager)
        {
            m_poolManager = poolManager;

            // View 컴포넌트 연결
            if (m_poolManager != null)
            {
                m_view = m_poolManager.GetComponent<PearlWeaponView>();
            }

            if (m_view == null)
            {
                var go = (m_poolManager != null) ? m_poolManager.gameObject : new GameObject("PearlWeaponView_Default");
                m_view = go.GetComponent<PearlWeaponView>() ?? go.AddComponent<PearlWeaponView>();
            }

            // 단일 투사체 풀 등록
            if (data.ProjectilePrefab != null && m_poolManager != null)
            {
                m_poolManager.GetOrAddPool<PearlProjectile>(
                    createFunc: () => Object.Instantiate(data.ProjectilePrefab).GetComponent<PearlProjectile>(),
                    actionOnGet: p => 
                    {
                        m_activePearl = p;
                        p.gameObject.SetActive(true);
                    },
                    actionOnRelease: p => 
                    {
                        if (m_activePearl == p) m_activePearl = null;
                        p.gameObject.SetActive(false);
                    },
                    actionOnDestroy: p => Object.Destroy(p.gameObject),
                    defaultCapacity: 1,
                    maxSize: 1
                );
            }
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            // 로직 초기화 및 갱신
            if (m_logic == null) m_logic = new PearlWeaponLogic(stats);
            else m_logic.UpdateStats(stats);

            // 이미 활성화된 진주가 있다면 상태만 갱신
            if (m_activePearl != null)
            {
                m_activePearl.UpdateState();
                return;
            }

            // 없으면 새로 발사
            if (m_poolManager == null) return;
            var pearl = m_poolManager.Get<PearlProjectile>();
            if (pearl != null)
            {
                pearl.transform.position = owner.position;

                // 랜덤 방향
                if (direction == Vector3.zero) direction = Random.insideUnitCircle.normalized;

                Vector3 velocity = direction.normalized * m_logic.AttackSpeed;
                pearl.Init(m_logic, m_view, velocity, m_poolManager);
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            if (m_logic != null) m_logic.UpdateStats(stats);
            if (m_activePearl != null) m_activePearl.UpdateState();
        }

        #endregion
    }
}