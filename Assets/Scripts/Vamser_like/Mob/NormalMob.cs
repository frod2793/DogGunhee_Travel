using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace DogGuns_Games.vamsir
{
    public class NormalMob : VamserMobBase
    {
        [Header("<color=green>플레이어 무기")] 
        private Weaphon_base _playerWeaphon;

        //피격 물체가 발사체인지 구분
        private bool _isHitByShoot;

        [Header("몹 스탯")]
        [SerializeField] private float initialHp = 100f;
        [SerializeField] private float initialSpeed = 1f;
        [SerializeField] private float initialAttackDamage = 10f;
        [SerializeField] private float initialAttackSpeed = 1f;
        [SerializeField] private float initialAttackRange = 1f;
        [SerializeField] private float initialStunTime = 0.1f;

        [Header("<color=green>탐색 범위")]
        [SerializeField] private float searchRange = 8f;
        
        private Bounds _mapBounds;
        private Tween _wanderTween;
        private bool _isWaitingToWander; // 추가: 배회 사이의 대기 상태를 추적하는 플래그

        private enum AIState
        {
            Wandering,
            Chasing
        }
        private AIState _currentState;
        private SpriteRenderer _spriteRenderer;
        
        private void Awake()
        {
            DOTween.SetTweensCapacity(500, 50);
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            // 맵 오브젝트를 "Map" 태그로 찾고, SpriteRenderer의 bounds를 캐싱
            var mapObj = GameObject.FindGameObjectWithTag("Map");
            if (mapObj != null)
            {
                var mapRenderer = mapObj.GetComponent<SpriteRenderer>();
                if (mapRenderer != null)
                {
                    _mapBounds = mapRenderer.bounds;
                }
                else
                {
                    LogManager.LogWarning("Map object does not have a SpriteRenderer component.", LogManager.LogCategory.NormalMob, this);
                }
            }
            else
            {
                LogManager.LogWarning("Could not find GameObject with 'Map' tag.", LogManager.LogCategory.NormalMob, this);
            }
        }

        /// <summary>
        /// 몹이 스폰되거나 풀에서 재사용될 때 호출되어 상태를 초기화합니다.
        /// </summary>
        private void Initialize()
        {
            // 스탯 초기화
            Mob_Hp = initialHp;
            Mob_Speed = initialSpeed;
            Mob_AttackDamage = initialAttackDamage;
            Mob_AttackSpeed = initialAttackSpeed;
            Mob_AttackRange = initialAttackRange;
            Mob_StunTime = initialStunTime;
            
            // 상태 플래그 초기화
            Mob_IsDie = false;
            Mob_IsHit = false;
            ismove = false;

            // AI 상태 초기화
            transform.DOKill(); // 이전 트윈이 남아있을 경우를 대비해 확실히 제거
            _isWaitingToWander = false;
            _currentState = AIState.Wandering;

            // 플레이어 및 무기 참조 설정
            if (player != null)
            {
                _playerWeaphon = player.WeaphonBase;
                _isHitByShoot = _playerWeaphon != null && _playerWeaphon.isShooting;
            }
        }

        public override void SetTarget(PlayerBase target)
        {
            base.SetTarget(target);
            Initialize();
        }
   
        public override void OnEnable()
        {
            base.OnEnable();
            StartAIBehavior().Forget();
        }

        private void FixedUpdate()
        {
            // ismove는 Mob_Move, Mob_Stun 등 상태 변경 메서드에서 관리
            // playerTransform은 StartAIBehavior에서 유효성이 보장되므로, null 체크만으로 충분합니다.
            if (!ismove || playerTransform == null || Mob_IsDie)
            {
                return;
            }

            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        
            // --- 상태 전환 로직 ---
            if (_currentState == AIState.Wandering && distanceToPlayer <= searchRange)
            {
                // 배회 -> 추격
                _currentState = AIState.Chasing;
                _wanderTween?.Kill(); // 진행 중인 배회 움직임 즉시 중단
                _isWaitingToWander = false; // 대기 상태였다면 취소
                LogManager.Log("플레이어 감지, 추격을 시작합니다.", LogManager.LogCategory.NormalMob, this);
            }
            else if (_currentState == AIState.Chasing && distanceToPlayer > searchRange)
            {
                // 추격 -> 배회
                _currentState = AIState.Wandering;
                Wander(); // 배회 시작
                LogManager.Log("플레이어를 놓쳤습니다. 배회를 시작합니다.", LogManager.LogCategory.NormalMob, this);
            }
        
            // --- 상태별 행동 로직 ---
            if (_currentState == AIState.Chasing)
            {
                ChasePlayer();
            }
            else if (_currentState == AIState.Wandering)
            {
                // 배회 상태인데, 실제 움직임(Tween)도 없고 다음 배회를 위한 대기 상태도 아니라면 (예: 스턴 직후)
                // AI가 멈추는 것을 방지하기 위해 새로운 배회 행동을 시작시킵니다.
                bool isWanderTweenActive = _wanderTween != null && _wanderTween.IsActive();
                if (!isWanderTweenActive && !_isWaitingToWander)
                {
                    Wander();
                }
            }
        }

        #region AI Logic
        
        // 적 ai 로직 설명 
        // 스폰후 적의 현위치가 맵의 외부 인지 내부인지 검사 
        // 맵의 외부일시 맵 내부 까지 복귀 
        // 맵의 내부일시 플레이어 탐색을 위한 배회 
        // 플레이어 탐지 시 플레이어 추격 

        private async UniTask StartAIBehavior()
        {
            // 스포너가 player를 설정하고, 그 player가 부모 객체에 연결될 때까지 대기합니다.
            // 이것이 모든 초기화 순서 관련 레이스 컨디션의 근본적인 해결책입니다.
            await UniTask.WaitUntil(() => player != null && player.transform.parent != null, cancellationToken: this.GetCancellationTokenOnDestroy());

            // 이 시점에서는 player와 player.transform.parent가 모두 유효함이 보장됩니다.
            // SetTarget에서 이미 설정되었을 수 있지만, 여기서 다시 한번 확실하게 설정합니다.
            playerTransform = player.transform.parent;
            LogManager.Log("플레이어 참조 및 Transform 설정 완료. AI를 시작합니다.", LogManager.LogCategory.NormalMob, this);

            // 맵 외부에 있다면, 맵 내부로 이동
            if (_mapBounds.size != Vector3.zero && !_mapBounds.Contains(transform.position))
            {
                await MoveToMapBoundary();
            }

            // AI 상태 시작
            _currentState = AIState.Wandering;
            SetMobState(MobState.Move); // AI가 모든 준비를 마친 후 이동 상태로 전환
            Wander(); // 초기 배회 시작
        }

        private async UniTask MoveToMapBoundary()
        {
            Vector3 targetPosition = transform.position;
            targetPosition.x = Mathf.Clamp(targetPosition.x, _mapBounds.min.x, _mapBounds.max.x);
            targetPosition.y = Mathf.Clamp(targetPosition.y, _mapBounds.min.y, _mapBounds.max.y);
            targetPosition.z = 0f; // 2D 게임이므로 Z축을 0으로 고정합니다.
            
            float duration = Vector3.Distance(transform.position, targetPosition) / (Mob_Speed * 2); // 2배 빠른 속도로 복귀

            var tween = transform.DOMove(targetPosition, duration)
                .SetEase(Ease.Linear);

            await UniTask.WaitUntil(() => !tween.IsActive(), cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        
        private void ChasePlayer()
        {
            Vector3 direction = (playerTransform.position - transform.position).normalized;
            
            Vector3 newPosition = transform.position + direction * Mob_Speed * Time.fixedDeltaTime;
            newPosition.z = 0f; // 2D 게임이므로 Z축을 0으로 고정합니다.
            transform.position = newPosition;

            FlipTowards(direction);
        }

        private void Wander()
        {
            // 맵 내부에서 랜덤한 목적지 설정
            Vector3 randomDestination = GetRandomPositionInMap();
    
            Vector3 direction = (randomDestination - transform.position).normalized;
            FlipTowards(direction);
    
            float duration = Vector3.Distance(transform.position, randomDestination) / Mob_Speed;
            _wanderTween = transform.DOMove(randomDestination, duration)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    // 이동 완료 후, 현재 상태가 여전히 '배회'일 경우에만 다음 배회 예약
                    if (_currentState == AIState.Wandering)
                    {
                        WaitAndWander().Forget();
                    }
                });
        }

        private async UniTask WaitAndWander()
        {
            try
            {
                _isWaitingToWander = true; // 대기 상태 시작
                await UniTask.Delay(TimeSpan.FromSeconds(UnityEngine.Random.Range(1f, 3f)), cancellationToken: this.GetCancellationTokenOnDestroy());
                
                // Delay 이후에도 여전히 배회 상태여야 다음 행동을 시작
                if (_currentState == AIState.Wandering)
                {
                    _isWaitingToWander = false; // 대기 상태 종료
                    Wander(); // 상태가 바뀌지 않았다면 다음 배회 시작
                }
            }
            catch (OperationCanceledException)
            {
                /* 작업 취소는 정상 동작 */
            }
            finally
            {
                // 작업이 성공적으로 끝나거나, 취소되거나, 예외가 발생해도 대기 상태는 반드시 해제
                _isWaitingToWander = false;
            }
        }
        
        private Vector3 GetRandomPositionInMap()
        {
            if (_mapBounds.size == Vector3.zero) return transform.position;
    
            float randomX = UnityEngine.Random.Range(_mapBounds.min.x, _mapBounds.max.x);
            float randomY = UnityEngine.Random.Range(_mapBounds.min.y, _mapBounds.max.y);
            return new Vector3(randomX, randomY, 0f); // 2D 게임이므로 Z축을 0으로 고정합니다.
        }

        private void FlipTowards(Vector3 direction)
        {
            if (Mathf.Abs(direction.x) > 0.01f) // 아주 작은 움직임에는 반응하지 않도록
            {
                float yRotation = direction.x > 0 ? 180f : 0f;
                transform.rotation = Quaternion.Euler(0, yRotation, 0);
            }
        }
        
        #endregion

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (_isHitByShoot)
            {
                HandleCollision(other);
            }
        }

        private void OnCollisionStay2D(Collision2D other)
        {
            if (!_isHitByShoot)
            {
                HandleCollision(other);
            }
        }

        private void HandleCollision(Collision2D other)
        {
            if (!Mob_IsHit && other.gameObject.CompareTag("Player_Attack"))
            {
                HitCooltime(other).Forget();
                LogManager.Log("_isHitByShoot: "+_isHitByShoot,LogManager.LogCategory.NormalMob);
            }
        }

        private async UniTask HitCooltime(Collision2D other)
        {
            Mob_IsHit = true;

            // 피격 이펙트: 붉은색으로 점멸
            if (_spriteRenderer != null)
            {
                // 진행중인 컬러 트윈을 중지하고 즉시 흰색으로 리셋 후 새로운 시퀀스 시작
                _spriteRenderer.DOKill();
                _spriteRenderer.color = Color.white;
                DOTween.Sequence()
                    .Append(_spriteRenderer.DOColor(Color.red, 0.1f))
                    .Append(_spriteRenderer.DOColor(Color.white, 0.1f))
                    .SetTarget(transform); // 오브젝트가 파괴될 때 트윈도 함께 정리되도록 타겟 설정
            }

            float attackPower = _playerWeaphon.attackPower;
            float stunTime = _playerWeaphon.mobStunTime;

            await UniTask.Yield();
            Mob_Hp -= attackPower;

            if (Mob_Hp <= 0)
            {
                SetMobState(MobState.Die);
            }
            else
            {
                Mob_StunTime = stunTime;
                SetMobState(MobState.Stun);
            }

            Mob_IsHit = false;
        }

        protected override void Mob_Idle()
        {
            LogManager.Log("Idle", LogManager.LogCategory.NormalMob);
        }

        protected override void Mob_Move()
        {
            ismove = true;
        }

        protected override void Mob_Stun()
        {
            LogManager.Log("Stun", LogManager.LogCategory.NormalMob);
            ismove = false;
            _wanderTween?.Kill(); // 배회 중이었다면 중지
            _isWaitingToWander = false; // 스턴 시 대기 상태도 강제로 해제
            DOVirtual.DelayedCall(Mob_StunTime, () => SetMobState(MobState.Move));
        }

        protected override void Mob_hit()
        {
            base.Mob_hit();
        }

        protected override void Mob_Attack()
        {
            LogManager.Log("Attack", LogManager.LogCategory.NormalMob);
        }

        protected override void Mob_Die()
        {
            // 오브젝트 풀로 돌아가기 전, 모든 동작(Tween)을 확실히 정지시킵니다.
            transform.DOKill();
            base.Mob_Die();
            LogManager.Log("Die", LogManager.LogCategory.NormalMob);
        }

#if UNITY_EDITOR
        // 씬 뷰에서 선택했을 때 탐색 범위를 노란색 원으로 표시합니다.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, searchRange);
        }
#endif
    }
}