using UnityEngine;
using System.Collections.Generic;

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
        private readonly List<GameObject> _spawnedBalls = new List<GameObject>();

        public override void OnEnable()
        {
            base.OnEnable();
            
            _currentAngle = 0f; // 회전 각도 초기화
            ClearBalls();
            
            if (ballPrefab != null)
            {
                SpawnBalls();
            }
            else
            {
                LogManager.LogError("Ball Prefab이 할당되지 않았습니다!", LogManager.LogCategory.Weapon, this);
            }
        }

        public override void OnDisable()
        {
            base.OnDisable();
            ClearBalls();
        }

        private void Update()
        {
            // 이 오브젝트는 더 이상 회전하지 않습니다. 대신 각 공의 위치를 직접 계산하여 업데이트합니다.

            // 1. 플레이어의 좌우 반전에 따른 회전 방향 보정
            float rotationDirectionCorrection = Mathf.Sign(transform.forward.z);

            // 2. 회전 각도 업데이트 (시계 방향: 음수)
            _currentAngle += -attackSpeed * rotationDirectionCorrection * Time.deltaTime;

            if (_spawnedBalls.Count == 0) return;

            // 3. 각 공의 위치를 새로운 각도에 맞춰 재계산
            float angleStep = 360f / _spawnedBalls.Count;
            for (int i = 0; i < _spawnedBalls.Count; i++)
            {
                float angle = _currentAngle + (i * angleStep);
                Vector3 newPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0) * rotationRadius;
                _spawnedBalls[i].transform.localPosition = newPosition;
            }
        }

        private void SpawnBalls()
        {
            float angleStep = 360f / ballCount;

            for (int i = 0; i < ballCount; i++)
            {
                float angle = i * angleStep;
                
                Vector3 spawnPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0) * rotationRadius;

                GameObject ball = Instantiate(ballPrefab, transform);
                ball.transform.localPosition = spawnPosition;
                
                if (ball.TryGetComponent<BallDamageDealer>(out var damageDealer))
                {
                    damageDealer.Initialize(attackPower, coolTime);
                }
                
                _spawnedBalls.Add(ball);
            }
        }

        private void ClearBalls()
        {
            foreach (var ball in _spawnedBalls)
            {
                if (ball != null) Destroy(ball);
            }
            _spawnedBalls.Clear();
        }
    }
}
