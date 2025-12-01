using UnityEngine;
using Vamser_like.Weaphon.Base;
using InGame.ObjectPool; // WeaponPoolManager 사용을 위해 추가

namespace Vamser_like.Weaphon
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

        [Header("진주 스탯")]
        [Tooltip("진주의 이동 속도")]
        [SerializeField] private float m_pearlSpeed = 5f;

        #endregion

        #region 내부 상태 변수

        private PearlProjectile m_activePearlProjectile; // 활성화된 진주 투사체 스크립트 참조

        private bool m_currentEvolveState;

        #endregion

        #region Unity 라이프사이클

        private new void OnEnable()
        {
            SetWeaphonState(WeaphonState.Idle);

            m_activePearlProjectile = null;
            m_currentEvolveState = this.isEvolved;

            // WeaponPoolManager를 통해 PearlProjectile 풀을 등록합니다.
            WeaponPoolManager.Instance.GetOrAddPool<PearlProjectile>(
                CreatePearlProjectile,
                OnGetPearlProjectile,
                OnReleasePearlProjectile,
                OnDestroyPearlProjectile,
                defaultCapacity: 1, // 진주는 하나만 존재하므로 용량 1
                maxSize: 1
            );
        }

        private new void OnDisable()
        {
            ReturnPearl();
        }

        private void Update()
        {
            if (m_activePearlProjectile != null && m_activePearlProjectile.gameObject.activeInHierarchy)
            {
                if (m_currentEvolveState != this.isEvolved)
                {
                    m_currentEvolveState = this.isEvolved;
                    
                    if (m_activePearlProjectile != null)
                    {
                        m_activePearlProjectile.UpdateState(this);
                    }
                }
            }
        }

        #endregion

        #region 무기 동작

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            // 이미 활성화된 진주가 있다면 새로 발사하지 않습니다.
            if (m_activePearlProjectile != null && m_activePearlProjectile.gameObject.activeInHierarchy)
            {
                return;
            }

            LaunchPearl(attackAngle);
        }

        /// <summary>
        /// 진주를 발사합니다.
        /// </summary>
        /// <param name="direction">진주가 나아갈 방향</param>
        private void LaunchPearl(Vector3 direction)
        {
            if (m_pearlPrefab == null)
            {
                LogManager.LogError("[WeaphonJinjoo] 진주 프리팹이 할당되지 않았습니다.");
                return;
            }

            if (direction == Vector3.zero) direction = UnityEngine.Random.insideUnitCircle.normalized;

            // WeaponPoolManager를 통해 진주를 가져옵니다.
            PearlProjectile pearl = WeaponPoolManager.Instance.Get<PearlProjectile>();
            if (pearl == null)
            {
                LogManager.LogError("[WeaphonJinjoo] 진주 풀에서 PearlProjectile을 가져오지 못했습니다.");
                return;
            }

            pearl.transform.SetPositionAndRotation(transform.position, Quaternion.identity);
            m_activePearlProjectile = pearl;
            pearl.Initialize(this, direction.normalized * m_pearlSpeed);
        }

        /// <summary>
        /// 활성화된 진주를 풀로 반환합니다.
        /// </summary>
        private void ReturnPearl()
        {
            if (m_activePearlProjectile != null)
            {
                WeaponPoolManager.Instance.Release(m_activePearlProjectile);
                m_activePearlProjectile = null;
            }
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