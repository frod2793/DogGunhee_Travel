using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;
using Vamser_like.Weaphon.Base;

namespace Vamser_like.Weaphon
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
        [Tooltip("부메랑이 날아가는 최대 거리")]
        [SerializeField] private float m_throwDistance = 5f;
        
        [Tooltip("부메랑의 비행 속도")]
        [SerializeField] private float m_flySpeed = 8f;
        
        [Tooltip("기본 발사 개수")]
        [SerializeField] private int m_baseCount = 1;

        private IObjectPool<BoomerangProjectile> m_pool;
        private bool m_isAttacking;

        private void Awake()
        {
            InitializePool();
        }

        private new void OnEnable()
        {
            SetWeaphonState(WeaphonState.Idle);
            if (m_firePoint == null) m_firePoint = transform.parent != null ? transform.parent : transform;
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

                var projectile = m_pool.Get();
                projectile.transform.position = m_firePoint.position;
                projectile.transform.rotation = rotation;

                float finalSpeed = m_flySpeed * (attackSpeed > 0 ? attackSpeed : 1f);
                
                projectile.Initialize(m_pool, m_firePoint, attackPower, mobStunTime, finalSpeed, m_throwDistance);

                await UniTask.Delay(50, cancellationToken: this.GetCancellationTokenOnDestroy());
            }

            await UniTask.Delay(System.TimeSpan.FromSeconds(coolTime), cancellationToken: this.GetCancellationTokenOnDestroy());
            
            m_isAttacking = false;
        }

        #region Object Pooling

        private void InitializePool()
        {
            m_pool = new ObjectPool<BoomerangProjectile>(
                CreateProjectile,
                OnGetProjectile,
                OnReleaseProjectile,
                OnDestroyProjectile,
                defaultCapacity: 10,
                maxSize: 20
            );
        }

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

        private void OnDestroy()
        {
            if (m_pool is System.IDisposable disposable) disposable.Dispose();
        }

        #endregion
    }
}