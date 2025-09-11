using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
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

        [Header("업그레이드: 베기 공격 설정")]
        [SerializeField] private bool isUpgraded = false;
        [SerializeField] private float slashArc = 180f; // 베기 공격의 각도
        [SerializeField] private float slashDuration = 0.4f; // 베기 공격의 지속 시간

        private bool _isAttacking;
        private Collider2D _collider2D;
        private Vector3 _originalLocalPosition;

        private void Awake()
        {
            _collider2D = GetComponent<Collider2D>();
            if (_collider2D != null)
            {
                _collider2D.enabled = false;
            }
            _originalLocalPosition = transform.localPosition;
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
            if (transform != null) transform.localPosition = _originalLocalPosition;
            if (_collider2D != null) _collider2D.enabled = false;
            transform.DOKill(); // 이전 사용에서 남은 DOTween 애니메이션을 모두 중지합니다.
        }

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            if (_isAttacking) return;
            // base.Weaphon_Attack(attackAngle); // 베이스 클래스의 상태 머신과 충돌할 수 있으므로 호출하지 않습니다.

            if (isUpgraded)
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
                transform.rotation = Quaternion.Euler(0, 0, angle);

                if (_collider2D != null) _collider2D.enabled = true;

                Vector3 targetPosition = _originalLocalPosition + (attackAngle.normalized * pierceDistance);
                
                await DOTween.Sequence()
                    .Append(transform.DOLocalMove(targetPosition, pierceDuration / 2).SetEase(Ease.OutCubic))
                    .Append(transform.DOLocalMove(_originalLocalPosition, pierceDuration / 2).SetEase(Ease.InCubic))
                    .WithCancellation(cts);

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
                // 작업이 중간에 취소되더라도 위치와 콜라이더를 안전하게 초기 상태로 되돌립니다.
                if (transform != null) transform.localPosition = _originalLocalPosition;
                if (_collider2D != null) _collider2D.enabled = false;
            }
        }

        /// <summary>
        /// 업그레이드된 베기 공격을 수행합니다.
        /// </summary>
        private async UniTaskVoid SlashAttackAsync(Vector3 attackAngle)
        {
            _isAttacking = true;
            var cts = this.GetCancellationTokenOnDestroy();

            try
            {
                if (_collider2D != null) _collider2D.enabled = true;

                float startAngle = Mathf.Atan2(attackAngle.y, attackAngle.x) * Mathf.Rad2Deg - (slashArc / 2);
                transform.localRotation = Quaternion.Euler(0, 0, startAngle);

                await transform.DOLocalRotate(new Vector3(0, 0, startAngle + slashArc), slashDuration, RotateMode.Fast)
                    .SetEase(Ease.OutQuad)
                    .WithCancellation(cts);

                if (_collider2D != null) _collider2D.enabled = false;
                transform.localRotation = Quaternion.identity;

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
                if (_collider2D != null) _collider2D.enabled = false;
            }
        }
    }
}