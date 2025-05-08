using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Pool;


namespace DogGuns_Games.vamsir
{
    public class WeaphonBone : Weaphon_base
    {
        #region 필드 및 변수

        public IObjectPool<BoneBullet> WeaphonBoneObjectPool;
        [SerializeField] private int poolSizeBulletCount = 10;

        [SerializeField] private GameObject bonePrefab;
        [SerializeField] private GameObject bulletParent;

        bool _isAttacking; // 중복 호출 방지 플래그

        #endregion

        #region 초기화 및 오브젝트 풀 관리

        public override void OnEnable()
        {
            base.OnEnable();
            //발사체 오브젝트 풀 설정 
            WeaphonBoneObjectPool = new ObjectPool<BoneBullet>(Create_Bullet,
                OnGet, OnRelease, OnDestory, maxSize: poolSizeBulletCount);

            bulletParent = GameObject.FindWithTag("WeaponPool");
        }

        private BoneBullet Create_Bullet()
        {
            BoneBullet bullet = Instantiate(bonePrefab.gameObject, bulletParent.transform)
                .GetComponent<BoneBullet>();
            bullet.objectPoolSpawner = this;
            return bullet;
        }

        private void OnGet(BoneBullet obj)
        {
            obj.gameObject.SetActive(true);
        }

        private void OnRelease(BoneBullet obj)
        {
            obj.gameObject.SetActive(false);
        }

        private void OnDestory(BoneBullet obj)
        {
            Destroy(obj.gameObject);
        }

        #endregion

        #region 무기 동작 관리

        public override void Weaphon_Idle()
        {
            base.Weaphon_Idle();
        }

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            base.Weaphon_Attack(attackAngle);
            if (!_isAttacking)
            {
                Thorw_Bone(attackAngle).Forget();
            }
        }

        public override void Weaphon_Reload()
        {
            base.Weaphon_Reload();
        }

        #endregion

        #region 유틸리티

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion

        #region 총알 발사

        private async UniTask Thorw_Bone(Vector3 attackAngle)
        {
            _isAttacking = true;
            BoneBullet bullet = WeaphonBoneObjectPool.Get();
            bullet.transform.position = transform.position;
            bullet.transform.rotation = Quaternion.Euler(attackAngle);
            bullet.bulletSpeed = attackSpeed;
            bullet.Thow_Bullet(attackAngle);
            await UniTask.Delay(TimeSpan.FromSeconds(coolTime));
            _isAttacking = false;
        }

        #endregion
    }
}