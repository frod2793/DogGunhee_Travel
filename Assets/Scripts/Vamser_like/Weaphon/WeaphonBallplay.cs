using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 플레이어 주변을 회전하는 공으로 적에게 지속적인 피해를 주는 무기입니다.
    /// Weaphon_base의 attackSpeed를 회전 속도로 사용하며, 레벨업 시 이 값을 조절하여 속도를 높일 수 있습니다.
    /// </summary>
    public class WeaphonBallplay : Weaphon_base
    {
        [Header("공 설정")]
        [Tooltip("회전하는 공의 프리팹. BallDamageDealer 컴포넌트가 있어야 합니다.")]
        [SerializeField] private GameObject ballPrefab;

        [Tooltip("생성할 공의 개수")]
        [SerializeField] private int ballCount = 2;

        [Tooltip("플레이어로부터 공까지의 회전 반경")]
        [SerializeField] private float rotationRadius = 2.5f;

        private float _currentAngle = 0f;
        
        // 오브젝트 풀링을 위한 필드
        private IObjectPool<BallDamageDealer> _ballPool;
        private readonly List<BallDamageDealer> _activeBalls = new List<BallDamageDealer>();

        private void Awake()
        {
            // 오브젝트 풀 초기화
            _ballPool = new ObjectPool<BallDamageDealer>(
                createFunc: CreateBall,
                actionOnGet: OnGetBall,
                actionOnRelease: OnReleaseBall,
                actionOnDestroy: OnDestroyBall,
                maxSize: ballCount * 2 // 예상 최대 개수보다 넉넉하게 설정
            );
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            
            _currentAngle = 0f; // 회전 각도 초기화
            ClearBalls(); // 이전 게임의 공이 남아있을 경우 풀에 반환
            
            if (ballPrefab != null)
            {
                SpawnBalls();
            }
            else
            {
                LogManager.LogError("Ball Prefab이 할당되지 않았습니다!", LogManager.LogCategory.Weapon, this);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ClearBalls();
        }

        private void OnDestroy()
        {
            // 오브젝트가 파괴될 때 풀도 함께 정리합니다.
            // IObjectPool<T> 인터페이스에는 Dispose가 없으므로, IDisposable로 캐스팅하여 호출합니다.
            if (_ballPool is System.IDisposable disposablePool)
            {
                disposablePool.Dispose();
            }
        }

        private void Update()
        {
            // 이 오브젝트는 더 이상 회전하지 않습니다. 대신 각 공의 위치를 직접 계산하여 업데이트합니다.

            // 1. 플레이어의 좌우 반전에 따른 회전 방향 보정
            float rotationDirectionCorrection = Mathf.Sign(transform.forward.z);

            // 2. 회전 각도 업데이트 (시계 방향: 음수)
            _currentAngle += -attackSpeed * rotationDirectionCorrection * Time.deltaTime;

            if (_activeBalls.Count == 0) return;

            // 3. 각 공의 위치를 새로운 각도에 맞춰 재계산
            float angleStep = 360f / _activeBalls.Count;
            for (int i = 0; i < _activeBalls.Count; i++)
            {
                float angle = _currentAngle + (i * angleStep);
                Vector3 newPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0) * rotationRadius;
                _activeBalls[i].transform.localPosition = newPosition;
            }
        }

        private void SpawnBalls()
        {
            float angleStep = 360f / ballCount;

            for (int i = 0; i < ballCount; i++)
            {
                var ball = _ballPool.Get();
                // OnGetBall에서 초기화가 처리됩니다.
            }
        }

        private void ClearBalls()
        {
            // 활성화된 모든 공을 풀에 반환합니다.
            foreach (var ball in _activeBalls)
            {
                _ballPool.Release(ball);
            }
            _activeBalls.Clear();
        }

        #region Object Pool Methods

        private BallDamageDealer CreateBall()
        {
            GameObject ballInstance = Instantiate(ballPrefab, transform);
            return ballInstance.GetComponent<BallDamageDealer>();
        }

        private void OnGetBall(BallDamageDealer ball)
        {
            ball.Initialize(this);
            ball.gameObject.SetActive(true);
            _activeBalls.Add(ball);
        }

        private void OnReleaseBall(BallDamageDealer ball)
        {
            ball.gameObject.SetActive(false);
        }

        private void OnDestroyBall(BallDamageDealer ball)
        {
            Destroy(ball.gameObject);
        }

        #endregion
    }
}
