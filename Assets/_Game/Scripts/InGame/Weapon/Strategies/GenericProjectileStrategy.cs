using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 개별 투사체(Projectile)의 초기화 인터페이스입니다.
    /// </summary>
    public interface IProjectile
    {
        void Init(Vector3 direction, float damage, float speed, float duration, bool isEvolved);
    }

    /// <summary>
    /// 제네릭을 사용하여 범용적인 투사체 타입을 발사하는 전략입니다.
    /// </summary>
    /// <typeparam name="TProjectile">MonoBehaviour 상속 및 IProjectile 구현 클래스</typeparam>
    public class GenericProjectileStrategy<TProjectile> : IWeaponStrategy 
        where TProjectile : MonoBehaviour, IProjectile
    {
        #region 내부 상태 및 변수

        private WeaponDataSO m_data;

        #endregion

        #region IWeaponStrategy 구현

        public void Init(WeaponDataSO data)
        {
            m_data = data;
            
            // 프리팹 기반 오브젝트 풀 자동 등록
            if (data.ProjectilePrefab != null)
            {
                WeaponPoolManager.Instance.GetOrAddPool<TProjectile>(
                    () => Object.Instantiate(data.ProjectilePrefab).GetComponent<TProjectile>(),
                    OnGet,
                    OnRelease,
                    OnDestroy,
                    maxSize: 20
                );
            }
        }

        public void Attack(WeaponRuntimeStats stats, Transform owner, Vector3 direction)
        {
            int count = stats.CurrentProjectileCount;

            for (int i = 0; i < count; i++)
            {
                var projectile = WeaponPoolManager.Instance.Get<TProjectile>();
                if (projectile != null)
                {
                    projectile.transform.position = owner.position;
                    
                    // 투사체 초기화 (공격력, 속도, 지속시간, 진화여부)
                    projectile.Init(
                        direction, 
                        stats.CurrentAttackPower, 
                        stats.CurrentAttackSpeed, 
                        stats.CurrentDuration,
                        stats.IsEvolved
                    );
                }
            }
        }

        public void OnUpdate(WeaponRuntimeStats stats, float deltaTime)
        {
            // 범용 투사체 전략은 별도의 프레임 업데이트가 필요 없음
        }

        #endregion

        #region 오브젝트 풀 이벤트

        private void OnGet(TProjectile p) => p.gameObject.SetActive(true);
        private void OnRelease(TProjectile p) => p.gameObject.SetActive(false);
        private void OnDestroy(TProjectile p) => Object.Destroy(p.gameObject);

        #endregion
    }
}
