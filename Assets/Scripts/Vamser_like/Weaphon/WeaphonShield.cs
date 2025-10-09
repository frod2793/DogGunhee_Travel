using DG.Tweening;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Pool;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 방패를 지면에 내려찍어 충격파로 공격하는 무기입니다.
    /// 특정 조건(isUpgradelv1) 만족 시, 방패가 지면에 닿을 때 5방향으로 작은 부메랑 방패를 추가로 발사합니다.
    /// 모든 애니메이션은 UniTask와 DOTween으로 관리되며, 부메랑은 오브젝트 풀링을 통해 효율적으로 생성됩니다.
    /// </summary>
    public class WeaphonShield : Weaphon_base
    {
        #region 필드 및 변수

        [Header("<color=green> 방패 아이템 관련 변수")]
        [SerializeField] private GameObject shield;
        [SerializeField] private Collider2D shieldCollider;
        
        private SpriteRenderer _shieldRenderer;
        private bool _isAnimShield; // 중복 호출 방지 플래그
        private readonly Vector3 _startPosition = new Vector3(0, 1, 0);
        private readonly Vector3 _endPosition = new Vector3(0, -0.1f, 0);

        [Header("방패 공격 설정")]
        [Tooltip("방패가 지면에 닿기까지 걸리는 시간입니다.")]
        [SerializeField] private float shieldAnimDuration = 0.5f;
        [Tooltip("방패가 지면에 닿은 후 충격파가 유지되는 시간입니다.")]
        [SerializeField] private float shockwaveDuration = 0.1f;
        
        [Header("부메랑 공격 설정")]
       
        [SerializeField] private GameObject boomerangPrefab; // 오브젝트 풀에서 사용할 프리팹
        [SerializeField] private int boomerangCount = 5;
        [SerializeField] private float boomerangSpeed = 5f;
        [SerializeField] private float boomerangDistance = 3f;
        [SerializeField] private float returnDelay = 0.1f;
        [Tooltip("부메랑이 초당 회전하는 횟수입니다.")]
        [SerializeField] private float boomerangRotationsPerSecond = 2.5f;
        
        // 부메랑 오브젝트 풀
        private IObjectPool<BoomerangProjectile> _boomerangPool;
        private Transform _playerTransform; // 플레이어 위치 추적을 위한 캐시

        #endregion

        #region Unity 라이프사이클
        

        public override void OnEnable()
        {
            base.OnEnable(); // 부모 클래스의 OnEnable을 호출하여 상태를 초기화합니다.
            InitializeBoomerangPool();
            
            if (_shieldRenderer == null)
                _shieldRenderer = shieldCollider.GetComponent<SpriteRenderer>();
            
            // 게임 매니저에서 ObjectPoolSpawner 인스턴스를 캐싱합니다.
            _playerTransform = VamserLikeGameManager.Instance.PlayerTransfrom();
                
            // 초기 상태 설정
            shieldCollider.enabled = false;
            _shieldRenderer.enabled = false;
            shield.transform.localPosition = _startPosition;
            _isAnimShield = false;
        }
        
        public override void OnDisable()
        {
            base.OnDisable();
            // 이 오브젝트와 관련된 모든 DOTween 애니메이션을 안전하게 종료합니다.
            // SetLink 또는 SetTarget을 사용했다면 자동으로 처리되지만, 안정성을 위해 명시적으로 호출합니다.
            transform.DOKill();
            shield.transform.DOKill();
        }

        #endregion

        #region 무기 동작 관리

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            base.Weaphon_Attack(attackAngle);
            // UniTask를 사용하여 비동기적으로 애니메이션을 실행하고, Forget()으로 "Fire and Forget" 처리합니다.
            AnimateShieldAttackAsync().Forget();
        }

        #endregion

        #region 애니메이션 및 이펙트

        /// <summary>
        /// UniTask와 DOTween을 사용하여 방패 공격 애니메이션을 안정적이고 순차적으로 실행합니다.
        /// </summary>
        private async UniTaskVoid AnimateShieldAttackAsync()
        {
            if (_isAnimShield) return;
            _isAnimShield = true;

            // 업그레이드 상태일 경우, 방패 애니메이션과 별개로 부메랑 공격을 즉시 시작합니다.
            if (isUpgradelv2) // isUpgradelv2는 부모 클래스 Weaphon_base에 정의되어 있습니다.
            {
                LaunchBoomerangs();
            }
            
            try
            {
                // 초기 상태 설정
                _shieldRenderer.enabled = true;
                shieldCollider.enabled = false;
                shield.transform.localPosition = _startPosition;

                // 1. 방패가 땅에 닿는 애니메이션 (비동기 대기)
                await shield.transform.DOLocalMove(_endPosition, shieldAnimDuration)
                    .SetEase(Ease.OutBounce)
                    .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());

                // 2. 땅에 닿은 후 효과 처리
                shieldCollider.enabled = true;

                // 3. 충격파 유지 시간 (비동기 대기)
                await UniTask.Delay(System.TimeSpan.FromSeconds(shockwaveDuration), cancellationToken: this.GetCancellationTokenOnDestroy());

                // 4. 효과 종료
                shieldCollider.enabled = false;
                _shieldRenderer.enabled = false;
            }
            finally
            {
                // 애니메이션이 성공적으로 끝나거나, 도중에 취소/오류가 발생해도 항상 공격 가능 상태로 복원합니다.
                _isAnimShield = false;
            }
        }
        
        /// <summary>
        /// 오브젝트 풀링을 사용하여 부메랑 광역 공격을 비동기적으로 실행합니다.
        /// </summary>
        private void LaunchBoomerangs()
        {
            if (boomerangPrefab == null)
            {
                LogManager.LogWarning("부메랑 프리팹이 할당되지 않았습니다.", LogManager.LogCategory.Weapon, this);
                return;
            }

            float angleStep = 360f / boomerangCount;
    
            for (int i = 0; i < boomerangCount; i++)
            {
                float angle = i * angleStep;
                Vector3 direction = Quaternion.Euler(0, 0, angle) * Vector3.up;

                // 풀에서 부메랑을 가져와 초기화합니다.
                var boomerang = _boomerangPool.Get();
                boomerang.transform.position = _playerTransform.position;
                boomerang.transform.rotation = Quaternion.Euler(0, 0, angle);
                boomerang.Initialize(_boomerangPool, this.attackPower, this.mobStunTime, _playerTransform, direction, boomerangSpeed, boomerangDistance, returnDelay, boomerangRotationsPerSecond);
            }
        }

        #endregion

        #region 오브젝트 풀링

        private void InitializeBoomerangPool()
        {
            _boomerangPool = new ObjectPool<BoomerangProjectile>(
                // AddComponent는 프리팹에 설정된 값을 무시하고 기본값으로 컴포넌트를 생성하여 데이터 유실을 유발합니다.
                // Instantiate로 생성된 인스턴스에서 GetComponent를 사용하여 기존 컴포넌트를 가져와야 합니다.
                createFunc: () => Instantiate(boomerangPrefab).GetComponent<BoomerangProjectile>(),
                actionOnGet: (proj) => proj.gameObject.SetActive(true),
                actionOnRelease: (proj) => proj.gameObject.SetActive(false),
                actionOnDestroy: (proj) => Destroy(proj.gameObject),
                maxSize: boomerangCount * 2
            );
        }

        // OnDestroy에서 풀을 정리하여 메모리 누수를 방지합니다.
        private void OnDestroy()
        {
            if (_boomerangPool is System.IDisposable disposablePool)
            {
                disposablePool.Dispose();
            }
        }

        #endregion
    }
}