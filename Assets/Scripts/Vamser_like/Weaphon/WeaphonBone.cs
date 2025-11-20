using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;


namespace DogGuns_Games.vamsir
{
    public class WeaphonBone : Weaphon_base
    {
        #region 필드 및 변수

        [Header("오브젝트 풀 설정")]
        [Tooltip("생성할 총알의 최대 개수입니다.")]
        
        [SerializeField] private int m_poolSizeBulletCount = 10;

        [Header("프리팹 및 부모 설정")]
        [Tooltip("복제하여 사용할 총알 프리팹입니다.")]
        [SerializeField] private GameObject m_bonePrefab;
        [Tooltip("생성된 총알들이 위치할 부모 오브젝트입니다. 지정하지 않으면 이 오브젝트의 자식으로 생성됩니다.")]
        
        [SerializeField] private Transform m_bulletParent;
        public IObjectPool<BoneBullet> WeaphonBoneObjectPool { get; private set; }
        private bool m_isAttacking; // 중복 호출 방지 플래그

        #endregion

        #region 초기화 및 오브젝트 풀 관리

        protected override void OnEnable()
        {
            
            base.OnEnable();
            
            // bulletParent가 할당되지 않았다면, 안전을 위해 현재 트랜스폼을 부모로 사용합니다.
            if (m_bulletParent == null)
            {
                m_bulletParent = transform;
            }
            
            //발사체 오브젝트 풀 설정 
            WeaphonBoneObjectPool = new ObjectPool<BoneBullet>(CreateBullet,
                OnGet, OnRelease, OnDestroyPoolItem, maxSize: m_poolSizeBulletCount);
        }

        private BoneBullet CreateBullet()
        {
            // 총알 생성 최적화
            // Instantiate 시 부모를 함께 지정하여 불필요한 월드 좌표 변환을 방지합니다.
            GameObject bulletObject = Instantiate(m_bonePrefab, m_bulletParent);
            
            BoneBullet bullet = bulletObject.GetComponent<BoneBullet>();

            // 총알 초기 설정
            bullet.ObjectPoolSpawner = this;

            // 총알 이름 설정으로 디버깅 용이성 향상
            bullet.gameObject.name = $"{m_bonePrefab.name}_{Guid.NewGuid().ToString().Substring(0, 4)}";

            // 초기 상태는 비활성화
            bulletObject.SetActive(false);

            return bullet;
        }

        private void OnGet(BoneBullet obj)
        {
            if (obj == null) return;

            // 총알 상태 초기화
            obj.ResetState();
            // 풀에서 나올 때마다 부모 무기의 최신 스탯으로 갱신합니다.
            obj.Initialize(this);
            obj.gameObject.SetActive(true);
        }

        private void OnRelease(BoneBullet obj)
        {
            if (obj == null) return;

            // 비활성화
            obj.gameObject.SetActive(false);
        }

        // 메서드명을 변경하여 Unity 라이프사이클 메서드와 충돌 방지
        private void OnDestroyPoolItem(BoneBullet obj)
        {
            if (obj != null)
            {
                Destroy(obj.gameObject);
            }
        }

        #endregion

        #region 무기 동작 관리
        
        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            base.Weaphon_Attack(attackAngle);
            ThrowBone(attackAngle).Forget();
        }

   

        #endregion


        #region 총알 발사

        private async UniTask ThrowBone(Vector3 attackAngle)
        {
            // 이미 공격 중이면 무시
            if (m_isAttacking) return;

            m_isAttacking = true;

            try
            {
                BoneBullet bullet = WeaphonBoneObjectPool.Get();
                bullet.transform.position = transform.position;
                bullet.ThrowBullet(attackAngle);

                await UniTask.Delay(TimeSpan.FromSeconds(coolTime),
                    cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            catch (Exception ex)
            {
                // UniTask의 CancellationToken으로 인해 발생하는 예외는 정상적인 종료 과정이므로 로그를 남기지 않습니다.
                if (ex is not OperationCanceledException)
                {
                    Debug.LogError($"뼈 발사 중 오류 발생: {ex.Message}");
                }
            }
            finally
            {
                m_isAttacking = false;
            }
        }

        #endregion
    }
}