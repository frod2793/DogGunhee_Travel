using System;
using Cysharp.Threading.Tasks;
using InGame.ObjectPool;
using UnityEngine;
using InGame.Weaphon.Base;


namespace InGame.Weaphon
{
    public class WeaphonBone : WeaphonBase
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
        
        private bool m_isAttacking; // 중복 호출 방지 플래그

        #endregion

        #region 초기화 및 오브젝트 풀 관리

        private new void OnEnable()
        {
            SetWeaphonState(WeaphonState.Idle);
            
            if (m_bulletParent == null)
            {
                m_bulletParent = transform;
            }
            
            WeaponPoolManager.Instance.GetOrAddPool<BoneBullet>(
                CreateBullet,
                OnGet,
                OnRelease,
                OnDestroyPoolItem,
                maxSize: m_poolSizeBulletCount
            );
        }

        // 이 메서드들은 이제 WeaponPoolManager의 델리게이트로 사용됩니다.
        private BoneBullet CreateBullet()
        {
            GameObject bulletObject = Instantiate(m_bonePrefab, m_bulletParent);
            
            BoneBullet bullet = bulletObject.GetComponent<BoneBullet>();

            bullet.gameObject.name = $"{m_bonePrefab.name}_{Guid.NewGuid().ToString().Substring(0, 4)}";
            bulletObject.SetActive(false);

            return bullet;
        }

        private void OnGet(BoneBullet obj)
        {
            if (obj == null) return;

            obj.ResetState();
            obj.Initialize(this); // WeaphonBone 참조는 여전히 필요할 수 있습니다 (예: 공격력 정보)
            obj.gameObject.SetActive(true);
        }

        private void OnRelease(BoneBullet obj)
        {
            if (obj == null) return;

            obj.gameObject.SetActive(false);
            obj.transform.SetParent(m_bulletParent); // 풀로 돌아갈 때 부모 설정
        }

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
            ThrowBone(attackAngle).Forget();
        }

        #endregion


        #region 총알 발사

        private async UniTask ThrowBone(Vector3 attackAngle)
        {
            if (m_isAttacking) return;

            m_isAttacking = true;

            try
            {
                // WeaponPoolManager를 통해 총알을 가져옵니다.
                BoneBullet bullet = WeaponPoolManager.Instance.Get<BoneBullet>();
                if (bullet == null)
                {
                    Debug.LogWarning("Failed to get BoneBullet from pool.");
                    return;
                }

                bullet.transform.position = transform.position;
                
                bullet.transform.SetParent(null); // 발사 시 부모 해제
                bullet.ThrowBullet(attackAngle);

                await UniTask.Delay(TimeSpan.FromSeconds(coolTime),
                    cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            catch (Exception ex)
            {
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
