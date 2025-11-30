using UnityEngine;
using Vamser_like.Weaphon.Base;

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

        private GameObject m_activePearlInstance;
        private PearlProjectile m_activeProjectileScript;

        private bool m_currentEvolveState;

        #endregion

        #region Unity 라이프사이클

        private new void OnEnable()
        {
            SetWeaphonState(WeaphonState.Idle);
            
            m_activePearlInstance = null;
            m_activeProjectileScript = null;
            m_currentEvolveState = this.isEvolved;
        }

        private new void OnDisable()
        {
            ReturnPearl();
        }

        private void Update()
        {
            if (m_activePearlInstance != null && m_activePearlInstance.activeInHierarchy)
            {
                if (m_currentEvolveState != this.isEvolved)
                {
                    m_currentEvolveState = this.isEvolved;
                    
                    if (m_activeProjectileScript != null)
                    {
                        m_activeProjectileScript.UpdateState(this);
                    }
                }
            }
        }

        #endregion

        #region 무기 동작

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            if (m_activePearlInstance != null && m_activePearlInstance.activeInHierarchy)
            {
                return;
            }

            LaunchPearl(attackAngle);
        }

        private void LaunchPearl(Vector3 direction)
        {
            var spawner = GameManager.Instance?.ObjectPoolSpawner;
            if (spawner == null || m_pearlPrefab == null)
            {
                LogManager.LogError("[WeaphonJinjoo] 스포너 또는 프리팹이 없습니다.");
                return;
            }

            if (direction == Vector3.zero) direction = UnityEngine.Random.insideUnitCircle.normalized;

            m_activePearlInstance = spawner.SpawnObject(m_pearlPrefab, transform.position, Quaternion.identity);
            
            if (m_activePearlInstance.TryGetComponent(out PearlProjectile projectile))
            {
                m_activeProjectileScript = projectile;
                projectile.Initialize(this, direction.normalized * m_pearlSpeed);
            }
            else
            {
                LogManager.LogError("[WeaphonJinjoo] 프리팹에 PearlProjectile 컴포넌트가 없습니다.");
                ReturnPearl();
            }
        }

        private void ReturnPearl()
        {
            if (m_activePearlInstance != null)
            {
                var spawner = GameManager.Instance?.ObjectPoolSpawner;
                spawner?.ReturnObject(m_activePearlInstance);
                
                m_activePearlInstance = null;
                m_activeProjectileScript = null;
            }
        }

        #endregion
    }
}