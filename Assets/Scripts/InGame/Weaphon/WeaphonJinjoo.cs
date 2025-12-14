using UnityEngine;
using InGame.Weaphon.Base;
using InGame.ObjectPool;


// todo : 진주 오브젝트 바운드시 모래 먼지 이펙트 추가 

namespace InGame.Weaphon
{
    /// <summary>
    /// 화면 내에서 계속 튕기는 단 하나의 영구적인 진주를 발사하는 무기입니다.
    /// </summary>
    public class WeaphonJinjoo : WeaphonBase
    {
        #region 인스펙터 필드

        [Header("진주 설정")]
        [Tooltip("발사할 진주 프리팹 (PearlProjectile 컴포넌트 필수)")]
        [SerializeField] private GameObject m_pearlPrefab;

        #endregion

        #region 내부 상태 변수

        private bool m_currentEvolveState;

        #endregion

        #region Unity 라이프사이클

        private new void OnEnable()
        {
            SetWeaphonState(WeaphonState.Idle);
            m_currentEvolveState = this.isEvolved;

            // WeaponPoolManager를 통해 PearlProjectile 풀을 등록합니다.
            WeaponPoolManager.Instance.GetOrAddPool<PearlProjectile>(
                CreatePearlProjectile,
                OnGetPearlProjectile,
                OnReleasePearlProjectile,
                OnDestroyPearlProjectile,
                defaultCapacity: 1,
                maxSize: 1
            );
        }

        private new void OnDisable()
        {
            // OnDisable에서 진주를 반환하는 로직 제거
            // 진주는 이제 독립적으로 존재합니다.
        }

        private void Update()
        {
            // static 인스턴스를 통해 진주가 존재하는지 확인하고 상태 업데이트
            if (PearlProjectile.Instance != null)
            {
                if (m_currentEvolveState != this.isEvolved ||
                    PearlProjectile.Instance.CurrentSpeed != this.attackSpeed)
                {
                    m_currentEvolveState = this.isEvolved;
                    PearlProjectile.Instance.UpdateState(this);
                }
            }
        }

        #endregion

        #region 무기 동작

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            // static 인스턴스를 통해 이미 활성화된 진주가 있는지 확인
            if (PearlProjectile.Instance != null)
            {
                return;
            }

            LaunchPearl(attackAngle);
        }

        private void LaunchPearl(Vector3 direction)
        {
            if (m_pearlPrefab == null)
            {
                LogManager.LogError("[WeaphonJinjoo] 진주 프리팹이 할당되지 않았습니다.");
                return;
            }

            if (direction == Vector3.zero) direction = UnityEngine.Random.insideUnitCircle.normalized;

            PearlProjectile pearl = WeaponPoolManager.Instance.Get<PearlProjectile>();
            if (pearl == null)
            {
                LogManager.LogError("[WeaphonJinjoo] 진주 풀에서 PearlProjectile을 가져오지 못했습니다.");
                return;
            }

            pearl.transform.SetPositionAndRotation(transform.position, Quaternion.identity);

            float initialSpeed = (this.attackSpeed > 0) ? this.attackSpeed : 1f;
            pearl.Initialize(this, direction.normalized * initialSpeed);
        }

        #endregion

        #region Object Pooling Delegates

        private PearlProjectile CreatePearlProjectile() => Instantiate(m_pearlPrefab).GetComponent<PearlProjectile>();
        private void OnGetPearlProjectile(PearlProjectile pearl) => pearl.gameObject.SetActive(true);
        private void OnReleasePearlProjectile(PearlProjectile pearl) => pearl.gameObject.SetActive(false);
        private void OnDestroyPearlProjectile(PearlProjectile pearl) => Destroy(pearl.gameObject);

        #endregion
    }
}