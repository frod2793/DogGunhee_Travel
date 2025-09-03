using DG.Tweening;
using UnityEngine;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 방패를 지면에 내려찍어 충격파로 공격하는 무기입니다.
    /// 특정 조건(isUpgradelv1) 만족 시, 방패가 지면에 닿을 때 5방향으로 작은 부메랑 방패를 추가로 발사합니다.
    /// 모든 애니메이션은 DOTween으로 관리되며, 부메랑은 오브젝트 풀링을 통해 효율적으로 생성됩니다.
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
        private Tween _shieldTween;

        [Header("방패 공격 설정")]
        [Tooltip("방패가 지면에 닿기까지 걸리는 시간입니다.")]
        [SerializeField] private float shieldAnimDuration = 0.5f;
        [Tooltip("방패가 지면에 닿은 후 충격파가 유지되는 시간입니다.")]
        [SerializeField] private float shockwaveDuration = 0.1f;
        
        [Header("부메랑 공격 설정")]
        [SerializeField] private bool isUpgradelv1 = false;
        [SerializeField] private GameObject boomerangPrefab; // 오브젝트 풀에서 사용할 프리팹
        [SerializeField] private int boomerangCount = 5;
        [SerializeField] private float boomerangSpeed = 5f;
        [SerializeField] private float boomerangDistance = 3f;
        [SerializeField] private float returnDelay = 0.1f;
        [Tooltip("부메랑이 초당 회전하는 횟수입니다.")]
        [SerializeField] private float boomerangRotationsPerSecond = 2.5f;

        #endregion

        #region Unity 라이프사이클

        public override void OnEnable()
        {
            base.OnEnable();
            
            if (_shieldRenderer == null)
                _shieldRenderer = shieldCollider.GetComponent<SpriteRenderer>();
                
            // 초기 상태 설정
            shieldCollider.enabled = false;
            _shieldRenderer.enabled = false;
            
            // 이전 Tween이 실행 중이라면 종료
            _shieldTween?.Kill();
            _isAnimShield = false;
        }
        
        private void OnDisable()
        {
            // 씬 전환 시 메모리 누수 방지
            // SetTarget(transform)을 사용하므로 DOKill()이 자동으로 호출될 수 있지만, 명시적으로 호출하여 안정성을 높입니다.
            transform.DOKill();
        }

        #endregion

        #region 무기 동작 관리

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            base.Weaphon_Attack(attackAngle);
            AnimateShieldAttack();
        }

        #endregion

        #region 애니메이션 및 이펙트

        /// <summary>
        /// DOTween 시퀀스를 사용하여 방패 공격 애니메이션을 안정적이고 순차적으로 실행합니다.
        /// </summary>
        private void AnimateShieldAttack()
        {
            // 중복 실행 방지
            if (_isAnimShield) return;
            _isAnimShield = true;

            // 이전 트윈 정리 및 초기 상태 설정
            _shieldTween?.Kill();
            _shieldRenderer.enabled = true;
            shieldCollider.enabled = false;
            shield.transform.localPosition = _startPosition; // 애니메이션 시작 위치를 명시적으로 설정

            _shieldTween = DOTween.Sequence()
                // .From()을 제거하고, 설정된 시작 위치에서 목표 위치로 이동하도록 수정합니다.
                .Append(shield.transform.DOLocalMove(_endPosition, shieldAnimDuration)
                    .SetEase(Ease.OutBounce))
                .AppendCallback(() => // 방패가 땅에 닿았을 때
                {
                    shieldCollider.enabled = true;
                    if (isUpgradelv1)
                    {
                        LaunchBoomerangAttack();
                    }
                })
                .AppendInterval(shockwaveDuration) // 충돌 판정 유지 시간
                .AppendCallback(() => // 효과 종료
                {
                    shieldCollider.enabled = false;
                    _shieldRenderer.enabled = false;
                })
                .OnComplete(() =>
                {
                    _isAnimShield = false;
                })
                .SetTarget(transform); // 트윈에 타겟을 설정하여 라이프사이클 관리
        }
        
        /// <summary>
        /// 오브젝트 풀링을 사용하여 부메랑 광역 공격을 실행합니다.
        /// </summary>
        private void LaunchBoomerangAttack()
        {
            if (boomerangPrefab == null)
            {
                LogManager.LogWarning("부메랑 프리팹이 할당되지 않았습니다.", LogManager.LogCategory.Weapon, this);
                return;
            }
            
            var objectPooler = VamserLikeGameManager.Instance.objectPoolSpawner;
            if (objectPooler == null)
            {
                LogManager.LogError("ObjectPoolSpawner를 찾을 수 없습니다.", LogManager.LogCategory.Weapon, this);
                return;
            }

            float angleStep = 360f / boomerangCount;
    
            for (int i = 0; i < boomerangCount; i++)
            {
                float angle = i * angleStep;
                Vector3 direction = Quaternion.Euler(0, 0, angle) * Vector3.up;
        
                // 오브젝트 풀에서 부메랑 스폰
                GameObject boomerang = objectPooler.SpawnObject(boomerangPrefab, transform.position, Quaternion.Euler(0, 0, angle));
                if (boomerang == null) continue;
        
                // 이동 시퀀스
                Sequence moveSequence = DOTween.Sequence();
                Vector3 targetPosition = transform.position + (direction * boomerangDistance);
        
                moveSequence.Append(boomerang.transform.DOMove(targetPosition, boomerangDistance / boomerangSpeed).SetEase(Ease.OutQuad))
                    .AppendInterval(returnDelay)
                    .Append(boomerang.transform.DOMove(transform.position, boomerangDistance / boomerangSpeed).SetEase(Ease.InQuad))
                    .OnComplete(() =>
                    {
                        // 사용이 끝난 부메랑을 풀에 반환
                        objectPooler.ReturnObject(boomerang);
                    });
        
                // 회전 트윈 (이동 시간 동안만 실행)
                float totalRotations = moveSequence.Duration() * boomerangRotationsPerSecond;
                Tween rotateTween = boomerang.transform.DORotate(new Vector3(0, 0, 360f * totalRotations), moveSequence.Duration(), RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear);
        
                // 트윈들을 게임 오브젝트에 연결하여 라이프사이클 관리
                moveSequence.SetTarget(boomerang);
                rotateTween.SetTarget(boomerang);
            }
        }

        #endregion
    }
}