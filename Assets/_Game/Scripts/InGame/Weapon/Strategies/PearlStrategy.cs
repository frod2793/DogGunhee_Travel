using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon.Logic;
using InGame.Weapon.Controllers;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 싸구려 진주 무기(Pearl)의 도비적인 공격 전략을 담당하는 클래스입니다.
    /// </summary>
    public class PearlStrategy : IWeaponStrategy
    {
        #region 내부 상태 및 변수

        private PearlWeaponLogic m_logic;
        private PearlWeaponView m_view;

        #endregion

        #region IWeaponStrategy 구현

        public void Init(WeaponDataSO data)
        {
            // 1. 전역 설정 컴포넌트(View) 추출
            if (WeaponPoolManager.Instance != null)
            {
                m_view = WeaponPoolManager.Instance.GetComponent<PearlWeaponView>();
            }

            if (m_view == null)
            {
                Debug.LogWarning("[PearlStrategy] PearlWeaponView가 없습니다. 기본 설정을 사용합니다.");
                var go = (WeaponPoolManager.Instance != null) ? WeaponPoolManager.Instance.gameObject : new GameObject("PearlWeaponView_Default");
                m_view = go.GetComponent<PearlWeaponView>() ?? go.AddComponent<PearlWeaponView>();
            }

            // 2. 단일 투사체(Pearl) 풀 등록
            if (data.ProjectilePrefab != null)
            {
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
            // 비즈니스 로직 초기화/동기화
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

            // 진주 투사체가 이미 존재하면 스탯만 갱신
            if (PearlProjectile.Instance != null)
            {
                PearlProjectile.Instance.UpdateState(); 
                return;
            }

            // 존재하지 않으면 신규 발사
            var pearl = WeaponPoolManager.Instance.Get<PearlProjectile>();
            if (pearl != null)
            {
                pearl.transform.position = owner.position;
                
                if (direction == Vector3.zero) 
                {
                    direction = Random.insideUnitCircle.normalized;
                }

                float speed = m_logic.AttackSpeed;
                Vector3 velocity = direction.normalized * speed;

                pearl.Init(m_logic, m_view, velocity);
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            if (m_logic != null)
            {
                m_logic.UpdateStats(stats);
            }

            // 투사체의 실시간 상태(데이터) 동기화
            if (PearlProjectile.Instance != null)
            {
                PearlProjectile.Instance.UpdateState();
            }
        }

        #endregion
    }
}
