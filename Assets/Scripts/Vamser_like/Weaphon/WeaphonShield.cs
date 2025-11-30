using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Pool;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using Vamser_like.Weaphon.Base;

namespace Vamser_like.Weaphon
{
    /// <summary>
    /// 방패를 지면에 내려찍어 충격파로 공격하는 무기 클래스입니다.
    /// </summary>
    public class WeaphonShield : WeaphonBase
    {
        #region 인스펙터 필드

        [Header("<color=green> 방패 아이템 관련 변수")]
        [SerializeField] private GameObject m_shieldObj;
        
        [Header("애니메이션 설정")]
        [SerializeField] private Animator m_shieldAnimator;
        [SerializeField] private ShieldShockwave m_shockwavePrefab; 

        [Header("공격 타이밍 설정")]
        [SerializeField] private float m_impactTriggerTime = 1.07f; 

        [Header("부메랑 공격 설정")]
        [SerializeField] private GameObject m_boomerangPrefab;
        [SerializeField] private int m_boomerangCount = 5;
        [SerializeField] private float m_boomerangSpeed = 5f;
        [SerializeField] private float m_boomerangDistance = 3f;
        [SerializeField] private float m_returnDelay = 0.1f;
        [SerializeField] private float m_boomerangRotationsPerSecond = 2.5f;

        #endregion

        #region 내부 캐시 및 상태 변수

        private Transform m_shieldTransform;
        private Transform m_playerTransform;
        
        private IObjectPool<shieldProjectile> m_boomerangPool;
        private IObjectPool<ShieldShockwave> m_shockwavePool;

        private bool m_isAnimShield;
        
        private static readonly int k_AnimHashAttack = Animator.StringToHash("Attack");

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            if (m_shieldObj != null) m_shieldTransform = m_shieldObj.transform;
            
            InitializeBoomerangPool();
            InitializeShockwavePool();
        }

        private new void OnEnable()
        {
            SetWeaphonState(WeaphonState.Idle);
            
            if (GameManager.Instance != null)
                m_playerTransform = GameManager.Instance.PlayerTransfrom();

            ResetShieldState();
        }

        private new void OnDisable()
        {
            transform.DOKill();
        }

        private void OnDestroy()
        {
            if (m_boomerangPool is System.IDisposable bPool) bPool.Dispose();
            if (m_shockwavePool is System.IDisposable sPool) sPool.Dispose();
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TestAttack();
            }
        }
#endif

        #endregion

        #region 무기 동작 관리

        [ContextMenu("Test Attack")]
        public void TestAttack()
        {
            if (!Application.isPlaying) return;
            Weaphon_Attack(Vector3.up);
        }

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            AnimateShieldAttackAsync().Forget();
        }

        private void ResetShieldState()
        {
            if (m_shieldTransform != null) m_shieldTransform.localPosition = Vector3.zero;
            
            if (m_shieldAnimator != null)
            {
                m_shieldAnimator.Rebind();
                m_shieldAnimator.speed = 1f;
            }

            m_isAnimShield = false;
        }

        #endregion

        #region 애니메이션 및 로직

        private async UniTaskVoid AnimateShieldAttackAsync()
        {
            if (m_isAnimShield) return;
            m_isAnimShield = true;

            var token = this.GetCancellationTokenOnDestroy();

            if (isEvolved)
            {
                LaunchBoomerangs();
            }

            try
            {
                if (m_shieldTransform != null) m_shieldTransform.localPosition = Vector3.zero;

                float speedMultiplier = (this.attackSpeed > 0) ? this.attackSpeed : 1.0f;

                if (m_shieldAnimator != null)
                {
                    m_shieldAnimator.speed = speedMultiplier;
                    m_shieldAnimator.SetTrigger(k_AnimHashAttack);
                }

                float waitTime = m_impactTriggerTime / speedMultiplier;
                
                await UniTask.Delay(System.TimeSpan.FromSeconds(waitTime), cancellationToken: token);

                SpawnShockwaveEffect();
            }
            finally
            {
                ResetShieldState();
            }
        }

        private void SpawnShockwaveEffect()
        {
            if (m_shockwavePool == null) return;

            ShieldShockwave effect = m_shockwavePool.Get();
            
            effect.transform.position = transform.position; 
            effect.transform.rotation = Quaternion.identity;

            effect.Initialize(m_shockwavePool, this.attackPower, this.mobStunTime, this.attackSpeed);
        }

        private void LaunchBoomerangs()
        {
            if (m_boomerangPrefab == null || m_playerTransform == null) return;

            float angleStep = 360f / m_boomerangCount;
            Vector3 spawnPos = m_playerTransform.position;

            for (int i = 0; i < m_boomerangCount; i++)
            {
                float currentAngle = i * angleStep;
                Quaternion rotation = Quaternion.Euler(0, 0, currentAngle);
                Vector3 direction = rotation * Vector3.up;

                var boomerang = m_boomerangPool.Get();
                boomerang.transform.SetPositionAndRotation(spawnPos, rotation);
                
                boomerang.Initialize(
                    m_boomerangPool, 
                    this.attackPower, 
                    this.mobStunTime, 
                    m_playerTransform, 
                    direction, 
                    m_boomerangSpeed, 
                    m_boomerangDistance, 
                    m_returnDelay, 
                    m_boomerangRotationsPerSecond
                );
            }
        }

        #endregion

        #region 오브젝트 풀링

        private void InitializeBoomerangPool()
        {
            if (m_boomerangPrefab == null) return;

            m_boomerangPool = new ObjectPool<shieldProjectile>(
                createFunc: () => Instantiate(m_boomerangPrefab).GetComponent<shieldProjectile>(),
                actionOnGet: (p) => p.gameObject.SetActive(true),
                actionOnRelease: (p) => p.gameObject.SetActive(false),
                actionOnDestroy: (p) => Destroy(p.gameObject),
                collectionCheck: false,
                defaultCapacity: m_boomerangCount,
                maxSize: m_boomerangCount * 3
            );
        }

        private void InitializeShockwavePool()
        {
            if (m_shockwavePrefab == null)
            {
                Debug.LogError("[WeaphonShield] Shockwave Prefab이 할당되지 않았습니다.");
                return;
            }

            m_shockwavePool = new ObjectPool<ShieldShockwave>(
                createFunc: () => Instantiate(m_shockwavePrefab),
                actionOnGet: (effect) => effect.gameObject.SetActive(true),
                actionOnRelease: (effect) => effect.gameObject.SetActive(false),
                actionOnDestroy: (effect) => Destroy(effect.gameObject),
                collectionCheck: false,
                defaultCapacity: 2,
                maxSize: 5
            );
        }

        #endregion
    }
}