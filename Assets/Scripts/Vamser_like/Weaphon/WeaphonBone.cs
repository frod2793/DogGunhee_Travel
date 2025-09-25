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
                OnGet, OnRelease, OnDestroyPoolItem, maxSize: poolSizeBulletCount);

            bulletParent = GameObject.FindWithTag("WeaponPool");
        }

        private BoneBullet Create_Bullet()
        {
            // 부모 객체 확인 및 fallback 처리
            Transform parent = bulletParent != null ? bulletParent.transform : transform;
    
            // 총알 생성 최적화
            BoneBullet bullet = Instantiate(bonePrefab, parent)
                .GetComponent<BoneBullet>();
    
            // 총알 초기 설정
            bullet.bulletSpeed = attackSpeed;
            bullet.objectPoolSpawner = this;
    
            // 총알 이름 설정으로 디버깅 용이성 향상
            bullet.gameObject.name = $"Bone_Bullet_{Guid.NewGuid().ToString().Substring(0, 8)}";
    
            // 초기 상태는 비활성화
            bullet.gameObject.SetActive(false);
    
            return bullet;
        }

        private void OnGet(BoneBullet obj)
        {
            if (obj == null) return;
    
            // 총알 상태 초기화
            obj.ResetState();
            obj.gameObject.SetActive(true);
          
        }

        private void OnRelease(BoneBullet obj)
        {
            if (obj == null) return;
    
            // DOTween 애니메이션 정리
            DOTween.Kill(obj.transform);
    
            // 위치 초기화 (선택적)
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
    
            // 비활성화
            obj.gameObject.SetActive(false);
        }

        // 메서드명을 변경하여 Unity 라이프사이클 메서드와 충돌 방지
        private void OnDestroyPoolItem(BoneBullet obj) 
        {
            if (obj == null) return;

            // 리소스 정리
            DOTween.Kill(obj.transform);
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
            Throw_Bone(attackAngle).Forget();
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

        private async UniTask Throw_Bone(Vector3 attackAngle)
        {
            // 이미 공격 중이면 무시
            if (_isAttacking) return;
    
            _isAttacking = true;
    
            try
            {
                BoneBullet bullet = WeaphonBoneObjectPool.Get();
                bullet.transform.position = transform.position;
                bullet.transform.rotation = Quaternion.Euler(attackAngle);
                bullet.Throw_Bullet(attackAngle);
        
                await UniTask.Delay(TimeSpan.FromSeconds(coolTime), 
                    cancellationToken: this.GetCancellationTokenOnDestroy());
                
              
            }
            catch (Exception ex)
            {
                Debug.LogError($"뼈 발사 중 오류 발생: {ex.Message}");
            }
            finally
            {
                _isAttacking = false;
            }
        }

        #endregion
    }
}