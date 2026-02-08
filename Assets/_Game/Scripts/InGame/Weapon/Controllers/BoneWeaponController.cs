using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using InGame.Weapon.Base;
using InGame.Weapon;

namespace InGame.Weapon.Controllers
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

        protected override void ExecuteAttack(Vector3 direction)
        {
             ThrowBone(direction);
        }

        private void ThrowBone(Vector3 direction)
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

                // 발사 방향 결정: Bone 무기는 유도(Tracking) 없이 정면/입력 방향으로 발사합니다.
                Vector3 dir = direction;
                // [Refactoring] 유저 요청: 적을 추적하지 않고 일정한 사거리/속도로 날아가도록 변경.
                // m_getTargetDirection(자동 조준)을 무시하고, 입력 방향이 없으면 오너의 위쪽(정면)을 사용합니다.
                if (dir == Vector3.zero) dir = m_ownerTransform.up; 
                
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
