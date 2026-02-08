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
        #region 설정 데이터

        private PearlProjectile m_pearlPrefab;
        private PearlWeaponLogic m_logic;
        private PearlWeaponView m_view;

        #endregion

        #region 내부 상태

        private bool m_currentEvolveState;

        #endregion

        #region 초기화

        public override void Init(
            WeaponDataSO data,
            Transform ownerTransform,
            Func<Vector3> getTargetDirection)
        {
            base.Init(data, ownerTransform, getTargetDirection);

            // 데이터로부터 투사체 프리팹 추출
            if (data.ProjectilePrefab != null)
            {
                m_pearlPrefab = data.ProjectilePrefab.GetComponent<PearlProjectile>();
            }

            if (m_pearlPrefab == null)
            {
                LogManager.LogError("JinjooWeaponController: WeaponData.ProjectilePrefab에서 PearlProjectile을 찾을 수 없습니다.", LogManager.LogCategory.Weapon);
                return;
            }
            
            // 1. View 추출 (WeaponPoolManager)
            if (WeaponPoolManager.Instance != null)
            {
                m_view = WeaponPoolManager.Instance.GetComponent<PearlWeaponView>();
            }

            // View가 없으면 경고 후 기본값 생성
            if (m_view == null)
            {
                Debug.LogWarning("[JinjooWeaponController] View not found. Creating default.");
                var go = (WeaponPoolManager.Instance != null) ? WeaponPoolManager.Instance.gameObject : new GameObject("PearlWeaponView_Default");
                m_view = go.GetComponent<PearlWeaponView>() ?? go.AddComponent<PearlWeaponView>();
            }

            // 2. Logic 초기화
            PearlTuningData tuningData = new PearlTuningData
            {
                HitCooldown = m_view.HitCooldown
            };
            m_logic = new PearlWeaponLogic(m_runtimeStats, tuningData);
            
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
            if (m_logic == null) return;

            // 스탯 변경 감지 및 로직 업데이트
            if (m_currentEvolveState != m_runtimeStats.IsEvolved ||
                m_logic.AttackSpeed != m_runtimeStats.AttackSpeed) // 단순 비교
            {
                m_currentEvolveState = m_runtimeStats.IsEvolved;
                m_logic.UpdateStats(m_runtimeStats);
            }

            // 진주가 존재하면 상태 동기화 (투사체가 스스로 UpdateState를 호출하지 않는 구조라면 여기서 호출)
            if (PearlProjectile.Instance != null)
            {
               PearlProjectile.Instance.UpdateState();
            }
        }

        protected override void ExecuteAttack(Vector3 direction)
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
            // 정리 로직
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

            // 초기 속도 계산 (Logic 활용)
            float speed = m_logic.AttackSpeed;
            Vector3 velocity = direction.normalized * speed;

            // 로직과 뷰 주입
            pearl.Initialize(m_logic, m_view, velocity);
        }

        #endregion

        #region 오브젝트 풀 델리게이트

        private PearlProjectile CreatePearlProjectile()
        {
            if (m_pearlPrefab == null) return null;
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
