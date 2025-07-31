using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

namespace DogGuns_Games.vamsir
{
    public class VamPlayerControll : MonoBehaviour
    {
        
        //todo 아직 맵안에 들어오지 않은 적은 자동공격에 탐색되지않게 
        
        #region 필드 및 변수

        [Header("<color=green>Player Object")] [SerializeField]
        private GameObject player;

        [Header("<color=green>Player charactor Object")] [SerializeField]
        private PlayerBase playerCharactor;

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
        [Tooltip("자동 공격 시 플레이어가 멈추는 거리(공격 사거리)")]
        public float autoAttackStopDistance = 1.5f;
        [Tooltip("자동 공격 시 이동 속도 배율")]
        public float autoAttackMoveSpeedMultiplier = 1.0f;

        private bool _isJoystickActive = false;
        private bool _autoAttackEnabledByToggle = true;

        private float joystickInputThreshold = 0.1f;
        private float joystickIdleTime = 0.2f; // 입력이 없을 때 자동공격 재활성화까지 대기 시간
        private float joystickIdleTimer = 0f;
        private bool _autoAttackActive = false;

        private bool _isAutoMoveAttackEnabled = false;
        
        
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

        private void OnJoystickDown()
        {
            _isJoystickActive = true;
            if (_autoAttackEnabledByToggle)
                DisableAutoMoveAttack();
        }

        private void OnJoystickUp()
        {
            _isJoystickActive = false;
            if (_autoAttackEnabledByToggle)
                EnableAutoMoveAttack();
        }

        public void SetAutoAttackToggle(bool isOn)
        {
            _autoAttackEnabledByToggle = isOn;
            if (!isOn && _autoAttackActive)
            {
                DisableAutoMoveAttack();
                _autoAttackActive = false;
            }
            joystickIdleTimer = 0f;
        }

        private void FixedUpdate()
        {
            if (_isGameStart)
            {
                PlayerMovement();
                FallowCamera();
                UpdatePlayerHpSlider();

                // VariableJoystick 수정 없이 입력값 감지
                float inputMagnitude = Mathf.Abs(variableJoystick.Horizontal) + Mathf.Abs(variableJoystick.Vertical);
                if (_autoAttackEnabledByToggle)
                {
                    if (inputMagnitude > joystickInputThreshold)
                    {
                        // 조이스틱 조작 중: 자동공격 비활성화
                        joystickIdleTimer = 0f;
                        if (_autoAttackActive)
                        {
                            DisableAutoMoveAttack();
                            _autoAttackActive = false;
                        }
                    }
                    else
                    {
                        
                        // 조이스틱 입력 없음: 일정 시간 후 자동공격 재활성화
                        joystickIdleTimer += Time.fixedDeltaTime;
                        if (_autoAttackActive && joystickIdleTimer > joystickIdleTime)
                        {
                            EnableAutoMoveAttack();
                        }
                    }
                }
                else
                {
                    if (_autoAttackActive)
                    {
                        DisableAutoMoveAttack();
                        _autoAttackActive = false;
                    }
                }
            }
        }

        #endregion

        #region 게임 상태 관리

        private void PlayerInit()
        {
            // 캐릭터 스폰과의 레이스 컨디션을 피하기 위해 초기화 로직을 AssignCharacter로 이동했습니다.
            // OnGameStart 이벤트에서는 게임 시작 플래그만 설정합니다.
            _isGameStart = true;
        }

        /// <summary>
        /// VamserLikeGameManager가 플레이어 스폰 후 호출하여 캐릭터를 설정합니다.
        /// </summary>
        /// <param name="character">스폰된 플레이어 캐릭터</param>
        public void AssignCharacter(PlayerBase character)
        {
            if (playerCharactor == null && character != null)
            {
                playerCharactor = character;
                playerCharactor.transform.SetParent(player.gameObject.transform);
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
            if (playerCharactor == null)
            {
                PlayerInit();
                return;
            }

            // 현재 체력 값이 이전 값과 다를 때만 슬라이더 업데이트
            if (_playerHpSlider != null && Mathf.Abs(_playerHpSliderValue - playerCharactor.Health) > 0.001f)
            {
                _playerHpSliderValue = playerCharactor.Health;
                _playerHpSlider.value = _playerHpSliderValue;
                
                // 게임 오버 처리는 PlayerBase의 Player_Hit에서 담당하므로 여기서는 제거
                // PlayerBase에서 Health <= 0일 때 자동으로 게임 오버 처리됨
            }
        }

        #endregion

        #region 플레이어 제어

        /// <summary>
        /// 플레이어 이동을 처리하는 메서드
        /// </summary>
        private void PlayerMovement()
        {
            // 플레이어 객체 유효성 검사
            if (player == null || playerCharactor == null)
            {
                // PlayerInit(); // 재귀 호출 및 로직 문제로 제거
                return;
            }

            // 이동 입력 및 위치 계산
            Vector3 moveDirection = GetJoystickInputDirection();
            Vector3 targetPosition = CalculateTargetPosition(moveDirection);

            // 실제 이동 처리
            MovePlayer(targetPosition);

            // 애니메이션 및 회전 처리
            UpdateAnimationState(moveDirection.magnitude);

            // 공격 처리
            TryAttack(moveDirection);

            // 캐릭터 회전 처리
            UpdateCharacterRotation(moveDirection);
        }

        /// <summary>
        /// 조이스틱 입력을 방향 벡터로 변환
        /// </summary>
        private Vector3 GetJoystickInputDirection()
        {
            return (Vector3.right * variableJoystick.Horizontal + Vector3.up * variableJoystick.Vertical);
        }

        /// <summary>
        /// 맵 범위를 고려한 목표 위치 계산
        /// </summary>
        private Vector3 CalculateTargetPosition(Vector3 moveDirection)
        {
            float deltaSpeed = playerCharactor.MoveSpeed * Time.deltaTime;
            Vector3 rawTargetPosition = player.transform.position + moveDirection * deltaSpeed;

            // 맵 경계 확인
            Bounds mapBounds = mapRange.bounds;

            // 맵 범위 내로 제한
            Vector3 clampedPosition = new Vector3(
                Mathf.Clamp(rawTargetPosition.x, mapBounds.min.x, mapBounds.max.x),
                Mathf.Clamp(rawTargetPosition.y, mapBounds.min.y, mapBounds.max.y),
                rawTargetPosition.z
            );

            return clampedPosition;
        }

        /// <summary>
        /// 플레이어를 목표 위치로 이동
        /// </summary>
        private void MovePlayer(Vector3 targetPosition)
        {
            player.transform.DOMove(targetPosition, moveDuration);
        }

        /// <summary>
        /// 플레이어 애니메이션 상태 업데이트
        /// </summary>
        private void UpdateAnimationState(float moveMagnitude)
        {
            if (_playerAnimator != null)
            {
                _playerAnimator.SetFloat("Walk", moveMagnitude);
            }
        }

        /// <summary>
        /// 이동 중 공격 시도
        /// </summary>
        private void TryAttack(Vector3 moveDirection)
        {
            if (moveDirection.magnitude > 0.1f && !_isAttack)
            {
                PlayerAttack(moveDirection).Forget();
            }
        }

        /// <summary>
        /// 이동 방향에 따른 캐릭터 회전 처리
        /// </summary>
        private void UpdateCharacterRotation(Vector3 moveDirection)
        {
            if (moveDirection != Vector3.zero && playerCharactor != null)
            {
                float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
                float yRotation = (angle > 90 || angle < -90) ? 0f : 180f;
                playerCharactor.transform.rotation = Quaternion.Euler(0, yRotation, 0);
            }
        }

        /// <summary>
        /// 플레이어 공격 호출 
        /// </summary>
        /// <param name="attackAngle">공격 방향</param>
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

        /// <summary>
        /// 카메라 추적 
        /// </summary>
        private void FallowCamera()
        {
            // playerCharactor, cameraTransform, mapRange가 모두 할당되었는지 확인
            if (playerCharactor == null || cameraTransform == null || mapRange == null)
            {
                // 필수 컴포넌트가 없으면 카메라 추적을 시도하지 않음
                return;
            }
            
            // 카메라가 맵 경계를 벗어나지 않도록 설정
            Vector3 cameraPosition = new Vector3(playerCharactor.transform.position.x,
                playerCharactor.transform.position.y, cameraTransform.transform.position.z);

            // 맵 범위의 경계를 가져옴
            Bounds mapBounds = mapRange.bounds;

            // 카메라의 절반 크기를 계산
            float cameraHalfWidth = cameraTransform.orthographicSize * cameraTransform.aspect;
            float cameraHalfHeight = cameraTransform.orthographicSize;

            // 맵 범위 내에서 카메라 위치를 클램프
            cameraPosition.x = Mathf.Clamp(cameraPosition.x, mapBounds.min.x + cameraHalfWidth,
                mapBounds.max.x - cameraHalfWidth);
            cameraPosition.y = Mathf.Clamp(cameraPosition.y, mapBounds.min.y + cameraHalfHeight,
                mapBounds.max.y - cameraHalfHeight);

            cameraTransform.transform.DOMove(cameraPosition, moveDuration);
        }

        #endregion

        #region 자동 이동 및 공격 설정

        
        
        public bool AutoAttackEnabledByToggle
        {
            get => _autoAttackEnabledByToggle;
            set
            {
                _autoAttackEnabledByToggle = value;
                if (value)
                {
                    EnableAutoMoveAttack();
                }
                else
                {
                    DisableAutoMoveAttack();
                }
            }
        }
        
        // 플레이어 자동 이동 및 공격 루프
        private CancellationTokenSource _autoMoveAttackCTS;
        public void EnableAutoMoveAttack()
        {
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
            if (_autoMoveAttackCTS != null)
            {
                _autoMoveAttackCTS.Cancel();
                _autoMoveAttackCTS.Dispose();
                _autoMoveAttackCTS = null;
            }
        }

        private async UniTaskVoid AutoMoveAttackLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!_isGameStart || playerCharactor == null)
                {
                    await UniTask.Yield();
                    continue;
                }

                // 가장 가까운 적 찾기
                GameObject closestEnemy = FindClosestEnemy();
                if (closestEnemy != null)
                {
                    Vector3 enemyPos = closestEnemy.transform.position;
                    Vector3 playerPos = player.transform.position;
                    Vector3 dir = (enemyPos - playerPos).normalized;
                    float distance = Vector3.Distance(playerPos, enemyPos);
                    float stopDistance = autoAttackStopDistance; // 인스펙터에서 설정

                    // 사거리 밖이면 적 방향으로 이동
                    if (distance > stopDistance)
                    {
                        Vector3 targetPosition = player.transform.position + dir * (playerCharactor.MoveSpeed * autoAttackMoveSpeedMultiplier * Time.deltaTime);
                        // 맵 경계 확인 및 클램프
                        Bounds mapBounds = mapRange.bounds;
                        targetPosition = new Vector3(
                            Mathf.Clamp(targetPosition.x, mapBounds.min.x, mapBounds.max.x),
                            Mathf.Clamp(targetPosition.y, mapBounds.min.y, mapBounds.max.y),
                            targetPosition.z
                        );
                        MovePlayer(targetPosition);
                        UpdateAnimationState(dir.magnitude);
                        UpdateCharacterRotation(dir);
                    }
                    else
                    {
                        // 사거리 안이면 공격
                        if (!_isAttack)
                        {
                            await PlayerAttack(dir);
                        }
                        UpdateAnimationState(0f);
                    }
                }
                else
                {
                    // 적이 없으면 Idle
                    UpdateAnimationState(0f);
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        private GameObject FindClosestEnemy()
        {
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Mob");
            GameObject closest = null;
            float minDist = float.MaxValue;
            Vector3 playerPos = player.transform.position;
            Bounds mapBounds = mapRange != null ? mapRange.bounds : new Bounds(Vector3.zero, Vector3.one * 9999f);
            foreach (var enemy in enemies)
            {
                Vector3 enemyPos = enemy.transform.position;
                // 맵 안에 있는 적만 탐색
                if (enemyPos.x < mapBounds.min.x || enemyPos.x > mapBounds.max.x ||
                    enemyPos.y < mapBounds.min.y || enemyPos.y > mapBounds.max.y)
                {
                    continue;
                }
                float dist = Vector3.Distance(playerPos, enemyPos);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = enemy;
                }
            }
            return closest;
        }
        
        #endregion
    }
}
