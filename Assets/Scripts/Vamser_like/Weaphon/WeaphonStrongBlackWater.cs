using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 지속적으로 틱 데미지를 주는 웅덩이를 생성하는 무기입니다.
    /// 특정 장신구 장착 시, 범위가 증가하고 적에게 슬로우 효과를 부여합니다.
    /// </summary>
    public class WeaphonStrongBlackWater : Weaphon_base
    {
        #region 필드 및 변수

        [Header("기본 공격 설정")]
        [Tooltip("공격이 지속되는 시간입니다.")]
        [SerializeField] private float attackDuration = 3f;
        [Tooltip("틱 데미지가 들어가는 간격입니다.")]
        [SerializeField] private float damageTickInterval = 0.5f;

        [Header("업그레이드 설정 (장신구)")]
        [Tooltip("업그레이드 활성화 여부입니다.")]
        [SerializeField] private bool isUpgraded = false;
        [Tooltip("업그레이드 시 공격 범위 증가 배율입니다.")]
        [SerializeField] private float rangeMultiplier = 1.5f;
        [Tooltip("적의 이동 속도를 감소시키는 비율입니다. (0.3 = 30% 감소)")]
        [SerializeField] [Range(0f, 1f)] private float slowAmount = 0.3f;
        [Tooltip("슬로우 효과가 지속되는 시간입니다.")]
        [SerializeField] private float slowDuration = 1.0f;

        private bool _isAttacking; // 중복 호출 방지 플래그
        private Collider2D _collider2D;
        private Vector3 _originalScale;
        private readonly List<VamserMobBase> _mobsInRange = new List<VamserMobBase>();
        private CancellationTokenSource _attackCts;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            _collider2D = GetComponent<Collider2D>();
            _originalScale = transform.localScale;
            _collider2D.enabled = false;
        }

        public override void OnEnable()
        {
            base.OnEnable();
            mobStunTime = 0.5f;
        }

        private void OnDisable()
        {
            // 오브젝트 비활성화 시 진행중인 공격 로직을 안전하게 취소합니다.
            // Dispose는 작업을 시작한 ActivateBlackWater가 담당하므로, 여기서는 Cancel 신호만 보냅니다.
            _attackCts?.Cancel();
            transform.DOKill(); // 스케일 트윈이 있을 경우를 대비
        }

        #endregion

        #region 무기 동작 관리

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            base.Weaphon_Attack(attackAngle);

            if (!_isAttacking)
            {
                ActivateBlackWater().Forget();
            }
        }

        #endregion

        #region 공격 구현

        private async UniTask ActivateBlackWater()
        {
            _isAttacking = true;
            
            // 이 공격 실행의 생명주기를 관리하는 CancellationTokenSource를 생성합니다.
            var cts = new CancellationTokenSource();
            _attackCts = cts; // OnDisable에서 참조할 수 있도록 필드에 할당합니다.

            try
            {
                // 업그레이드 시 범위 증가
                if (isUpgraded)
                {
                    transform.localScale = _originalScale * rangeMultiplier;
                }

                _collider2D.enabled = true;
                // TODO: 웅덩이 생성 비주얼 이펙트 (예: transform.DOScale, DOFade 등)

                // 틱 데미지 루프 시작 (오브젝트 파괴 시 함께 취소되도록 링크)
                var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, this.GetCancellationTokenOnDestroy()).Token;
                DealTickDamageLoop(linkedToken).Forget();

                // 공격 지속 시간만큼 대기
                await UniTask.Delay(TimeSpan.FromSeconds(attackDuration), cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException)
            {
                // UniTask.Delay가 오브젝트 파괴로 인해 취소된 경우. 정상적인 흐름입니다.
                // finally 블록에서 정리 작업이 수행되므로, 여기서는 아무것도 할 필요가 없습니다.
                return;
            }
            finally
            {
                // 성공, 예외, 취소 등 어떤 경우에도 반드시 실행되는 정리 블록입니다.
                cts.Cancel(); // 틱 데미지 루프 등 이 공격과 관련된 모든 작업을 취소합니다.

                _collider2D.enabled = false;
                _mobsInRange.Clear(); // 범위 내 몹 리스트 초기화
                transform.localScale = _originalScale; // 원래 크기로 복원
                // TODO: 웅덩이 소멸 비주얼 이펙트

                // 이 공격에 사용된 CTS를 정리합니다.
                if (_attackCts == cts) _attackCts = null;
                cts.Dispose();
            }

            // 재공격 쿨타임
            // 공격이 정상적으로 완료된 후, 재공격 쿨타임을 시작합니다.
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(coolTime), cancellationToken: this.GetCancellationTokenOnDestroy());
                _isAttacking = false;
            }
            catch (OperationCanceledException)
            {
                // 쿨타임 중 오브젝트가 파괴된 경우
                _isAttacking = false;
            }
        }

        private async UniTask DealTickDamageLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // 리스트의 복사본을 만들어 순회 (순회 중 리스트 변경에 따른 오류 방지)
                var mobsToDamage = new List<VamserMobBase>(_mobsInRange);
                foreach (var mob in mobsToDamage)
                {
                    if (mob != null && !mob.IsDead)
                    {
                        // VamserMobBase에 TakeDamage(float) 메서드가 필요합니다.
                        mob.TakeDamage(attackPower);

                        // 업그레이드 시 슬로우 효과 적용
                        if (isUpgraded)
                        {
                            // VamserMobBase에 ApplySlow(float, float) 메서드가 필요합니다.
                            mob.ApplySlow(slowAmount, slowDuration);
                        }
                    }
                }
                await UniTask.Delay(TimeSpan.FromSeconds(damageTickInterval), cancellationToken: token);
            }
        }

        #endregion

        #region 충돌 처리

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<VamserMobBase>(out var mob) && !_mobsInRange.Contains(mob))
            {
                _mobsInRange.Add(mob);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.TryGetComponent<VamserMobBase>(out var mob))
            {
                _mobsInRange.Remove(mob);
            }
        }

        #endregion
    }
}