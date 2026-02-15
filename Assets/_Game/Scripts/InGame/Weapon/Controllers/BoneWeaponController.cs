using System;
using UnityEngine;
using InGame.ObjectPool; 
using InGame.Weapon.Base; 
using InGame.Weapon.Logic; 

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// [설명]: 뼈다귀(Bone) 투사체를 포물선으로 던지는 원거리 무기 컨트롤러입니다.
    /// </summary>
    public class BoneWeaponController : WeaponControllerBase
    {
        #region 내부 변수

        private BoneWeaponLogic m_logic;

        #endregion

        #region 초기화 및 해제

        public override void Init(WeaponDataSO data, Transform owner, WeaponPoolManager poolManager, Func<Vector3> getTargetDirection)
        {
            base.Init(data, owner, poolManager, getTargetDirection);

            // 튜닝 데이터 가져오기
            BoneWeaponTuningData tuningData = new BoneWeaponTuningData();
            if (m_poolManager != null)
            {
                var view = m_poolManager.GetComponent<BoneWeaponView>();
                if (view != null)
                {
                    tuningData.BoneSpeed = view.BoneSpeed;
                }
            }

            // 로직 인스턴스 생성
            m_logic = new BoneWeaponLogic(m_runtimeStats, tuningData);

            // 풀 초기화
            InitializeProjectilePool();
        }

        private void InitializeProjectilePool()
        {
            if (m_runtimeStats.Data.ProjectilePrefab == null)
            {
                Debug.LogError($"[BoneWeaponController] '{WeaponName}'의 ProjectilePrefab이 누락되었습니다.");
                return;
            }

            if (m_poolManager == null) return;

            int initialSize = 10 + (m_runtimeStats.CurrentProjectileCount * 2);

            m_poolManager.GetOrAddPool<BoneBullet>(
                CreateBullet,
                OnGetBullet,
                OnReleaseBullet,
                OnDestroyBullet,
                initialSize,
                100
            );
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        #endregion

        #region 공격 실행 로직

        protected override void ExecuteAttack(Vector3 direction)
        {
            ThrowBone(direction);
        }

        private void ThrowBone(Vector3 direction)
        {
            if (m_poolManager == null || m_logic == null) return;

            BoneBullet bullet = m_poolManager.Get<BoneBullet>();
            if (bullet == null) return;

            bullet.transform.position = m_ownerTransform.position;
            bullet.transform.rotation = Quaternion.identity; 
            bullet.transform.SetParent(null); 
            
            bullet.Init(
                m_logic.AttackPower,
                m_logic.Duration,    
                m_logic.BoneSpeed,
                m_logic.IsEvolved,
                m_poolManager,
                m_soundManager
            );

            Vector3 finalDir = direction == Vector3.zero ? Vector3.up : direction;
            bullet.ThrowBullet(finalDir);
        }

        #endregion

        #region 오브젝트 풀 델리게이트

        private BoneBullet CreateBullet()
        {
            if (m_runtimeStats.Data.ProjectilePrefab == null) return null;
            GameObject go = UnityEngine.Object.Instantiate(m_runtimeStats.Data.ProjectilePrefab);
            return go.GetComponent<BoneBullet>();
        }

        private void OnGetBullet(BoneBullet bullet)
        {
            bullet.gameObject.SetActive(true);
            bullet.ResetState();
        }

        private void OnReleaseBullet(BoneBullet bullet)
        {
            bullet.gameObject.SetActive(false);
            if (m_poolManager != null)
            {
                bullet.transform.SetParent(m_poolManager.transform);
            }
        }

        private void OnDestroyBullet(BoneBullet bullet)
        {
            if (bullet != null) UnityEngine.Object.Destroy(bullet.gameObject);
        }

        #endregion
    }
}