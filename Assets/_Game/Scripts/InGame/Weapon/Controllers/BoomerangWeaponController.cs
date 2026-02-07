using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Weapon.Base;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 부메랑 무기의 발사 로직을 담당하는 POCO 컨트롤러입니다.
    /// </summary>
    public class BoomerangWeaponController : WeaponControllerBase
    {
        #region 설정 데이터

        private BoomerangProjectile m_boomerangPrefab;
        private int m_baseCount;
        private int m_poolDefaultCapacity;
        private int m_poolMaxSize;

        #endregion

        #region 내부 상태

        private bool m_isAttacking;
        private CancellationTokenSource m_attackCts;

        #endregion

        #region 초기화

        /// <summary>
        /// BoomerangWeaponController를 초기화합니다.
        /// </summary>
        /// <param name="data">무기 데이터 ScriptableObject</param>
        /// <param name="ownerTransform">소유자(플레이어)의 Transform</param>
        /// <param name="getTargetDirection">공격 방향을 가져오는 델리게이트</param>
        /// <param name="boomerangPrefab">부메랑 프리팹</param>
        /// <param name="baseCount">기본 발사 개수</param>
        /// <param name="poolDefaultCapacity">풀 기본 용량</param>
        /// <param name="poolMaxSize">풀 최대 크기</param>
        public void Init(
            WeaponDataSO data,
            Transform ownerTransform,
            Func<Vector3> getTargetDirection,
            BoomerangProjectile boomerangPrefab,
            int baseCount = 1,
            int poolDefaultCapacity = 10,
            int poolMaxSize = 20)
        {
            base.Init(data, ownerTransform, getTargetDirection);

            m_boomerangPrefab = boomerangPrefab;
            m_baseCount = baseCount;
            m_poolDefaultCapacity = poolDefaultCapacity;
            m_poolMaxSize = poolMaxSize;

            m_isAttacking = false;

            // 풀 등록
            RegisterPool();
        }

        private void RegisterPool()
        {
            WeaponPoolManager.Instance.GetOrAddPool<BoomerangProjectile>(
                CreateProjectile,
                OnGetProjectile,
                OnReleaseProjectile,
                OnDestroyProjectile,
                defaultCapacity: m_poolDefaultCapacity,
                maxSize: m_poolMaxSize
            );
        }

        #endregion

        #region IWeaponController 구현

        public override void OnUpdate(float deltaTime)
        {
            // 부메랑은 쿨타임 기반 자동 공격이 아니므로 Update에서 처리할 로직 없음
        }

        protected override void ExecuteAttack(Vector3 direction)
        {
            if (m_isAttacking) return;

            m_attackCts?.Cancel();
            m_attackCts = new CancellationTokenSource();
            FireBoomerangAsync(direction, m_attackCts.Token).Forget();
        }

        public override void Dispose()
        {
            m_attackCts?.Cancel();
            m_attackCts?.Dispose();
            m_attackCts = null;
        }

        #endregion

        #region 발사 로직

        private async UniTaskVoid FireBoomerangAsync(Vector3 direction, CancellationToken token)
        {
            m_isAttacking = true;

            if (direction == Vector3.zero) direction = Vector3.right;

            // 진화 시 추가 투사체
            int count = m_runtimeStats.IsEvolved ? m_baseCount + 2 : m_baseCount;

            float startAngle = -15f * (count - 1);
            float angleStep = (count > 1) ? 30f : 0f;

            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

            try
            {
                for (int i = 0; i < count; i++)
                {
                    float currentAngle = baseAngle + startAngle + (angleStep * i);
                    Quaternion rotation = Quaternion.Euler(0, 0, currentAngle);

                    var projectile = WeaponPoolManager.Instance.Get<BoomerangProjectile>();
                    if (projectile == null)
                    {
                        LogManager.LogWarning("BoomerangWeaponController: 풀에서 BoomerangProjectile을 가져오지 못했습니다.", LogManager.LogCategory.Weapon);
                        continue;
                    }

                    projectile.transform.position = m_ownerTransform.position;
                    projectile.transform.rotation = rotation;

                    float finalSpeed = m_runtimeStats.AttackSpeed > 0 ? m_runtimeStats.AttackSpeed : 1f;

                    projectile.Initialize(
                        m_ownerTransform,
                        m_runtimeStats.AttackPower,
                        m_runtimeStats.MobStunTime,
                        finalSpeed,
                        m_runtimeStats.AttackRange
                    );

                    await UniTask.Delay(50, cancellationToken: token);
                }

                // 쿨타임 대기
                await UniTask.Delay(TimeSpan.FromSeconds(m_runtimeStats.CoolTime), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                // 취소됨 - 정상 동작
            }
            finally
            {
                m_isAttacking = false;
            }
        }

        #endregion

        #region 오브젝트 풀 델리게이트

        private BoomerangProjectile CreateProjectile()
        {
            if (m_boomerangPrefab == null)
            {
                LogManager.LogError("BoomerangWeaponController: 프리팹이 할당되지 않았습니다!", LogManager.LogCategory.Weapon);
                return null;
            }
            return UnityEngine.Object.Instantiate(m_boomerangPrefab);
        }

        private void OnGetProjectile(BoomerangProjectile obj) => obj.gameObject.SetActive(true);
        private void OnReleaseProjectile(BoomerangProjectile obj) => obj.gameObject.SetActive(false);
        private void OnDestroyProjectile(BoomerangProjectile obj)
        {
            if (obj != null) UnityEngine.Object.Destroy(obj.gameObject);
        }

        #endregion
    }
}
