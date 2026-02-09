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
        #region 내부 변수

        private TEffect m_activeAura;

        private Transform m_owner;

        #endregion

        public void Initialize(WeaponDataSO data)
        {
            if (data == null || data.ProjectilePrefab == null)
            {
                LogManager.LogError($"[FollowingAuraStrategy] 데이터 또는 프리팹이 유효하지 않습니다. (Weapon: {data?.WeaponName})");
                return;
            }

            var prefab = data.ProjectilePrefab.GetComponent<TEffect>();
            if (prefab == null)
            {
                LogManager.LogError($"[FollowingAuraStrategy] 프리팹에 {typeof(TEffect).Name} 컴포넌트가 없습니다.");
                return;
            }

            // 오라 타입은 보통 1개만 존재하므로 풀 사이즈를 작게 설정
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

            // 아직 오라가 없다면 생성 (최초 1회 또는 재생성)
            if (m_activeAura == null || !m_activeAura.gameObject.activeSelf)
            {
                m_activeAura = WeaponPoolManager.Instance.Get<TEffect>();
                
                if (m_activeAura != null)
                {
                    // 부모를 해제하여 플레이어 회전/스케일 영향을 받지 않도록 함
                    m_activeAura.transform.SetParent(null); 
                    m_activeAura.transform.position = owner.position;
                    // 회전 초기화 (필요시)
                    m_activeAura.transform.rotation = Quaternion.identity;
                    
                    m_activeAura.Initialize(stats);
                }
            }
            else
            {
                // 이미 존재한다면 스탯 업데이트 (레벨업 등 대응)
                m_activeAura.UpdateStats(stats);
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 매 프레임 플레이어 위치를 따라감 (회전/스케일 무시)
            if (m_activeAura != null && m_activeAura.gameObject.activeSelf && m_owner != null)
            {
                m_activeAura.transform.position = m_owner.position;
            }
        }
    }
}
