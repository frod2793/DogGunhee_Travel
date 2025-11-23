using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Pool;
using DG.Tweening;
using Cysharp.Threading.Tasks;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 방패를 지면에 내려찍어 충격파로 공격하는 무기 클래스입니다.
    /// Animator를 사용하여 공격 모션을 재생하며, AttackSpeed에 따라 애니메이션 속도와 타격 타이밍이 조절됩니다.
    /// </summary>
    public class WeaphonShield : Weaphon_base
    {
        #region 인스펙터 필드 (데이터 보존)

        [Header("<color=green> 방패 아이템 관련 변수")]
        [FormerlySerializedAs("shield")]
        [SerializeField] private GameObject m_shieldObj;
        
        // [참고] 충격파 프리팹이 자체 콜라이더를 가지므로, 무기 본체의 콜라이더는 사용하지 않습니다.
        // [FormerlySerializedAs("shieldCollider")]
        // [SerializeField] private Collider2D m_shieldCollider;

        [Header("애니메이션 설정")]
        [Tooltip("방패 본체의 애니메이터 (공격 모션)")]
        [SerializeField] private Animator m_shieldAnimator;
        
        [Tooltip("소환할 충격파 이펙트 프리팹 (ShieldShockwave 스크립트 포함)")]
        [SerializeField] private ShieldShockwave m_shockwavePrefab; 

        [Header("공격 타이밍 설정")]
        [Tooltip("방패 애니메이션 시작 후, 충격파가 발생하기까지의 기본 대기 시간 (공격속도 1.0 기준)\n(예: 60fps 기준 1:04 = 약 1.067초)")]
        [SerializeField] private float m_impactTriggerTime = 1.07f; 

        [Header("부메랑 공격 설정")]
        [FormerlySerializedAs("boomerangPrefab")]
        [SerializeField] private GameObject m_boomerangPrefab;
        
        [FormerlySerializedAs("boomerangCount")]
        [SerializeField] private int m_boomerangCount = 5;
        
        [FormerlySerializedAs("boomerangSpeed")]
        [SerializeField] private float m_boomerangSpeed = 5f;
        
        [FormerlySerializedAs("boomerangDistance")]
        [SerializeField] private float m_boomerangDistance = 3f;
        
        [FormerlySerializedAs("returnDelay")]
        [SerializeField] private float m_returnDelay = 0.1f;
        
        [Tooltip("부메랑이 초당 회전하는 횟수입니다.")]
        [FormerlySerializedAs("boomerangRotationsPerSecond")]
        [SerializeField] private float m_boomerangRotationsPerSecond = 2.5f;

        #endregion

        #region 내부 캐시 및 상태 변수

        private Transform m_shieldTransform;
        private Transform m_playerTransform;
        
        // 오브젝트 풀
        private IObjectPool<shieldProjectile> m_boomerangPool;
        private IObjectPool<ShieldShockwave> m_shockwavePool;

        private bool m_isAnimShield;
        
        // 애니메이션 파라미터 해싱
        private static readonly int k_AnimHashAttack = Animator.StringToHash("Attack");

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            if (m_shieldObj != null) m_shieldTransform = m_shieldObj.transform;
            
            InitializeBoomerangPool();
            InitializeShockwavePool();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            
            if (VamserLikeGameManager.Instance != null)
                m_playerTransform = VamserLikeGameManager.Instance.PlayerTransfrom();

            ResetShieldState();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            transform.DOKill(); // 안전장치
        }

        private void OnDestroy()
        {
            if (m_boomerangPool is System.IDisposable bPool) bPool.Dispose();
            if (m_shockwavePool is System.IDisposable sPool) sPool.Dispose();
        }

#if UNITY_EDITOR
        private void Update()
        {
            // 에디터 테스트용: 스페이스바 입력 시 공격
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
            base.Weaphon_Attack(attackAngle);
            AnimateShieldAttackAsync().Forget();
        }

        private void ResetShieldState()
        {
            // 방패 위치 및 애니메이터 초기화
            if (m_shieldTransform != null) m_shieldTransform.localPosition = Vector3.zero;
            
            if (m_shieldAnimator != null)
            {
                m_shieldAnimator.Rebind();
                m_shieldAnimator.speed = 1f; // 속도 원상복구
            }

            m_isAnimShield = false;
        }

        #endregion

        #region 애니메이션 및 로직

        private async UniTaskVoid AnimateShieldAttackAsync()
        {
            if (m_isAnimShield) return;
            m_isAnimShield = true;

            // 캔슬 토큰 (객체 파괴 시 비동기 작업 중단)
            var token = this.GetCancellationTokenOnDestroy();

            // 업그레이드 상태면 부메랑 발사
            if (isUpgradelv2)
            {
                LaunchBoomerangs();
            }

            try
            {
                // 1. 초기화
                if (m_shieldTransform != null) m_shieldTransform.localPosition = Vector3.zero;

                // 2. 공격 속도(attackSpeed) 반영
                // attackSpeed가 0 이하일 경우를 대비해 1.0으로 보정
                float speedMultiplier = (this.attackSpeed > 0) ? this.attackSpeed : 1.0f;

                // 3. 방패 공격 애니메이션 시작 (배속 적용)
                if (m_shieldAnimator != null)
                {
                    m_shieldAnimator.speed = speedMultiplier; // 애니메이터 속도 설정
                    m_shieldAnimator.SetTrigger(k_AnimHashAttack);
                }

                // 4. [Timing Wait] 충격파 타이밍까지 대기
                // 기본 대기 시간을 배속으로 나누어, 공속이 빠를수록 대기 시간도 짧아지게 함
                float waitTime = m_impactTriggerTime / speedMultiplier;
                
                await UniTask.Delay(System.TimeSpan.FromSeconds(waitTime), cancellationToken: token);

                // --- 충격파 발생 시점 ---
                
                // 5. 충격파 이펙트 소환 (독립 오브젝트)
                // 현재 위치에 고정된 충격파를 생성합니다.
                SpawnShockwaveEffect();

                // 6. (선택 사항) 애니메이션이 완전히 끝날 때까지 기다리거나, 후딜레이를 주고 싶다면 여기서 추가 대기
                // 예: await UniTask.Delay(TimeSpan.FromSeconds(0.2f / speedMultiplier), cancellationToken: token);
            }
            finally
            {
                // 7. 종료 및 상태 복구
                ResetShieldState();
            }
        }

        /// <summary>
        /// 충격파 이펙트를 현재 위치(World Position)에 소환합니다.
        /// </summary>
        private void SpawnShockwaveEffect()
        {
            if (m_shockwavePool == null) return;

            ShieldShockwave effect = m_shockwavePool.Get();
            
            effect.transform.position = transform.position; 
            effect.transform.rotation = Quaternion.identity;

            // [수정] Initialize 호출 시 this.attackSpeed 전달
            // 공격 속도가 빠를수록(값이 클수록) 충격파 애니메이션도 빨라집니다.
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