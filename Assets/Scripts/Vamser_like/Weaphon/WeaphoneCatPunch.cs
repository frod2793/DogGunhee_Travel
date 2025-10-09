using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using DG.Tweening;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 플레이어의 이동 방향으로 뻗어나가는 찌르기 공격을 실행하는 무기입니다.
    /// 특정 장신구 착용 시, 찌르기 공격이 광범위한 베기 공격으로 업그레이드됩니다.
    /// </summary>
    public class WeaphoneCatPunch : Weaphon_base
    {
        [Header("고양이 펀치 고유 스탯")]
        [SerializeField] private float initialAttackPower = 20f;
        [SerializeField] private float initialCoolTime = 1.5f;
        [SerializeField] private float initialMobStunTime = 0.3f;

        [Header("찌르기 공격 설정")]
        [SerializeField] private float pierceDistance = 3f;
        [SerializeField] private float pierceDuration = 0.3f; // 찌르고 돌아오는 총 시간
        [Tooltip("스프라이트가 오른쪽을 바라보지 않을 경우 회전 보정값 (e.g., 위를 보면 -90)")]
        [SerializeField] private float rotationOffset = 0f;

        [Header("업그레이드: 베기 공격 설정")]
        [SerializeField] private float slashAngle = 180f; // 베기 공격의 부채꼴 범위(각도)
        [SerializeField] private float slashRange = 1.5f; // 베기 시 앞으로 뻗는 사거리
        [SerializeField] private float slashDuration = 0.4f; // 베기 공격의 지속 시간

        private bool _isAttacking;
        private Collider2D _collider2D;
        private Vector3 _originalLocalPosition;
        private LineRenderer _slashTrailRenderer;
        private Color _originalTrailStartColor;
        private Color _originalTrailEndColor;
        private EdgeCollider2D _edgeCollider; // 라인 렌더러 경로를 따를 콜라이더

        // 최적화를 위한 필드
        private readonly HashSet<VamserMobBase> _hitMobsThisAttack = new HashSet<VamserMobBase>();
        private const int MAX_TRAIL_VERTICES = 100; // 궤적의 최대 정점 수
        private readonly Vector3[] _linePositions = new Vector3[MAX_TRAIL_VERTICES];
        private readonly Vector2[] _colliderPoints = new Vector2[MAX_TRAIL_VERTICES];


        private void Awake()
        {
            _collider2D = GetComponent<Collider2D>();
            if (_collider2D != null)
            {
                _collider2D.enabled = false;
            }
            _originalLocalPosition = transform.localPosition;

            _slashTrailRenderer = GetComponent<LineRenderer>();
            if (_slashTrailRenderer != null)
            {
                _slashTrailRenderer.enabled = false;
                _originalTrailStartColor = _slashTrailRenderer.startColor;
                _originalTrailEndColor = _slashTrailRenderer.endColor;
            }

            _edgeCollider = GetComponent<EdgeCollider2D>();
            if (_edgeCollider != null)
            {
                _edgeCollider.enabled = false;
            }
        }

        public override void OnEnable()
        {
            base.OnEnable(); // 기본 상태를 Idle로 설정

            // 인스펙터에서 설정한 초기 스탯을 Weaphon_base의 public 필드에 할당합니다.
            attackPower = initialAttackPower;
            coolTime = initialCoolTime;
            mobStunTime = initialMobStunTime;
            
            // 오브젝트 풀에서 재사용될 경우를 대비하여 상태를 초기화합니다.
            _isAttacking = false;

            // 모든 DOTween 애니메이션을 중지하고 상태를 리셋합니다.
            transform.DOKill();
            transform.localPosition = _originalLocalPosition;
            transform.localRotation = Quaternion.identity;

            if (_collider2D != null) _collider2D.enabled = false;
            if (_slashTrailRenderer != null)
            {
                _slashTrailRenderer.enabled = false;
                _slashTrailRenderer.startColor = _originalTrailStartColor;
                _slashTrailRenderer.endColor = _originalTrailEndColor;
            }
            if (_edgeCollider != null)
            {
                _edgeCollider.enabled = false;
                _edgeCollider.points = new Vector2[0];
            }
        }

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            if (_isAttacking) return;
           
            if (isUpgradelv2)
            {
                SlashAttackAsync(attackAngle).Forget();
            }
            else
            {
                StabAttackAsync(attackAngle).Forget();
            }
        }

        /// <summary>
        /// 기본 찌르기 공격을 수행합니다.
        /// </summary>
        private async UniTaskVoid StabAttackAsync(Vector3 attackAngle)
        {
            _isAttacking = true;
            var cts = this.GetCancellationTokenOnDestroy();

            try
            {
                float angle = Mathf.Atan2(attackAngle.y, attackAngle.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle + rotationOffset);

                if (_collider2D != null) _collider2D.enabled = true;

                // 공격 방향(월드)을 부모의 로컬 방향으로 변환하여 이동 계산의 정확성을 보장합니다.
                Vector3 localAttackDirection = transform.parent.InverseTransformDirection(attackAngle.normalized);
                Vector3 targetPosition = _originalLocalPosition + localAttackDirection * pierceDistance;
                
                var stabSequence = DOTween.Sequence()
                    .Append(transform.DOLocalMove(targetPosition, pierceDuration / 2).SetEase(Ease.OutCubic))
                    .Append(transform.DOLocalMove(_originalLocalPosition, pierceDuration / 2).SetEase(Ease.InCubic));
                
                cts.Register(() => stabSequence?.Kill());
                await UniTask.WaitUntil(() => !stabSequence.IsActive(), cancellationToken: cts);

                if (_collider2D != null) _collider2D.enabled = false;

                // 쿨타임 대기
                await UniTask.Delay(TimeSpan.FromSeconds(coolTime), cancellationToken: cts);
            }
            catch (OperationCanceledException)
            {
                // 오브젝트가 파괴되어 작업이 취소된 경우, finally 블록에서 정리합니다.
            }
            finally
            {
                _isAttacking = false;
                // 오브젝트가 파괴되었을 수 있으므로, 유효한지 확인 후 멤버에 접근합니다.
                if (this != null)
                {
                    // 작업이 중간에 취소되더라도 위치와 콜라이더를 안전하게 초기 상태로 되돌립니다.
                    transform.localPosition = _originalLocalPosition;
                    if (_collider2D != null) _collider2D.enabled = false;
                }
            }
        }

        /// <summary>
        /// 업그레이드된 베기 공격을 수행합니다.
        /// </summary>
        private async UniTaskVoid SlashAttackAsync(Vector3 attackAngle)
        {
            _isAttacking = true;
            _hitMobsThisAttack.Clear(); // 새 공격 시작 시, 피격 몹 목록 초기화
            var cts = this.GetCancellationTokenOnDestroy();

            try
            {
                if (_collider2D != null) _collider2D.enabled = true;
                if (_slashTrailRenderer != null)
                {
                    // 공격 시작 시, 궤적의 색상과 상태를 원본으로 확실하게 리셋합니다.
                    _slashTrailRenderer.startColor = _originalTrailStartColor;
                    _slashTrailRenderer.endColor = _originalTrailEndColor;
                    _slashTrailRenderer.positionCount = 0; // 궤적 초기화
                    _slashTrailRenderer.enabled = true;
                }
                if (_edgeCollider != null)
                {
                    _edgeCollider.points = new Vector2[0]; // 콜라이더 초기화
                    _edgeCollider.enabled = true;
                }

                // 1. 뻗어나갈 방향과 위치를 부모의 로컬 공간 기준으로 계산합니다.
                Vector3 localReachDirection = transform.parent.InverseTransformDirection(attackAngle.normalized);
                Vector3 reachPosition = _originalLocalPosition + localReachDirection * slashRange;

                // 2. '뻗기 -> 베기 -> 복귀' 동작을 DOTween 시퀀스로 생성합니다.
                var sequence = DOTween.Sequence();

                // 2a. 앞으로 뻗기
                sequence.Append(transform.DOLocalMove(reachPosition, slashDuration * 0.2f).SetEase(Ease.OutSine));

                // 2b. 부채꼴로 베기
                // 로컬 좌표계 기준으로 각도를 계산하여 플레이어의 회전에 영향을 받지 않도록 합니다.
                float localCenterAngle = Mathf.Atan2(localReachDirection.y, localReachDirection.x) * Mathf.Rad2Deg;
                float localStartAngle = localCenterAngle + (slashAngle / 2);

                // 베기 이펙트를 재생합니다.
                EffectManager.Instance.PlayEffect(EffectType.WeaponSlash, transform.position, Quaternion.Euler(0, 0, localCenterAngle));

                float localEndAngle = localCenterAngle - (slashAngle / 2);
                float radius = reachPosition.magnitude;

                var slashTween = DOTween.To(
                    () => localStartAngle, // getter: 시작 각도
                    (float angle) =>       // setter: angle 타입을 float으로 명시하여 모호성 해결
                    {
                        float rad = angle * Mathf.Deg2Rad;
                        Vector3 newLocalPos = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius;
                        transform.localPosition = newLocalPos;
                        transform.localRotation = Quaternion.Euler(0, 0, angle + rotationOffset);

                        // LineRenderer에 현재 무기 위치를 월드 좌표로 추가합니다.
                        if (_slashTrailRenderer != null)
                        {
                            int currentPositionCount = _slashTrailRenderer.positionCount;
                            if (currentPositionCount < MAX_TRAIL_VERTICES)
                            {
                                _slashTrailRenderer.positionCount = currentPositionCount + 1;
                                _slashTrailRenderer.SetPosition(currentPositionCount, transform.position);

                                // EdgeCollider2D도 GC 할당 없이 업데이트합니다.
                                _colliderPoints[currentPositionCount] = transform.localPosition;
                                // pointCount는 setter가 없으므로, points 배열을 새로 할당해야 합니다.
                                // ArraySegment를 사용하여 필요한 부분만 잘라내 새 배열을 만듭니다.
                                _edgeCollider.points = new ArraySegment<Vector2>(_colliderPoints, 0, currentPositionCount + 1).ToArray();
                            }
                        }
                    },
                    localEndAngle,         // endValue: 종료 각도
                    slashDuration * 0.6f
                ).SetEase(Ease.InOutQuad);
                
                sequence.Append(slashTween);

                // 2c. 원래 위치로 복귀
                // 위치와 회전을 동시에 원래 상태로 되돌려 부드러운 복귀 동작을 만듭니다.
                sequence.Append(transform.DOLocalMove(_originalLocalPosition, slashDuration * 0.2f).SetEase(Ease.InSine));
                sequence.Join(transform.DOLocalRotateQuaternion(Quaternion.identity, slashDuration * 0.2f).SetEase(Ease.InSine));

                cts.Register(() => sequence?.Kill());
                await UniTask.WaitUntil(() => !sequence.IsActive(), cancellationToken: cts);

                // 공격 동작이 끝나면 쿨타임과 궤적 페이드아웃을 동시에 시작합니다.
                var coolTimeTask = UniTask.Delay(TimeSpan.FromSeconds(coolTime), cancellationToken: cts);
                var trailFadeTask = FadeOutTrailAsync(cts);

                await UniTask.WhenAll(coolTimeTask, trailFadeTask);
            }
            catch (OperationCanceledException)
            {
                // 오브젝트가 파괴되어 작업이 취소된 경우, finally 블록에서 정리합니다.
            }
            finally
            {
                _isAttacking = false;
                _hitMobsThisAttack.Clear();
                // 오브젝트가 파괴되지 않았을 경우에만 상태를 리셋합니다.
                if (this != null)
                {
                    if (_collider2D != null) _collider2D.enabled = false;
                    
                    if (_edgeCollider != null) _edgeCollider.enabled = false;
                    if (_slashTrailRenderer != null) _slashTrailRenderer.enabled = false;

                    transform.localPosition = _originalLocalPosition;
                    transform.localRotation = Quaternion.identity;
                }
            }
        }

        /// <summary>
        /// 궤적을 부드럽게 사라지게 하는 비동기 메서드입니다.
        /// </summary>
        private async UniTask FadeOutTrailAsync(CancellationToken token)
        {
            if (_slashTrailRenderer == null || !_slashTrailRenderer.enabled)
            {
                return;
            }

            var fadeDuration = 0.3f; // 사라지는 데 걸리는 시간

            // 현재 색상에서 투명한 색상으로 변경하는 트윈을 생성합니다.
            var fadeTween = _slashTrailRenderer.DOColor(
                new Color2(_slashTrailRenderer.startColor, _slashTrailRenderer.endColor),
                new Color2(Color.clear, Color.clear),
                fadeDuration
            );

            token.Register(() => fadeTween?.Kill());
            await UniTask.WaitUntil(() => !fadeTween.IsActive(), cancellationToken: token);

            // 페이드아웃이 완료되면 LineRenderer를 비활성화합니다.
            if (this != null && _slashTrailRenderer != null)
            {
                _slashTrailRenderer.enabled = false;
            }
        }

        /// <summary>
        /// 공격 콜라이더가 몹과 충돌했을 때 호출됩니다.
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            // 공격 중이 아니거나, 몹이 아니거나, 이미 이번 공격에서 맞은 몹이면 무시합니다.
            if (!_isAttacking || !other.CompareTag("Mob")) return;

            if (other.TryGetComponent<VamserMobBase>(out var mob) && _hitMobsThisAttack.Add(mob))
            {
                // HashSet.Add는 항목이 성공적으로 추가되었을 때 true를 반환하므로, 중복 피격을 방지합니다.
                mob.TakeDamage(attackPower, mobStunTime);
            }
        }
    }
}