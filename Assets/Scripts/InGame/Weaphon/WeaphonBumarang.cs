using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Weaphon.Base;

namespace InGame.Weaphon
{
    /// <summary>
    /// 부메랑 무기 컨트롤러입니다.
    /// </summary>
    public class WeaphonBumarang : WeaphonBase
    {
        [Header("부메랑 설정")]
        [SerializeField] private BoomerangProjectile m_boomerangPrefab;
        [SerializeField] private Transform m_firePoint;

        [Header("발사체 스탯")]
        [Tooltip("기본 발사 개수")]
        [SerializeField] private int m_baseCount = 1;

        private bool m_isAttacking;

        private new void OnEnable()
        {
            SetWeaphonState(WeaphonState.Idle);
            if (m_firePoint == null) m_firePoint = transform.parent != null ? transform.parent : transform;

            // WeaponPoolManager를 통해 BoomerangProjectile 풀을 등록합니다.
            WeaponPoolManager.Instance.GetOrAddPool<BoomerangProjectile>(
                CreateProjectile,
                OnGetProjectile,
                OnReleaseProjectile,
                OnDestroyProjectile,
                defaultCapacity: 10,
                maxSize: 20
            );
        }

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            if (m_isAttacking) return;
            
            FireBoomerangAsync(attackAngle).Forget();
        }

        private async UniTaskVoid FireBoomerangAsync(Vector3 direction)
        {
            m_isAttacking = true;

            if (direction == Vector3.zero) direction = Vector3.right;

            int count = isEvolved ? m_baseCount + 2 : m_baseCount;
            
            float startAngle = -15f * (count - 1);
            float angleStep = (count > 1) ? 30f : 0f;

            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

            for (int i = 0; i < count; i++)
            {
                float currentAngle = baseAngle + startAngle + (angleStep * i);
                Quaternion rotation = Quaternion.Euler(0, 0, currentAngle);

                // WeaponPoolManager를 통해 투사체를 가져옵니다.
                var projectile = WeaponPoolManager.Instance.Get<BoomerangProjectile>();
                if (projectile == null)
                {
                    Debug.LogWarning("Failed to get BoomerangProjectile from pool.");
                    continue;
                }

                projectile.transform.position = m_firePoint.position;
                projectile.transform.rotation = rotation;

                // attackSpeed를 직접 비행 속도로 사용합니다. 0 이하일 경우 기본값 1f를 사용합니다.
                float finalSpeed = (this.attackSpeed > 0) ? this.attackSpeed : 1f;
                
                projectile.Initialize(m_firePoint, attackPower, mobStunTime, finalSpeed, this.attackRange);

                await UniTask.Delay(50, cancellationToken: this.GetCancellationTokenOnDestroy());
            }

            await UniTask.Delay(System.TimeSpan.FromSeconds(coolTime), cancellationToken: this.GetCancellationTokenOnDestroy());
            
            m_isAttacking = false;
        }

        #region Object Pooling Delegates (WeaponPoolManager에서 사용될 델리게이트)

        private BoomerangProjectile CreateProjectile()
        {
            var obj = Instantiate(m_boomerangPrefab);
            return obj;
        }

        private void OnGetProjectile(BoomerangProjectile obj) => obj.gameObject.SetActive(true);
        private void OnReleaseProjectile(BoomerangProjectile obj) => obj.gameObject.SetActive(false);
        private void OnDestroyProjectile(BoomerangProjectile obj)
        {
            if (obj != null) Destroy(obj.gameObject);
        }
        
        #endregion
    }
}