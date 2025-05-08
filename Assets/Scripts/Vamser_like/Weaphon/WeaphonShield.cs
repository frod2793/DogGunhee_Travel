using DG.Tweening;
using UnityEngine;

namespace DogGuns_Games.vamsir
{
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
        
        [SerializeField] bool isUpgradelv1 = false;
        
        [Header("부메랑 공격 설정")]
        [SerializeField] private GameObject boomerangPrefab;
        [SerializeField] private float boomerangSpeed = 5f;
        [SerializeField] private float boomerangDistance = 3f;
        [SerializeField] private float returnDelay = 0.1f;

        private readonly int boomerangCount = 5;
        private GameObject[] boomerangs;
        private Tween[] boomerangTweens;

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
            _shieldTween?.Kill();
        }

        #endregion

        #region 무기 동작 관리

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            base.Weaphon_Attack(attackAngle);
            Shieldmove_anime();
        }

        #endregion

        #region 애니메이션 및 이펙트

        private void Shieldmove_anime()
        {
            // 중복 실행 방지
            if (_isAnimShield)
                return;

            _isAnimShield = true;

            // 초기 상태 설정
            _shieldRenderer.enabled = false;
            shieldCollider.enabled = false;

            // 기존 Tween 정리
            _shieldTween?.Kill();

            // 목표 위치 도달 여부 체크를 위한 변수
            bool hasReachedDestination = false;

            bool activeshield = true;
            
            _shieldTween = shield.transform.DOLocalMove(_endPosition, 0.5f)
                .SetEase(Ease.OutBounce)
                .From(_startPosition)
                .OnUpdate(() => 
                {
                    // 목표 위치에 한 번만 도달했는지 확인
                    if (!hasReachedDestination)
                    {
                        activeshield = shield.transform.localPosition == _endPosition;
                        
                        // 목표 위치에 도달했을 때 (한 번만 실행)
                        if (activeshield)
                        {
                            hasReachedDestination = true;
                            shieldCollider.enabled = true;
                            _shieldRenderer.enabled = true;
                            if (isUpgradelv1)
                            {
                                WideAttack();
                            }
                        }
                    }
                })
                .OnComplete(() =>
                {
                    // 애니메이션 종료 후 정리
                    DOVirtual.DelayedCall(0.02f, () => 
                    {
                        shieldCollider.enabled = false;
                        _shieldRenderer.enabled = false;
                        _isAnimShield = false;
                    });
                });
        }
        
        private void WideAttack()
        {
            // 초기화 확인
            InitBoomerangs();
    
            // 5방향으로 발사 (원형 배치)
            float angleStep = 360f / boomerangCount;
            float startAngle = 0f;
    
            for (int i = 0; i < boomerangCount; i++)
            {
                float angle = startAngle + (i * angleStep);
                Vector3 direction = Quaternion.Euler(0, 0, angle) * Vector3.up;
        
                GameObject boomerang = boomerangs[i];
                boomerang.transform.position = transform.position;
                boomerang.transform.rotation = Quaternion.Euler(0, 0, angle);
                boomerang.SetActive(true);
        
                // 이전 Tween 종료
                boomerangTweens[i]?.Kill();
        
                // 새 Tween 시퀀스 생성 (발사 → 대기 → 귀환)
                Sequence sequence = DOTween.Sequence();
        
                // 발사 단계
                Vector3 targetPosition = transform.position + (direction * boomerangDistance);
                sequence.Append(boomerang.transform.DOMove(targetPosition, boomerangDistance / boomerangSpeed)
                    .SetEase(Ease.OutQuad));
        
                // 대기 단계
                sequence.AppendInterval(returnDelay);
        
                // 귀환 단계
                sequence.Append(boomerang.transform.DOMove(transform.position, boomerangDistance / boomerangSpeed)
                    .SetEase(Ease.InQuad));
        
                // 회전 효과 추가
                boomerang.transform.DORotate(new Vector3(0, 0, 360f * 5), sequence.Duration(), RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1);
        
                // 완료 후 처리
                sequence.OnComplete(() => 
                {
                    boomerang.SetActive(false);
                    boomerang.transform.DOKill();
                });
        
                boomerangTweens[i] = sequence;
            }
        }

        #endregion

        #region 초기화 및 설정

        private void InitBoomerangs()
        {
            if (boomerangs == null)
            {
                boomerangs = new GameObject[boomerangCount];
                boomerangTweens = new Tween[boomerangCount];
        
                for (int i = 0; i < boomerangCount; i++)
                {
                    boomerangs[i] = Instantiate(boomerangPrefab, transform);
                    boomerangs[i].SetActive(false);
                }
            }
        }

        #endregion
    }
}