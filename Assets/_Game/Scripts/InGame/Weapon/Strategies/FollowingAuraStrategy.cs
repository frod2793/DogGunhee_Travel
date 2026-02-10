using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 플레이어 주변을 따라다니는 오라(Aura) 형태의 무기 전략입니다.
    /// <br/> 초기 생성 후 매 프레임 위치를 동기화하고 스탯을 갱신합니다.
    /// </summary>
    /// <typeparam name="TEffect">IAuraEffect를 구현하는 MonoBehaviour</typeparam>
    public class FollowingAuraStrategy<TEffect> : IWeaponStrategy
        where TEffect : MonoBehaviour, IAuraEffect
    {
        #region 1. 내부 변수 (Internal State)

        private TEffect m_activeAura;
        private WeaponPoolManager m_poolManager;
        private Transform m_owner;

        #endregion

        #region 2. 인터페이스 구현 (IWeaponStrategy Implementation)

        public void Init(WeaponDataSO data, WeaponPoolManager poolManager)
        {
            m_poolManager = poolManager;
            if (data == null || data.ProjectilePrefab == null || m_poolManager == null)
            {
                Debug.LogError("[FollowingAuraStrategy] 데이터 또는 프리팹 누락");
                return;
            }

            var prefab = data.ProjectilePrefab.GetComponent<TEffect>();
            if (prefab == null)
            {
                Debug.LogError($"[FollowingAuraStrategy] 프리팹에 {typeof(TEffect).Name} 컴포넌트가 없습니다.");
                return;
            }

            // 오라는 보통 1개만 유지되므로 풀 사이즈를 작게 설정
            m_poolManager.GetOrAddPool<TEffect>(
                createFunc: () => Object.Instantiate(prefab),
                actionOnGet: (e) => e.gameObject.SetActive(true),
                actionOnRelease: (e) => e.gameObject.SetActive(false),
                actionOnDestroy: (e) => Object.Destroy(e.gameObject),
                maxSize: 5
            );
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            m_owner = owner;

            // 오라가 없거나 비활성화 상태면 새로 생성
            if (m_activeAura == null || !m_activeAura.gameObject.activeSelf)
            {
                if (m_poolManager == null) return;
                m_activeAura = m_poolManager.Get<TEffect>();

                if (m_activeAura != null)
                {
                    // 부모 종속성 해제 (스케일 왜곡 방지) 후 위치 동기화
                    m_activeAura.transform.SetParent(null);
                    m_activeAura.transform.position = owner.position;
                    m_activeAura.transform.rotation = Quaternion.identity;

                    m_activeAura.Init(stats, m_poolManager);
                }
            }
            else
            {
                // 이미 존재하면 스탯만 업데이트 (레벨업 반영 등)
                m_activeAura.UpdateStats(stats);
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 플레이어 위치 추적
            if (m_activeAura != null && m_activeAura.gameObject.activeSelf && m_owner != null)
            {
                m_activeAura.transform.position = m_owner.position;
            }
        }

        #endregion
    }
}