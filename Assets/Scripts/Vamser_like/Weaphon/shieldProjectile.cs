using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Pool;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// WeaphonShield에서 발사되는 부메랑 투사체의 동작을 관리합니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class shieldProjectile : MonoBehaviour
    {
        private IObjectPool<shieldProjectile> _pool;
        private Transform _playerTransform;

        // 부모의 참조 대신 능력치를 직접 저장하여 NullReferenceException을 방지합니다.
        private float _attackPower;
        private float _mobStunTime;

        private void Awake()
        {
            // 충돌 시 물리적 영향 없이 이벤트만 발생시키도록 트리거로 설정합니다.
            GetComponent<Collider2D>().isTrigger = true;
        }

        /// <summary>
        /// 부메랑을 초기화하고 애니메이션을 시작합니다.
        /// </summary>
        public void Initialize(IObjectPool<shieldProjectile> pool, float attackPower, float mobStunTime, Transform playerTransform, Vector3 direction, float speed, float distance, float returnDelay, float rotationsPerSecond)
        {
            _pool = pool;
            _playerTransform = playerTransform;
            _attackPower = attackPower;
            _mobStunTime = mobStunTime;
            AnimateBoomerangAsync(direction, speed, distance, returnDelay, rotationsPerSecond).Forget();
        }

        private async UniTaskVoid AnimateBoomerangAsync(Vector3 direction, float speed, float distance, float returnDelay, float rotationsPerSecond)
        {
            try
            {
                Vector3 originPosition = transform.position;
                float outwardDuration = distance / speed;
                float totalDuration = outwardDuration * 2 + returnDelay; // 대략적인 총 시간

                // 회전 애니메이션: 오브젝트가 비활성화될 때 자동으로 정리되도록 SetLink 사용
                transform.DORotate(new Vector3(0, 0, 360f * totalDuration * rotationsPerSecond), totalDuration, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLink(gameObject);

                // 1. 바깥으로 이동
                await transform.DOMove(originPosition + (direction * distance), outwardDuration)
                    .SetEase(Ease.OutQuad)
                    .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());

                // 2. 복귀 전 딜레이
                await UniTask.Delay(System.TimeSpan.FromSeconds(returnDelay), cancellationToken: this.GetCancellationTokenOnDestroy());

                // 3. 플레이어의 현재 위치를 동적으로 추적하며 복귀
                while (gameObject.activeInHierarchy && _playerTransform != null && Vector3.Distance(transform.position, _playerTransform.position) > 0.1f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, _playerTransform.position, speed * Time.deltaTime);
                    await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
                }
            }
            finally
            {
                // 애니메이션이 끝나거나 취소되면 풀에 반환합니다.
                if (gameObject.activeInHierarchy)
                {
                    _pool.Release(this);
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Mob") && other.TryGetComponent<MobBase>(out var mob))
            {
                if (!mob.IsDead)
                {
                    mob.TakeDamage(_attackPower, _mobStunTime);
                }
            }
        }
        
        private void OnDisable()
        {
            // 오브젝트가 비활성화될 때 모든 DOTween 애니메이션을 확실히 정리합니다.
            transform.DOKill();
        }
    }
}