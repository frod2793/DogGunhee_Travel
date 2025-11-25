using UnityEngine;
using UnityEngine.Serialization;
using Cysharp.Threading.Tasks;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 화면 내에서 계속 튕기는 단 하나의 영구적인 진주를 발사하는 무기입니다.
    /// [최적화] 실시간 레벨업 반영 기능 추가
    /// </summary>
    public class WeaponPearl : WeaphonBase
    {
        #region 인스펙터 필드

        [Header("진주 설정")]
        [Tooltip("발사할 진주 프리팹 (PearlProjectile 컴포넌트 필수)")]
        [FormerlySerializedAs("pearlPrefab")]
        [SerializeField] private GameObject m_pearlPrefab;

        [Header("진주 스탯")]
        [Tooltip("진주의 이동 속도")]
        [SerializeField] private float m_pearlSpeed = 5f;

        #endregion

        #region 내부 상태 변수

        // 현재 활성화된 진주 인스턴스 (단 하나만 유지)
        private GameObject m_activePearlInstance;
        private PearlProjectile m_activeProjectileScript; // 스크립트 캐싱

        // 레벨 변경 감지용 변수
        private bool m_currentUpgradeState;

        #endregion

        #region Unity 라이프사이클

        protected override void OnEnable()
        {
            base.OnEnable();
            
            // 초기 상태 동기화
            m_activePearlInstance = null;
            m_activeProjectileScript = null;
            m_currentUpgradeState = this.isUpgradelv2;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            // 무기가 비활성화(교체)될 때, 진주를 회수합니다.
            ReturnPearl();
        }

        private void Update()
        {
            // 이미 진주가 소환되어 있는 상태에서 레벨업(업그레이드)이 발생했는지 체크
            if (m_activePearlInstance != null && m_activePearlInstance.activeInHierarchy)
            {
                if (m_currentUpgradeState != this.isUpgradelv2)
                {
                    m_currentUpgradeState = this.isUpgradelv2;
                    
                    // 진주에게 변경된 정보(공격력, 레벨 등)를 즉시 전달
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
            // 이미 활성화된 진주가 있다면 새로 발사하지 않습니다.
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
                LogManager.LogError("[WeaponPearl] 스포너 또는 프리팹이 없습니다.");
                return;
            }

            // 방향이 0이면 랜덤 방향
            if (direction == Vector3.zero) direction = UnityEngine.Random.insideUnitCircle.normalized;

            // 스폰
            m_activePearlInstance = spawner.SpawnObject(m_pearlPrefab, transform.position, Quaternion.identity);
            
            if (m_activePearlInstance.TryGetComponent(out PearlProjectile projectile))
            {
                m_activeProjectileScript = projectile; // 캐싱
                
                // 스탯 초기화 및 발사 (무기 정보와 초기 속도 전달)
                projectile.Initialize(this, direction.normalized * m_pearlSpeed);
            }
            else
            {
                LogManager.LogError("[WeaponPearl] 프리팹에 PearlProjectile 컴포넌트가 없습니다.");
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