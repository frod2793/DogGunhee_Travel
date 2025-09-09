using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

namespace DogGuns_Games.vamsir
{
    public class VamPlayerControll : MonoBehaviour
    {
        
        #region 필드 및 변수 
        
        [Header("<color=green>Player Object")] [SerializeField]
        private GameObject player; // 플레이어 오브젝트 (움직일 대상 )
        
        [Header("<color=green>Player charactor Object")] [SerializeField]
        private PlayerBase playerCharactor; // 플레이어 캐릭터

        [Header("<color=green>Player HP_IU")] [SerializeField]
        private Slider playerHpSliderPrefab;

        private float _playerHpSliderValue = 0;

        private Animator _playerAnimator;

        [Header("<color=green>Joystick")] [SerializeField]
        private VariableJoystick variableJoystick;

        [Header("<color=green>camera")] [SerializeField]
        private Camera cameraTransform;

        [Header("<color=green>Move Duration")] [SerializeField]
        private float moveDuration = 0.1f;


        private Slider _playerHpSlider; // 인스턴스화된 슬라이더 참조 추가

        [Header("<color=green>Map Range")] [SerializeField]
        private SpriteRenderer mapRange;


        bool _isGameStart = false;

        bool _isAttack = false;

        [Header("<color=green>자동 공격 설정")] 
        [Tooltip("자동 공격 시 탐지할 적의 레이어")]
        [SerializeField] private LayerMask enemyLayer;
        [Tooltip("자동 공격 시 적을 탐지하는 최대 반경")]
        public float detectionRadius = 30f; 
        [Tooltip("자동 공격 시 플레이어가 멈추는 거리(공격 사거리)")]
        public float attackRadius = 1.5f;
        [Tooltip("자동 공격 시 이동 속도 배율")]
        public float autoAttackMoveSpeedMultiplier = 1.0f;

        private float joystickInputThreshold = 0.1f;
        private bool _autoAttackActive = false;
        private GameObject _currentTarget;
        
        public Vector3 MoveDirection { get; private set; } // 현재 조이스틱 입력에 따른 이동 방향
        
        #endregion

        #region Unity 라이프사이클

        private void Start()
        {
            PlayStateManager.OnGameStart += PlayerInit;
            PlayStateManager.OnGamePause += Pause;
            PlayStateManager.OnGameResume += Resume;
            PlayStateManager.OnGameOver += OnGameOver;
        }

        private void OnDestroy()
        {
            PlayStateManager.OnGameStart -= PlayerInit;
            PlayStateManager.OnGamePause -= Pause;
            PlayStateManager.OnGameResume -= Resume;
            PlayStateManager.OnGameOver -= OnGameOver;
        }

        private void OnGameOver()
        {
            DisableAutoMoveAttack();
            _autoAttackActive = false;
        }

        private void FixedUpdate()
        {
            if (_isGameStart)
            {
                HandleMovement();
                FallowCamera();
                UpdatePlayerHpSlider();
            }
        }

        #endregion
        
        #region 게임 상태 관리

        private void PlayerInit()
        {
            _isGameStart = true;
        }

        public void AssignCharacter(PlayerBase character)
        {
            if (playerCharactor == null && character != null)
            {
                playerCharactor = character;
                playerCharactor.transform.SetParent(player.transform, false);
                _playerAnimator = playerCharactor.GetComponent<Animator>();
                cameraTransform = Camera.main;
                Set_playerHpSlider();
            }
        }

        private void Pause()
        {
            _isGameStart = false;
        }

        private void Resume()
        {
            _isGameStart = true;
        }

        #endregion

        #region UI 설정

        private void Set_playerHpSlider()
        {
            _playerHpSlider = Instantiate(playerHpSliderPrefab, player.transform);
            _playerHpSlider.transform.localPosition = new Vector3(0, -0.4f, 0);
            _playerHpSlider.maxValue = playerCharactor.Health;
            _playerHpSlider.value = playerCharactor.Health;
            _playerHpSliderValue = playerCharactor.Health;
        }

        private void UpdatePlayerHpSlider()
        {
            if (player == null || playerCharactor == null) return;

            if (_playerHpSlider != null && Mathf.Abs(_playerHpSliderValue - playerCharactor.Health) > 0.001f)
            {
                _playerHpSliderValue = playerCharactor.Health;
                _playerHpSlider.value = _playerHpSliderValue;
            }
        }

        #endregion

        #region 플레이어 제어
        
        private void HandleMovement()
        {
            MoveDirection = GetJoystickInputDirection(); // 현재 이동 방향 업데이트
            bool isJoystickActive = MoveDirection.magnitude > joystickInputThreshold;

            if (isJoystickActive)
            {
                if (_autoAttackActive)
                {
                    DisableAutoMoveAttack();
                }
                ManualPlayerMovement(MoveDirection);
                TryAttack(MoveDirection);
            }
            else
            {
                if (AutoAttackEnabledByToggle && !_autoAttackActive)
                {
                    EnableAutoMoveAttack();
                }
                
                if (!isJoystickActive && !_autoAttackActive)
                {
                    UpdateAnimationState(0f);
                }
            }
        }

        private void ManualPlayerMovement(Vector3 moveDirection)
        {
            if (player == null || playerCharactor == null) return;

            float deltaSpeed = playerCharactor.MoveSpeed * Time.deltaTime;
            Vector3 targetPosition = player.transform.position + moveDirection * deltaSpeed;

            player.transform.position = ClampPositionToMap(targetPosition);
            UpdateAnimationState(moveDirection.magnitude);
            UpdateCharacterRotation(moveDirection);
        }

        private Vector3 GetJoystickInputDirection()
        {
            return (Vector3.right * variableJoystick.Horizontal + Vector3.up * variableJoystick.Vertical);
        }

        private Vector3 ClampPositionToMap(Vector3 position)
        {
            if (mapRange == null) return position;

            Bounds mapBounds = mapRange.bounds;
            return new Vector3(
                Mathf.Clamp(position.x, mapBounds.min.x, mapBounds.max.x),
                Mathf.Clamp(position.y, mapBounds.min.y, mapBounds.max.y),
                0f // 플레이어의 z 위치를 항상 0으로 고정합니다.
            );
        }

        private void UpdateAnimationState(float moveMagnitude)
        {
            if (_playerAnimator != null)
            {
                _playerAnimator.SetFloat("Walk", moveMagnitude);
            }
        }

        private void TryAttack(Vector3 moveDirection)
        {
            if (moveDirection.magnitude > 0.1f && !_isAttack)
            {
                PlayerAttack(moveDirection).Forget();
            }
        }

        private void UpdateCharacterRotation(Vector3 moveDirection)
        {
            if (moveDirection != Vector3.zero && playerCharactor != null)
            {
                float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
                float yRotation = (angle > 90 || angle < -90) ? 0f : 180f;
                playerCharactor.transform.rotation = Quaternion.Euler(0, yRotation, 0);
            }
        }

        private async UniTask PlayerAttack(Vector3 attackAngle)
        {
            _isAttack = true;
            playerCharactor.AttackAngle = attackAngle;
            playerCharactor.PlayState = PlayerBase.PlayerState.Attack;
            await UniTask.Delay(100);
            _isAttack = false;
        }

        #endregion

        #region 카메라 제어

        private void FallowCamera()
        {
            if (player == null || cameraTransform == null || mapRange == null) return;
            
            Vector3 cameraPosition = new Vector3(player.transform.position.x, player.transform.position.y, cameraTransform.transform.position.z);
            Bounds mapBounds = mapRange.bounds;
            float cameraHalfWidth = cameraTransform.orthographicSize * cameraTransform.aspect;
            float cameraHalfHeight = cameraTransform.orthographicSize;

            cameraPosition.x = Mathf.Clamp(cameraPosition.x, mapBounds.min.x + cameraHalfWidth, mapBounds.max.x - cameraHalfWidth);
            cameraPosition.y = Mathf.Clamp(cameraPosition.y, mapBounds.min.y + cameraHalfHeight, mapBounds.max.y - cameraHalfHeight);

            float smoothSpeed = 1f - Mathf.Exp(-Time.fixedDeltaTime / Mathf.Max(0.001f, moveDuration));
            cameraTransform.transform.position = Vector3.Lerp(cameraTransform.transform.position, cameraPosition, smoothSpeed);
        }

        #endregion

        #region 자동 이동 및 공격 설정

        private bool _autoAttackEnabledByToggle = false;
        public bool AutoAttackEnabledByToggle
        {
            get => _autoAttackEnabledByToggle;
            set
            {
                if (_autoAttackEnabledByToggle == value) return;
                _autoAttackEnabledByToggle = value;

                if (!_autoAttackEnabledByToggle && _autoAttackActive)
                {
                    DisableAutoMoveAttack();
                }
            }
        }
        
        private CancellationTokenSource _autoMoveAttackCTS;
        public void EnableAutoMoveAttack()
        {
            if (_autoAttackActive) return;
            _autoAttackActive = true;
            
            if (_autoMoveAttackCTS != null)
            {
                _autoMoveAttackCTS.Cancel();
                _autoMoveAttackCTS.Dispose();
            }
            _autoMoveAttackCTS = new CancellationTokenSource();
            AutoMoveAttackLoop(_autoMoveAttackCTS.Token).Forget();
        }

        public void DisableAutoMoveAttack()
        {
            if (!_autoAttackActive) return;
            _autoAttackActive = false;
            
            if (_autoMoveAttackCTS != null)
            {
                _autoMoveAttackCTS.Cancel();
                _autoMoveAttackCTS.Dispose();
                _autoMoveAttackCTS = null;
            }
            _currentTarget = null;
            LogManager.Log("자동 공격 비활성화됨.", LogManager.LogCategory.PlayerBase);
        }

        private async UniTaskVoid AutoMoveAttackLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!_isGameStart || playerCharactor == null || !_autoAttackActive)
                {
                    await UniTask.Yield();
                    continue;
                }

                GameObject closestEnemy = FindClosestEnemy();
                if (closestEnemy != null)
                {
                    if (closestEnemy != _currentTarget)
                    {
                        _currentTarget = closestEnemy;
                        LogManager.Log($"자동 공격, 새 타겟 지정: {closestEnemy.name}", LogManager.LogCategory.PlayerBase);
                    }

                    Vector3 enemyPos = closestEnemy.transform.position;
                    Vector3 playerPos = player.transform.position;
                    Vector3 dir = (enemyPos - playerPos).normalized;

                    // 1. 이동: 항상 이상적인 공격 위치로 부드럽게 이동합니다.
                    Vector3 destination = enemyPos - dir * attackRadius;
                    float step = playerCharactor.MoveSpeed * autoAttackMoveSpeedMultiplier * Time.deltaTime;
                    Vector3 newPosition = Vector3.MoveTowards(playerPos, destination, step);
                    player.transform.position = ClampPositionToMap(newPosition);

                    // 2. 애니메이션 및 회전
                    Vector3 movedVector = newPosition - playerPos;
                    UpdateAnimationState(movedVector.magnitude > 0.001f ? 1f : 0f);
                    UpdateCharacterRotation(dir);

                    // 3. 공격: 사거리 내에 들어오면 공격을 시작합니다.
                    float distance = Vector3.Distance(player.transform.position, enemyPos);
                    if (distance <= attackRadius * 1.1f) // 약간의 버퍼를 주어 안정적으로 공격하게 함
                    {
                        if (!_isAttack)
                        {
                            await PlayerAttack(dir);
                        }
                    }
                }
                else
                {
                    if (_currentTarget != null) _currentTarget = null;
                    UpdateAnimationState(0f);
                }

                await UniTask.Yield(PlayerLoopTiming.FixedUpdate);
            }
        }

        private GameObject FindClosestEnemy()
        {
            Vector2 searchPosition = player.transform.position;
            Collider2D[] enemiesInRange = Physics2D.OverlapCircleAll(searchPosition, detectionRadius, enemyLayer);
            
            GameObject closest = null;
            float minDist = float.MaxValue;
            
            foreach (var enemyCollider in enemiesInRange)
            {
                Vector3 enemyPos = enemyCollider.transform.position;
                float dist = Vector2.Distance(searchPosition, enemyPos);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = enemyCollider.gameObject;
                }
            }
            return closest;
        }
        
        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (player == null) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(player.transform.position, detectionRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(player.transform.position, attackRadius);
        }
        #endif
        
        #endregion
    }
}
