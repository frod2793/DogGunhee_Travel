using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 부메랑 무기 컨트롤러입니다.
    /// 레벨 2가 되면 부메랑 개수가 늘어납니다.
    /// </summary>
    public class WeaponBoomerang : Weaphon_base
    {
        [Header("부메랑 설정")]
        [SerializeField] private BoomerangProjectile m_boomerangPrefab;
        [SerializeField] private Transform m_firePoint; // 발사 위치 (보통 플레이어)

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

        protected override void OnEnable()
        {
            base.OnEnable();
            // 플레이어 위치 참조 (없으면 부모)
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

            // 방향이 없으면 기본 오른쪽
            if (direction == Vector3.zero) direction = Vector3.right;

            // 레벨업 시 발사 개수 증가 (예: +2개)
            int count = isUpgradelv2 ? m_baseCount + 2 : m_baseCount;
            
            // 부채꼴 발사 각도 계산
            float startAngle = -15f * (count - 1); // 간격 30도 기준
            float angleStep = (count > 1) ? 30f : 0f;

            // 발사 기준 각도 (입력 방향)
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f; // Sprite Up 기준

            for (int i = 0; i < count; i++)
            {
                float currentAngle = baseAngle + startAngle + (angleStep * i);
                Quaternion rotation = Quaternion.Euler(0, 0, currentAngle);

                var projectile = m_pool.Get();
                projectile.transform.position = m_firePoint.position;
                projectile.transform.rotation = rotation;

                // 속도 및 거리 등 스탯 적용
                // attackSpeed가 높을수록 투사체 속도 증가
                float finalSpeed = m_flySpeed * (attackSpeed > 0 ? attackSpeed : 1f);
                
                projectile.Initialize(m_pool, m_firePoint, attackPower, mobStunTime, finalSpeed, m_throwDistance);

                // 연사 간격 (약간의 텀을 두고 발사)
                await UniTask.Delay(50, cancellationToken: this.GetCancellationTokenOnDestroy());
            }

            // 쿨타임 대기
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
            // IDisposable 패턴으로 풀 정리
            if (m_pool is System.IDisposable disposable) disposable.Dispose();
        }

        #endregion
    }
}