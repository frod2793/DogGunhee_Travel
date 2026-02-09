using System;
using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon.Logic;
using InGame.Manager;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 뼈다귀 투사체를 던지는 원거리 무기 컨트롤러입니다.
    /// </summary>
    public class BoneWeaponController : WeaponControllerBase
    {
        #region 내부 상태 및 변수

        /// <summary>
        /// 뼈다귀 무기의 핵심 비즈니스 로직
        /// </summary>
        private BoneWeaponLogic m_logic;

        #endregion

        #region 초기화 및 해제

        /// <summary>
        /// 무기를 초기화하고 필요한 리소스(로직, 오브젝트 풀)를 설정합니다.
        /// </summary>
        public override void Init(WeaponDataSO data, Transform owner, Func<Vector3> getTargetDirection)
        {
            base.Init(data, owner, getTargetDirection);

            // 1. 비주얼 튜닝 데이터 추출 (WeaponPoolManager에서 View 참조)
            BoneWeaponTuningData? tuningData = null;
            if (WeaponPoolManager.Instance != null)
            {
                var view = WeaponPoolManager.Instance.GetComponent<BoneWeaponView>();
                if (view != null)
                {
                    tuningData = new BoneWeaponTuningData 
                    { 
                        BoneSpeed = view.BoneSpeed 
                    };
                }
            }

            // 2. POCO 로직 인스턴스 생성
            m_logic = new BoneWeaponLogic(m_runtimeStats, tuningData);

            // 3. 오브젝트 풀 등록 (BoneBullet)
            if (m_runtimeStats.Data.ProjectilePrefab != null)
            {
                WeaponPoolManager.Instance.GetOrAddPool<BoneBullet>(
                    CreateBullet, 
                    OnGet, 
                    OnRelease, 
                    OnDestroyPoolItem,
                    maxSize: 10 + m_runtimeStats.CurrentProjectileCount * 2
                );
            }
        }

        /// <summary>
        /// 무기 사용 중단 시 호출됩니다.
        /// </summary>
        public override void Dispose()
        {
            // 전역 풀을 사용하므로 별도의 정리는 필요 없음
        }

        #endregion

        #region 공격 실행 로직

        /// <summary>
        /// 추상 메서드를 통해 실제 공격 행위를 수행합니다.
        /// </summary>
        protected override void ExecuteAttack(Vector3 direction)
        {
            ThrowBone(direction);
        }

        /// <summary>
        /// 뼈다귀 투사체를 생성하고 발사합니다.
        /// </summary>
        private void ThrowBone(Vector3 direction)
        {
            // 오브젝트 풀에서 투사체 획득
            BoneBullet bullet = WeaponPoolManager.Instance.Get<BoneBullet>();
            if (bullet == null)
            {
                return;
            }

            bullet.transform.position = m_ownerTransform.position;
            bullet.transform.SetParent(null);

            // 투사체 데이터 초기화 (Initialize -> Init 변경 필요 시 진행)
            bullet.Init(
                m_logic.AttackPower,
                m_logic.Duration,
                m_logic.BoneSpeed,
                m_logic.IsEvolved
            );

            // 발사 방향 결정
            Vector3 dir = direction == Vector3.zero ? m_ownerTransform.up : direction;
            bullet.ThrowBullet(dir);
        }

        #endregion

        #region 오브젝트 풀 관리 델리게이트

        /// <summary>
        /// 새로운 뼈다귀 투사체를 생성합니다.
        /// </summary>
        private BoneBullet CreateBullet()
        {
            var go = UnityEngine.Object.Instantiate(m_runtimeStats.Data.ProjectilePrefab);
            return go.GetComponent<BoneBullet>();
        }

        /// <summary>
        /// 풀에서 활성화될 때 호출되는 콜백입니다.
        /// </summary>
        private void OnGet(BoneBullet obj)
        {
            obj.gameObject.SetActive(true);
            obj.ResetState();
        }

        /// <summary>
        /// 풀로 반환될 때 호출되는 콜백입니다.
        /// </summary>
        private void OnRelease(BoneBullet obj)
        {
            obj.gameObject.SetActive(false);
            obj.transform.SetParent(null);
        }

        /// <summary>
        /// 풀 아이템 파괴 시 호출됩니다.
        /// </summary>
        private void OnDestroyPoolItem(BoneBullet obj)
        {
            UnityEngine.Object.Destroy(obj.gameObject);
        }

        #endregion
    }
}
