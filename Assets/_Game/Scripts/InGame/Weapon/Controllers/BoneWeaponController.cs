using System;
using UnityEngine;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon.Logic;
using InGame.Manager;

namespace InGame.Weapon.Controllers
{
    public class BoneWeaponController : WeaponControllerBase
    {
        #region 내부 상태

        private BoneWeaponLogic m_logic;

        #endregion

        #region 초기화
        
        public override void Init(WeaponDataSO data, Transform owner, Func<Vector3> getTargetDirection)
        {
            base.Init(data, owner, getTargetDirection);
            
            // 1. 비주얼 튜닝 데이터 추출 (WeaponPoolManager)
            BoneWeaponTuningData? tuningData = null;
            if (WeaponPoolManager.Instance != null)
            {
                var view = WeaponPoolManager.Instance.GetComponent<BoneWeaponView>();
                if (view != null)
                {
                    tuningData = new BoneWeaponTuningData { BoneSpeed = view.BoneSpeed };
                }
            }

            // 2. POCO 로직 초기화
            m_logic = new BoneWeaponLogic(m_runtimeStats, tuningData);

            // 3. 오브젝트 풀 등록
            if (m_runtimeStats.Data.ProjectilePrefab != null)
            {
                WeaponPoolManager.Instance.GetOrAddPool<BoneBullet>(
                    CreateBullet, OnGet, OnRelease, OnDestroyPoolItem,
                    maxSize: 10 + m_runtimeStats.CurrentProjectileCount * 2
                );
            }
        }

        #endregion

        #region IWeaponController 구현

        protected override void ExecuteAttack(Vector3 direction)
        {
             ThrowBone(direction);
        }

        public override void Dispose()
        {
            // BoneWeaponController는 특별한 해제 로직 없음
        }

        #endregion

        #region 공격 로직

        private void ThrowBone(Vector3 direction)
        {
            // 풀에서 투사체 가져오기
            BoneBullet bullet = WeaponPoolManager.Instance.Get<BoneBullet>();
            if (bullet == null) return;

            bullet.transform.position = m_ownerTransform.position;
            bullet.transform.SetParent(null);
            
            // 로직 데이터로 초기화
            bullet.Initialize(
                m_logic.AttackPower, 
                m_logic.Duration, 
                m_logic.BoneSpeed, 
                m_logic.IsEvolved);

            // 발사 방향 설정 (기본값: 위쪽)
            Vector3 dir = direction == Vector3.zero ? m_ownerTransform.up : direction; 
            bullet.ThrowBullet(dir);
        }
        
        #endregion

        #region 오브젝트 풀 델리게이트

        private BoneBullet CreateBullet()
        {
            var go = UnityEngine.Object.Instantiate(m_runtimeStats.Data.ProjectilePrefab);
            return go.GetComponent<BoneBullet>();
        }

        private void OnGet(BoneBullet obj) 
        {
            obj.gameObject.SetActive(true);
            obj.ResetState();
        }

        private void OnRelease(BoneBullet obj) 
        {
            obj.gameObject.SetActive(false);
            obj.transform.SetParent(null);
        }

        private void OnDestroyPoolItem(BoneBullet obj) => UnityEngine.Object.Destroy(obj.gameObject);

        #endregion
    }
}
