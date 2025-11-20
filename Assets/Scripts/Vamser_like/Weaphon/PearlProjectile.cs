using UnityEngine;
namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 진주 투사체의 이동, 벽 반사, 적 충돌 로직을 관리합니다.
    /// 이 스크립트는 진주 프리팹에 부착되어야 합니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class PearlProjectile : Weaphon_base
    {
        private Rigidbody2D _rb;
        private Camera _mainCamera;
        

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            GetComponent<CircleCollider2D>().isTrigger = true; // 충돌 시 멈추지 않고 통과하도록 트리거로 설정
            _mainCamera = Camera.main;
        }

        /// <summary>
        /// 진주를 초기화하고 발사합니다.
        /// </summary>
        public void Initialize(Weaphon_base parentWeapon)
        {
            isUpgradelv2 = parentWeapon.isUpgradelv2;
            attackPower = parentWeapon.attackPower;
            mobStunTime = parentWeapon.mobStunTime;
            attackSpeed = parentWeapon.attackSpeed;
            
            
            // 랜덤한 초기 방향으로 발사
            Vector2 randomDirection = UnityEngine.Random.insideUnitCircle.normalized;
            _rb.linearVelocity = randomDirection * attackSpeed;
        }

        private void FixedUpdate()
        {
            // 카메라 시야 경계에 닿으면 반사
            BounceOffCameraView();
        }

        private void BounceOffCameraView()
        {
            if (_mainCamera == null) 
            {
                // 메인 카메라가 없는 경우에 대한 방어 코드
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            // 카메라의 월드 좌표 기준 경계를 계산합니다. (Orthographic 카메라 기준)
            float cameraHeight = _mainCamera.orthographicSize * 2;
            float cameraWidth = cameraHeight * _mainCamera.aspect;
            Vector3 cameraPosition = _mainCamera.transform.position;

            float minX = cameraPosition.x - cameraWidth / 2;
            float maxX = cameraPosition.x + cameraWidth / 2;
            float minY = cameraPosition.y - cameraHeight / 2;
            float maxY = cameraPosition.y + cameraHeight / 2;
            
            Vector2 currentVelocity = _rb.linearVelocity;
            Vector3 currentPosition = transform.position;

            // 위치가 경계를 넘어섰는지 확인하고 속도 방향을 반전시킵니다.
            if ((currentPosition.x <= minX && currentVelocity.x < 0) || (currentPosition.x >= maxX && currentVelocity.x > 0))
            {
                currentVelocity.x *= -1;
            }
            if ((currentPosition.y <= minY && currentVelocity.y < 0) || (currentPosition.y >= maxY && currentVelocity.y > 0))
            {
                currentVelocity.y *= -1;
            }

            _rb.linearVelocity = currentVelocity;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<VamserMobBase>(out var mob))
            {
                if (!mob.IsDead)
                {
                    // 진주가 몹에게 데미지를 입힙니다.
                    mob.TakeDamage(attackPower);
            
                    // 업그레이드 시 스턴 효과 적용
                    if (isUpgradelv2)
                    {
                        mob.StunTime = mobStunTime;
                        // [수정] 변수 직접 접근 대신 public 메서드 사용
                        mob.SetState(VamserMobBase.MobState.Stun); 
                    }
                }
            }
        }

        private void OnDisable()
        {
            // 풀로 돌아갈 때 물리력을 초기화하여 다음 사용에 영향을 주지 않도록 합니다.
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
            }
        }
    }
}