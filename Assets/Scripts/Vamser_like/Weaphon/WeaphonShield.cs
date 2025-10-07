using DG.Tweening;
using UnityEngine;
using Cysharp.Threading.Tasks;

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

        // 성능 최적화를 위해 ObjectPoolSpawner 참조를 캐싱합니다.
        private ObjectPoolSpawner _objectPooler;

        #endregion

        #region Unity 라이프사이클

        public override void OnEnable()
        {
            base.OnEnable();
            
            if (_shieldRenderer == null)
                _shieldRenderer = shieldCollider.GetComponent<SpriteRenderer>();
            
            // 게임 매니저에서 ObjectPoolSpawner 인스턴스를 캐싱합니다.
            if (VamserLikeGameManager.Instance != null)
            {
                _objectPooler = VamserLikeGameManager.Instance.objectPoolSpawner;
            }
                
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
            if (isUpgradelv2)
            {
                LaunchBoomerangAttackAsync();
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
        private void LaunchBoomerangAttackAsync()
        {
            if (boomerangPrefab == null)
            {
                LogManager.LogWarning("부메랑 프리팹이 할당되지 않았습니다.", LogManager.LogCategory.Weapon, this);
                return;
            }
            
            if (_objectPooler == null)
            {
                LogManager.LogError("ObjectPoolSpawner를 찾을 수 없습니다.", LogManager.LogCategory.Weapon, this);
                return;
            }

            float angleStep = 360f / boomerangCount;
    
            for (int i = 0; i < boomerangCount; i++)
            {
                float angle = i * angleStep;
                Vector3 direction = Quaternion.Euler(0, 0, angle) * Vector3.up;
                
                // 각 부메랑이 생성되는 순간의 플레이어 위치를 실시간으로 가져옵니다.
                Vector3 spawnPosition = VamserLikeGameManager.Instance.PlayerPos(); 
                GameObject boomerang = _objectPooler.SpawnObject(boomerangPrefab, spawnPosition, Quaternion.Euler(0, 0, angle));
                if (boomerang == null) continue;
        
                // 각 부메랑의 애니메이션을 독립적으로 실행하고, 발사 위치를 전달합니다.
                AnimateSingleBoomerangAsync(boomerang, direction, spawnPosition).Forget();
            }
        }

        /// <summary>
        /// 단일 부메랑의 이동 및 회전 애니메이션을 처리하고, 완료 시 풀에 반환합니다.
        /// </summary>
        private async UniTaskVoid AnimateSingleBoomerangAsync(GameObject boomerang, Vector3 direction, Vector3 originPosition)
        {
            try
            {
                float outwardDuration = boomerangDistance / boomerangSpeed;
                float returnDuration = outwardDuration;
                float totalDuration = outwardDuration + returnDelay + returnDuration;

                // 회전 트윈 생성
                float totalRotations = totalDuration * boomerangRotationsPerSecond;
                var rotateTween = boomerang.transform.DORotate(new Vector3(0, 0, 360f * totalRotations), totalDuration, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear);
                
                // 트윈의 생명주기를 부메랑 오브젝트에 연결하여, 오브젝트 비활성화 시 트윈이 자동 정리되도록 합니다.
                rotateTween.SetLink(boomerang);

                // 1. 발사 지점에서 바깥으로 이동
                await boomerang.transform.DOMove(originPosition + (direction * boomerangDistance), outwardDuration)
                    .SetEase(Ease.OutQuad)
                    .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());

                // 2. 복귀 전 딜레이
                await UniTask.Delay(System.TimeSpan.FromSeconds(returnDelay), cancellationToken: this.GetCancellationTokenOnDestroy());

                // 3. 플레이어의 현재 위치를 동적으로 추적하며 복귀 (수동 루프)
                // 이동 주체인 GameManager로부터 플레이어의 실시간 위치를 가져옵니다.
                while (boomerang.activeInHierarchy && Vector3.Distance(boomerang.transform.position, VamserLikeGameManager.Instance.PlayerPos()) > 0.1f)
                {
                    boomerang.transform.position = Vector3.MoveTowards(boomerang.transform.position, VamserLikeGameManager.Instance.PlayerPos(), boomerangSpeed * Time.deltaTime);
                    await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
                }
            }
            finally
            {
                // 애니메이션이 성공적으로 끝나거나, 도중에 취소되어도 부메랑을 풀에 반환합니다.
                if (boomerang.activeInHierarchy) // 오브젝트가 여전히 활성 상태일 때만 반환
                {
                    _objectPooler.ReturnObject(boomerang);
                }
            }
        }

        #endregion
    }
}