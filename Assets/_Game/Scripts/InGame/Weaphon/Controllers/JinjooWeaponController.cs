using System;
using UnityEngine;
using InGame.ObjectPool;
using InGame.Weaphon.Base;

namespace InGame.Weaphon.Controllers
{
    /// <summary>
    /// 화면 내에서 계속 튕기는 영구적인 진주를 관리하는 POCO 컨트롤러입니다.
    /// </summary>
    public class JinjooWeaponController : WeaponControllerBase
    {
        #region 설정 데이터

        private PearlProjectile m_pearlPrefab;

        #endregion

        #region 내부 상태

        private bool m_currentEvolveState;

        #endregion

        #region 초기화

        /// <summary>
        /// JinjooWeaponController를 초기화합니다.
        /// </summary>
        /// <param name="data">무기 데이터 ScriptableObject</param>
        /// <param name="ownerTransform">소유자(플레이어)의 Transform</param>
        /// <param name="getTargetDirection">공격 방향을 가져오는 델리게이트</param>
        /// <param name="pearlPrefab">진주 프리팹</param>
        public void Init(
            WeaponDataSO data,
            Transform ownerTransform,
            Func<Vector3> getTargetDirection,
            PearlProjectile pearlPrefab)
        {
            base.Init(data, ownerTransform, getTargetDirection);

            m_pearlPrefab = pearlPrefab;
            m_currentEvolveState = m_runtimeStats.IsEvolved;

            // 풀 등록 (최대 1개만 존재)
            RegisterPool();
        }

        private void RegisterPool()
        {
            WeaponPoolManager.Instance.GetOrAddPool<PearlProjectile>(
                CreatePearlProjectile,
                OnGetPearlProjectile,
                OnReleasePearlProjectile,
                OnDestroyPearlProjectile,
                defaultCapacity: 1,
                maxSize: 1
            );
        }

        #endregion

        #region IWeaponController 구현

        public override void OnUpdate(float deltaTime)
        {
            // 진주가 존재하면 상태 동기화
            if (PearlProjectile.Instance != null)
            {
                if (m_currentEvolveState != m_runtimeStats.IsEvolved ||
                    PearlProjectile.Instance.CurrentSpeed != m_runtimeStats.AttackSpeed)
                {
                    m_currentEvolveState = m_runtimeStats.IsEvolved;
                    PearlProjectile.Instance.UpdateState(
                        m_runtimeStats.AttackPower,
                        m_runtimeStats.MobStunTime,
                        m_runtimeStats.AttackSpeed,
                        m_runtimeStats.IsEvolved
                    );
                }
            }
        }

        public override void Attack(Vector3 direction)
        {
            // 이미 활성화된 진주가 있으면 무시
            if (PearlProjectile.Instance != null)
            {
                return;
            }

            LaunchPearl(direction);
        }

        public override void Dispose()
        {
            // 정리 로직 (필요시)
        }

        #endregion

        #region 발사 로직

        private void LaunchPearl(Vector3 direction)
        {
            if (m_pearlPrefab == null)
            {
                LogManager.LogError("JinjooWeaponController: 진주 프리팹이 할당되지 않았습니다.", LogManager.LogCategory.Weapon);
                return;
            }

            if (direction == Vector3.zero) direction = UnityEngine.Random.insideUnitCircle.normalized;

            PearlProjectile pearl = WeaponPoolManager.Instance.Get<PearlProjectile>();
            if (pearl == null)
            {
                LogManager.LogError("JinjooWeaponController: 풀에서 PearlProjectile을 가져오지 못했습니다.", LogManager.LogCategory.Weapon);
                return;
            }

            pearl.transform.SetPositionAndRotation(m_ownerTransform.position, Quaternion.identity);

            float initialSpeed = m_runtimeStats.AttackSpeed > 0 ? m_runtimeStats.AttackSpeed : 1f;

            pearl.Initialize(
                m_runtimeStats.AttackPower,
                m_runtimeStats.MobStunTime,
                m_runtimeStats.AttackSpeed,
                m_runtimeStats.IsEvolved,
                direction.normalized * initialSpeed
            );
        }

        #endregion

        #region 오브젝트 풀 델리게이트

        private PearlProjectile CreatePearlProjectile()
        {
            if (m_pearlPrefab == null)
            {
                LogManager.LogError("JinjooWeaponController: 진주 프리팹이 할당되지 않았습니다!", LogManager.LogCategory.Weapon);
                return null;
            }
            return UnityEngine.Object.Instantiate(m_pearlPrefab);
        }

        private void OnGetPearlProjectile(PearlProjectile pearl) => pearl.gameObject.SetActive(true);
        private void OnReleasePearlProjectile(PearlProjectile pearl) => pearl.gameObject.SetActive(false);
        private void OnDestroyPearlProjectile(PearlProjectile pearl)
        {
            if (pearl != null) UnityEngine.Object.Destroy(pearl.gameObject);
        }

        #endregion
    }
}
