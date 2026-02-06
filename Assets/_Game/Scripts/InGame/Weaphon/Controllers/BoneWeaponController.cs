using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Weaphon.Base;
using InGame.Weaphon;

namespace InGame.Weaphon.Controllers
{
    public class BoneWeaponController : WeaponControllerBase
    {
        private float m_boneSpeed;
        
        public override void Init(WeaponDataSO data, Transform owner, System.Func<Vector3> getTargetDirection)
        {
            base.Init(data, owner, getTargetDirection);
            
            // Bone 전용 초기화: 풀 등록
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

            // 초기 스피드 설정 (AttackSpeed를 투사체 속도로 사용하는지 확인 필요, 기존 코드에서는 attackSpeed를 BulletSpeed로 사용)
            m_boneSpeed = m_runtimeStats.CurrentAttackSpeed > 0 ? m_runtimeStats.CurrentAttackSpeed : 10f;
        }

        public override void Attack(Vector3 direction)
        {
             ThrowBone(direction).Forget();
        }

        private async UniTaskVoid ThrowBone(Vector3 direction)
        {
            try
            {
                // WeaponPoolManager를 통해 총알을 가져옵니다.
                BoneBullet bullet = WeaponPoolManager.Instance.Get<BoneBullet>();
                if (bullet == null) return;

                bullet.transform.position = m_ownerTransform.position;
                bullet.transform.SetParent(null); // 발사 시 부모 해제
                
                // POCO Controller에서 투사체로 데이터 전달
                bullet.Initialize(m_runtimeStats.CurrentAttackPower, m_runtimeStats.CurrentDuration, m_boneSpeed, m_runtimeStats.CurrentLevel >= 6); // 6레벨 진화 가정

                // 발사 방향 결정: 전달받은 방향이 유효하면 사용, 아니면 GetTargetDirection 사용
                Vector3 dir = direction;
                if (dir == Vector3.zero && m_getTargetDirection != null) dir = m_getTargetDirection.Invoke();
                if (dir == Vector3.zero) dir = m_ownerTransform.up; // 안전장치
                
                bullet.ThrowBullet(dir);
            }
            catch (Exception ex)
            {
                 Debug.LogWarning($"[BoneWeaponController] Error: {ex.Message}");
            }
        }
        
        // --- Object Pool Delegates ---
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
    }
}
