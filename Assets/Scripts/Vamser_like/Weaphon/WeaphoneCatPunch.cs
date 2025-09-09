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
        [Header("공격 설정")]
        [Tooltip("일반 찌르기 공격에 사용할 프리팹입니다.")]
        [SerializeField] private GameObject thrustPrefab;
        [Tooltip("공격 이펙트가 활성화되어 있는 시간입니다.")]
        [SerializeField] private float attackDuration = 0.3f;
        [Tooltip("한 번의 공격 시 찌르기를 반복하는 횟수입니다. 1로 설정하면 단일 찌르기입니다.")]
        [SerializeField, Range(1, 5)] private int thrustRepetitions = 2;
        [Tooltip("찌르기 반복 시 각 찌르기 사이의 짧은 딜레이(초)입니다.")]
        [SerializeField] private float delayBetweenThrusts = 0.05f;

        [Header("업그레이드 설정 (장신구)")]
        [Tooltip("업그레이드 활성화 여부입니다.")]
        [SerializeField] private bool isUpgraded = false;
        [Tooltip("업그레이드된 베기 공격에 사용할 프리팹입니다.")]
        [SerializeField] private GameObject slashPrefab;

        private bool _isAttacking; // 중복 공격 방지 플래그
        private VamPlayerControll _playerController;

        private void Awake()
        {
            // VamserLikeGameManager를 통해 플레이어 컨트롤러 참조를 가져옵니다.
            _playerController = VamserLikeGameManager.Instance?.vamPlayerControll;
        }

        public override void OnEnable()
        {
            base.OnEnable();
            _isAttacking = false;
        }

        public override void Weaphon_Attack(Vector3 attackAngle)
        {
            base.Weaphon_Attack(attackAngle);

            if (!_isAttacking)
            {
                ExecuteAttackAsync(attackAngle).Forget();
            }
        }

        private async UniTaskVoid ExecuteAttackAsync(Vector3 attackAngle)
        {
            _isAttacking = true;

            // 1. 공격 방향 결정
            Vector3 direction = GetAttackDirection(attackAngle);
            if (direction == Vector3.zero)
            {
                // 방향이 없으면 공격을 실행하지 않고 쿨타임만 적용
                await UniTask.Delay(TimeSpan.FromSeconds(coolTime), cancellationToken: this.GetCancellationTokenOnDestroy());
                _isAttacking = false;
                return;
            }

            // 2. 사용할 프리팹과 풀러 가져오기
            GameObject prefabToSpawn = isUpgraded ? slashPrefab : thrustPrefab;
            var objectPooler = VamserLikeGameManager.Instance.objectPoolSpawner;

            if (objectPooler == null || prefabToSpawn == null)
            {
                Debug.LogError("ObjectPooler 또는 공격 프리팹이 할당되지 않았습니다.");
                _isAttacking = false;
                return;
            }

            // 3. 공격 이펙트 스폰 및 애니메이션
            // 공격은 무기 위치에서 attackRange만큼 떨어진 곳에서 시작하고, 해당 방향으로 회전합니다.
            Vector3 spawnPosition = transform.position + (direction * attackRange);
            Quaternion spawnRotation = Quaternion.FromToRotation(Vector3.up, direction);

            GameObject attackInstance = objectPooler.SpawnObject(prefabToSpawn, spawnPosition, spawnRotation);

            if (attackInstance != null)
            {
                // 생성된 공격 이펙트를 무기의 자식으로 설정하여, 무기가 움직일 때 함께 따라가도록 합니다.
                attackInstance.transform.SetParent(transform);
                
                // 애니메이션을 위해 프리팹의 원본 스케일을 저장합니다.
                Vector3 originalScale = attackInstance.transform.localScale;
                
                Sequence sequence = DOTween.Sequence();

                if (isUpgraded)
                {
                    // 업그레이드 (베기): 프리팹을 0에서 원본 크기까지 전체적으로 빠르게 확대하고, attackDuration 동안 유지한 후, 빠르게 축소합니다.
                    float animInDuration = 0.1f;
                    float animOutDuration = 0.1f;
                    attackInstance.transform.localScale = Vector3.zero;
                    sequence.Append(attackInstance.transform.DOScale(originalScale, animInDuration).SetEase(Ease.OutBack))
                            .AppendInterval(attackDuration)
                            .Append(attackInstance.transform.DOScale(0f, animOutDuration).SetEase(Ease.InBack));
                }
                else
                {
                    // 기본 (찌르기 반복): 'attackDuration' 동안 'thrustRepetitions' 횟수만큼 빠르게 찌르는 동작을 반복합니다.
                    // 이를 위해 프리팹의 피벗은 공격 시작점에, 그래픽은 Y축 방향으로 길게 뻗어있어야 합니다.
                    attackInstance.transform.localScale = new Vector3(originalScale.x, 0, originalScale.z);

                    // 한 번의 찌르기(나갔다 들어오기)에 걸리는 시간을 계산합니다.
                    float singleThrustDuration = (attackDuration - (delayBetweenThrusts * (thrustRepetitions - 1))) / thrustRepetitions;

                    for (int i = 0; i < thrustRepetitions; i++)
                    {
                        sequence.Append(attackInstance.transform.DOScaleY(originalScale.y, singleThrustDuration / 2).SetEase(Ease.OutQuad));
                        sequence.Append(attackInstance.transform.DOScaleY(0, singleThrustDuration / 2).SetEase(Ease.InQuad));

                        // 마지막 찌르기가 아니면, 다음 찌르기 전 짧은 딜레이 추가
                        if (i < thrustRepetitions - 1)
                        {
                            sequence.AppendInterval(delayBetweenThrusts);
                        }
                    }
                }
                
                sequence.OnComplete(() =>
                        {
                            objectPooler.ReturnObject(attackInstance);
                        })
                        .SetTarget(attackInstance); // 트윈의 생명주기를 인스턴스에 연결
            }

            // 4. 다음 공격까지 쿨타임 대기
            await UniTask.Delay(TimeSpan.FromSeconds(coolTime), cancellationToken: this.GetCancellationTokenOnDestroy());
            _isAttacking = false;
        }

        /// <summary>
        /// 플레이어의 현재 이동 방향 또는 공격 방향(가까운 적)을 기반으로 공격 방향을 결정합니다.
        /// </summary>
        private Vector3 GetAttackDirection(Vector3 fallbackDirection)
        {
            if (_playerController != null && _playerController.MoveDirection != Vector3.zero)
            {
                // 플레이어가 움직이고 있다면, 현재 이동 방향을 사용합니다.
                return _playerController.MoveDirection.normalized;
            }
            
            // 플레이어가 멈춰있다면, 자동 공격 시스템이 지정한 방향(가장 가까운 적)을 사용합니다.
            return fallbackDirection.normalized;
        }
    }
}