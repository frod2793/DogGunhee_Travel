using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Manager;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 플레이어 주변에 영구적으로 지속되는 오라(Aura)형 무기 전략입니다.
    /// </summary>
    /// <typeparam name="TEffect">IAuraEffect를 구현하는 MonoBehaviour</typeparam>
    public class FollowingAuraStrategy<TEffect> : IWeaponStrategy 
        where TEffect : MonoBehaviour, IAuraEffect
    {
        #region 내부 상태 및 변수

        private TEffect m_activeAura;
        private Transform m_owner;

        #endregion

        #region IWeaponStrategy 구현

        public void Init(WeaponDataSO data)
        {
            if (data == null || data.ProjectilePrefab == null)
            {
                Debug.LogError($"[FollowingAuraStrategy] 데이터 또는 프리팹이 유효하지 않습니다. (Weapon: {data?.WeaponName})");
                return;
            }

            var prefab = data.ProjectilePrefab.GetComponent<TEffect>();
            if (prefab == null)
            {
                Debug.LogError($"[FollowingAuraStrategy] 프리팹에 {typeof(TEffect).Name} 컴포넌트가 없습니다.");
                return;
            }

            // 오라 타입은 보통 하나만 유지되므로 풀 사이즈를 최소화
            WeaponPoolManager.Instance.GetOrAddPool<TEffect>(
                () => Object.Instantiate(prefab),
                actionOnGet: (e) => e.gameObject.SetActive(true),
                actionOnRelease: (e) => e.gameObject.SetActive(false),
                actionOnDestroy: (e) => Object.Destroy(e.gameObject),
                maxSize: 5
            );
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            m_owner = owner;

            // 아직 오라가 생성되지 않았다면 풀에서 가져와 소환함
            if (m_activeAura == null || !m_activeAura.gameObject.activeSelf)
            {
                m_activeAura = WeaponPoolManager.Instance.Get<TEffect>();
                
                if (m_activeAura != null)
                {
                    // 플레이어의 회전/스케일에 영향을 받지 않도록 부모 해제
                    m_activeAura.transform.SetParent(null); 
                    m_activeAura.transform.position = owner.position;
                    m_activeAura.transform.rotation = Quaternion.identity;
                    
                    m_activeAura.Init(stats);
                }
            }
            else
            {
                // 이미 존재한다면 스탯 정보만 동기화
                m_activeAura.UpdateStats(stats);
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 매 프레임 플레이어 위치를 동기화함
            if (m_activeAura != null && m_activeAura.gameObject.activeSelf && m_owner != null)
            {
                m_activeAura.transform.position = m_owner.position;
            }
        }

        #endregion
    }
}
