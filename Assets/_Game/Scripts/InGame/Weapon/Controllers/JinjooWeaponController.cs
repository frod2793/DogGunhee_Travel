using System;
using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon.Logic;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 화면 내에서 계속 튕기는 영구적인 진주를 관리하는 컨트롤러입니다.
    /// POCO Logic과 View Tuning 아키텍처가 적용되었습니다.
    /// </summary>
    public class JinjooWeaponController : WeaponControllerBase
    {
        #region 내부 상태 및 변수

        private PearlProjectile m_pearlPrefab;
        private PearlWeaponLogic m_logic;
        private PearlWeaponView m_view;

        private bool m_currentEvolveState;

        #endregion

        #region 초기화 및 해제

        /// <summary>
        /// 무기를 초기화하고 진주 투사체 및 풀을 설정합니다.
        /// </summary>
        public override void Init(WeaponDataSO data, Transform ownerTransform, Func<Vector3> getTargetDirection)
        {
            base.Init(data, ownerTransform, getTargetDirection);

            // 1. 투사체 프리팹 추출
            if (data.ProjectilePrefab != null)
            {
                m_pearlPrefab = data.ProjectilePrefab.GetComponent<PearlProjectile>();
            }

            if (m_pearlPrefab == null)
            {
                LogManager.LogError("JinjooWeaponController: WeaponData.ProjectilePrefab에서 PearlProjectile을 찾을 수 없습니다.", LogManager.LogCategory.Weapon);
                return;
            }
            
            // 2. View 추출 (WeaponPoolManager에서 보정값 참조)
            if (WeaponPoolManager.Instance != null)
            {
                m_view = WeaponPoolManager.Instance.GetComponent<PearlWeaponView>();
            }

            if (m_view == null)
            {
                Debug.LogWarning("[JinjooWeaponController] View not found. Creating default.");
                var go = (WeaponPoolManager.Instance != null) ? WeaponPoolManager.Instance.gameObject : new GameObject("PearlWeaponView_Default");
                m_view = go.GetComponent<PearlWeaponView>() ?? go.AddComponent<PearlWeaponView>();
            }

            // 3. POCO Logic 초기화
            PearlTuningData tuningData = new PearlTuningData
            {
                HitCooldown = m_view.HitCooldown
            };
            m_logic = new PearlWeaponLogic(m_runtimeStats, tuningData);
            
            m_currentEvolveState = m_runtimeStats.IsEvolved;

            // 4. 오브젝트 풀 등록 (진주는 화면에 1개만 존재하도록 설정)
            RegisterPool();
        }

        /// <summary>
        /// 진주 투사체를 위한 오브젝트 풀을 등록합니다.
        /// </summary>
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

        /// <summary>
        /// 무기 해제 시 호출됩니다.
        /// </summary>
        public override void Dispose()
        {
            // 전역 풀 및 정적 인스턴스를 사용하므로 별도 해제 불필요
        }

        #endregion

        #region 업데이트 및 실행 인터페이스

        public override void OnUpdate(float deltaTime)
        {
            if (m_logic == null)
            {
                return;
            }

            // 실시간 스탯 변화 감지 및 로직 업데이트
            if (m_currentEvolveState != m_runtimeStats.IsEvolved || m_logic.AttackSpeed != m_runtimeStats.AttackSpeed)
            {
                m_currentEvolveState = m_runtimeStats.IsEvolved;
                m_logic.UpdateStats(m_runtimeStats);
            }

            // 현재 활성화된 진주가 있다면 상태 갱신
            if (PearlProjectile.Instance != null)
            {
               PearlProjectile.Instance.UpdateState();
            }
        }

        protected override void ExecuteAttack(Vector3 direction)
        {
            // 화면에 진주가 이미 존재하는 경우 중복 발사 방지
            if (PearlProjectile.Instance != null)
            {
                return;
            }

            LaunchPearl(direction);
        }

        #endregion

        #region 발사 로직

        /// <summary>
        /// 진주를 생성하고 초기 속도를 부여하여 발사합니다.
        /// </summary>
        private void LaunchPearl(Vector3 direction)
        {
            if (m_pearlPrefab == null)
            {
                return;
            }

            if (direction == Vector3.zero)
            {
                direction = UnityEngine.Random.insideUnitCircle.normalized;
            }

            PearlProjectile pearl = WeaponPoolManager.Instance.Get<PearlProjectile>();
            if (pearl == null)
            {
                return;
            }

            pearl.transform.SetPositionAndRotation(m_ownerTransform.position, Quaternion.identity);

            // 초기 속도 계산 및 로직 주입
            float speed = m_logic.AttackSpeed;
            Vector3 velocity = direction.normalized * speed;

            // 투사체 초기화 (Initialize -> Init)
            pearl.Init(m_logic, m_view, velocity);
        }

        #endregion

        #region 오브젝트 풀 관리 델리게이트

        private PearlProjectile CreatePearlProjectile()
        {
            if (m_pearlPrefab == null)
            {
                return null;
            }
            return UnityEngine.Object.Instantiate(m_pearlPrefab);
        }

        private void OnGetPearlProjectile(PearlProjectile pearl)
        {
            pearl.gameObject.SetActive(true);
        }

        private void OnReleasePearlProjectile(PearlProjectile pearl)
        {
            pearl.gameObject.SetActive(false);
        }

        private void OnDestroyPearlProjectile(PearlProjectile pearl)
        {
            if (pearl != null)
            {
                UnityEngine.Object.Destroy(pearl.gameObject);
            }
        }

        #endregion
    }
}
