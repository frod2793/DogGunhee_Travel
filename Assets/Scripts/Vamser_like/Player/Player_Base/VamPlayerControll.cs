using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DG.Tweening;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DogGuns_Games.vamsir
{
    public class VamPlayerControll : MonoBehaviour
    {
        
        #region 필드 및 변수 

        [Header("오브젝트 참조")]
        [Tooltip("실제 움직임을 담당하는 플레이어의 부모 오브젝트입니다.")]
        [FormerlySerializedAs("player")]
        [SerializeField] private GameObject m_playerObject;
        [Tooltip("애니메이션과 캐릭터 로직을 담당하는 자식 오브젝트입니다.")]
        [FormerlySerializedAs("playerCharactor")]
        [SerializeField] private PlayerBase m_playerCharacter;
        [Tooltip("플레이어 머리 위에 표시될 HP 슬라이더 프리팹입니다.")]
        [FormerlySerializedAs("playerHpSliderPrefab")] [SerializeField]
        private Slider m_playerHpSliderPrefab;
        [Tooltip("플레이어의 이동 범위를 제한하는 맵의 SpriteRenderer입니다.")]
        [FormerlySerializedAs("mapRange")]
        [SerializeField] private SpriteRenderer m_mapRange;

        [Header("입력 및 UI")]
        [Tooltip("플레이어 이동에 사용될 조이스틱입니다.")]
        private const float k_joystickInputThreshold = 0.1f;

        [Header("카메라 이동")]
        [Tooltip("카메라가 플레이어를 따라가는 부드러운 정도입니다. 값이 작을수록 빠르게 따라갑니다.")]
        [FormerlySerializedAs("moveDuration")]
        [SerializeField] private float m_cameraSmoothTime = 0.1f;

        [Header("<color=green>자동 공격 설정")] 
        [Tooltip("자동 공격 시 탐지할 적의 레이어")]
        [FormerlySerializedAs("enemyLayer")]
        [SerializeField] private LayerMask m_enemyLayer;
        [Tooltip("자동 공격 시 적을 탐지하는 최대 반경")]
        [FormerlySerializedAs("detectionRadius")]
        [SerializeField] private float m_detectionRadius = 30f;
        [Tooltip("자동 공격 시 플레이어가 멈추는 거리(공격 사거리)")]
        [FormerlySerializedAs("attackRadius")]
        [SerializeField] private float m_attackRadius = 1.5f;
        [Tooltip("자동 공격 시 이동 속도 배율")]
        [FormerlySerializedAs("autoAttackMoveSpeedMultiplier")]
        [SerializeField] private float m_autoAttackMoveSpeedMultiplier = 1.0f;

        // Private 상태 변수
        private VamserLikeGameManager m_gameManager;
        private Animator m_playerAnimator;
        private Camera m_camera;
        private Slider m_playerHpSlider;
        private bool m_isGameStarted = false;
        private bool m_isAttacking = false;
        private bool m_isAutoAttackActive = false;
        private GameObject m_currentTarget;
        private CancellationTokenSource m_autoMoveAttackCTS;
        private Vector3 m_autoMoveDirection; // 자동 공격 시 이동 방향
        private Vector3 m_cameraVelocity = Vector3.zero; // SmoothDamp를 위한 속도 변수
        private ContactFilter2D m_contactFilter;

        public Vector3 MoveDirection { get; private set; } // 현재 조이스틱 입력에 따른 이동 방향
        
        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_contactFilter.useTriggers = true; // 몹의 isTrigger 콜라이더를 감지
            m_contactFilter.SetLayerMask(m_enemyLayer);
            m_contactFilter.useLayerMask = true;
        }

        private void Start()
        {
            m_gameManager = VamserLikeGameManager.Instance;
            PlayStateManager.OnGameStart += PlayerInit;
            PlayStateManager.OnGamePause += Pause;
            PlayStateManager.OnGameResume += Resume;
            PlayStateManager.OnGameOver += OnGameOver;
        }

        private void OnDisable()
        {
            PlayStateManager.OnGameStart -= PlayerInit;
            PlayStateManager.OnGamePause -= Pause;
            PlayStateManager.OnGameResume -= Resume;
            PlayStateManager.OnGameOver -= OnGameOver;

            if (m_playerCharacter != null)
            {
                m_playerCharacter.OnHealthChanged -= UpdatePlayerHpSlider;
            }
        }

        private void OnGameOver()
        {
            DisableAutoMoveAttack();
            m_isAutoAttackActive = false;
        }

        private void FixedUpdate()
        {
            if (m_isGameStarted)
            {
                HandleMovementInput();
                ProcessMovement();
                
                FollowCamera();
            }
        }

        #endregion
        
        #region 게임 상태 관리

        private void PlayerInit()
        {
            m_isGameStarted = true;
        }

        public void AssignCharacter(PlayerBase character)
        {
            if (m_playerCharacter == null && character != null)
            {
                m_playerCharacter = character;
                m_playerCharacter.transform.SetParent(m_playerObject.transform, false);
                m_playerAnimator = m_playerCharacter.GetComponent<Animator>();
                m_camera = VamserLikeGameManager.Instance.MainCamera;
                LogManager.Log(m_camera != null ? $"캐릭터 할당 성공: {character.name}, 카메라 할당 성공" : $"캐릭터 할당 성공: {character.name}, 그러나 카메라 할당 실패", 
                    LogManager.LogCategory.PlayerBase);
                
                SetPlayerHpSlider();
                
                // 체력 변경 이벤트 구독
                m_playerCharacter.OnHealthChanged += UpdatePlayerHpSlider;
            }
        }

        private void Pause()
        {
            m_isGameStarted = false;
        }

        private void Resume()
        {
            m_isGameStarted = true;
        }

        #endregion

        #region UI 설정

        private void SetPlayerHpSlider()
        {
            m_playerHpSlider = Instantiate(m_playerHpSliderPrefab, m_playerObject.transform);
            m_playerHpSlider.transform.localPosition = new Vector3(0, -0.4f, 0);
            m_playerHpSlider.maxValue = m_playerCharacter.MaxHealth;
            m_playerHpSlider.value = m_playerCharacter.CurrentHealth;
        }

        /// <summary>
        /// PlayerBase의 OnHealthChanged 이벤트에 의해 호출됩니다.
        /// </summary>
        private void UpdatePlayerHpSlider(float currentHealth, float maxHealth)
        {
            if (m_playerHpSlider != null)
            {
                m_playerHpSlider.value = currentHealth;
                m_playerHpSlider.maxValue = maxHealth;
            }
        }

        #endregion

        #region 플레이어 제어
        
        /// <summary>
        /// FixedUpdate에서 입력을 처리하고 이동 방향을 결정합니다.
        /// </summary>
        private void HandleMovementInput()
        {
            MoveDirection = GetJoystickInputDirection(); // 현재 이동 방향 업데이트
            bool isJoystickActive = MoveDirection.magnitude > k_joystickInputThreshold;

            if (isJoystickActive)
            {
                // 수동 조작 시 자동 공격 비활성화
                if (m_isAutoAttackActive)
                {
                    DisableAutoMoveAttack();
                }
                TryAttack(MoveDirection);
                // 조이스틱 입력이 있을 때는 수동 이동이므로, 여기서 return하지 않고 ProcessMovement로 넘어갑니다.
            }
            else
            {
                // 조이스틱 입력이 없을 때 자동 공격 활성화 조건 체크
                if (AutoAttackEnabledByToggle && !m_isAutoAttackActive)
                {
                    EnableAutoMoveAttack();
                }
                
                // 조이스틱 입력이 없고, 자동 공격도 비활성 상태일 때 애니메이션 정지
                if (!m_isAutoAttackActive)
                {
                    MoveDirection = Vector3.zero;
                }
                else
                {
                    // 자동 공격이 활성화 상태일 때는 AutoMoveAttackLoop에서 계산된 방향을 사용
                    MoveDirection = m_autoMoveDirection;
                }
            }
            
            LogManager.Log($"[Movement] Final MoveDirection: {MoveDirection}", LogManager.LogCategory.PlayerBase);
            
            UpdateAnimationState(MoveDirection.magnitude);
        }

        /// <summary>
        /// FixedUpdate에서 최종 이동 방향에 따라 실제 위치를 변경합니다.
        /// </summary>
        private void ProcessMovement()
        {
            if (m_playerObject == null || m_playerCharacter == null || MoveDirection == Vector3.zero)
            {
                return;
            }

            float deltaSpeed = m_playerCharacter.MoveSpeed * Time.fixedDeltaTime;
            Vector3 currentPos = m_playerObject.transform.position; // GameManager 대신 직접 참조
            Vector3 targetPosition = currentPos + MoveDirection * deltaSpeed;

            // Rigidbody가 있다면 MovePosition을 사용하는 것이 더 좋습니다.
            // 여기서는 기존 구조를 유지하여 transform.position을 직접 변경합니다.
            m_playerObject.transform.position = ClampPositionToMap(targetPosition);

            UpdateCharacterRotation(MoveDirection);
        }

        private Vector3 GetJoystickInputDirection()
        {
            // GameManager를 통해 중앙에서 관리되는 조이스틱 참조를 사용합니다.
            var joystick = VamserLikeGameManager.Instance?.Joystick;
            if (joystick != null)
            {
                Vector3 direction = new Vector3(joystick.Horizontal, joystick.Vertical, 0);
                if (direction.magnitude > k_joystickInputThreshold)
                {
                    LogManager.Log($"[Input] Joystick Input: ({joystick.Horizontal:F2}, {joystick.Vertical:F2})", LogManager.LogCategory.PlayerBase);
                }
                return direction.normalized;
            }
            else
            {
                LogManager.LogWarning("[Input] Joystick is not available.", LogManager.LogCategory.PlayerBase);
                return Vector3.zero;
            }
        }

        private Vector3 ClampPositionToMap(Vector3 position)
        {
            if (m_mapRange == null) return position;

            Bounds mapBounds = m_mapRange.bounds;
            return new Vector3(
                Mathf.Clamp(position.x, mapBounds.min.x, mapBounds.max.x),
                Mathf.Clamp(position.y, mapBounds.min.y, mapBounds.max.y),
                0f // 플레이어의 z 위치를 항상 0으로 고정합니다.
            );
        }

        private void UpdateAnimationState(float moveMagnitude)
        {
            if (m_playerAnimator != null)
            {
                m_playerAnimator.SetFloat("Walk", moveMagnitude);
            }
        }

        private void TryAttack(Vector3 moveDirection)
        {
            if (moveDirection.magnitude > k_joystickInputThreshold && !m_isAttacking)
            {
                PlayerAttack(moveDirection).Forget();
            }
        }

        private void UpdateCharacterRotation(Vector3 moveDirection)
        {
            if (moveDirection != Vector3.zero && m_playerCharacter != null)
            {
                float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
                float yRotation = (angle > 90 || angle < -90) ? 0f : 180f;
                m_playerCharacter.transform.rotation = Quaternion.Euler(0, yRotation, 0);
            }
        }

        private async UniTask PlayerAttack(Vector3 attackAngle)
        {
            if (m_playerCharacter == null || m_playerCharacter.WeaphonBase == null) return;

            m_isAttacking = true;
            
            // Weaphon_base의 AttackAngle에 의존하지 않고, 컨트롤러에서 직접 공격 방향을 전달합니다.
            // 이를 통해 수동 조작과 자동 공격의 방향 결정 로직이 명확하게 분리됩니다.
            m_playerCharacter.WeaphonBase.Weaphon_Attack(attackAngle);

            // 공격 쿨타임 또는 애니메이션 시간에 따른 딜레이
            // Weaphon_base의 coolTime을 사용하는 것이 더 정확할 수 있습니다.
            await UniTask.Delay(TimeSpan.FromSeconds(m_playerCharacter.WeaphonBase.coolTime), cancellationToken: this.GetCancellationTokenOnDestroy());
            
            m_isAttacking = false;
        }

        #endregion

        #region 카메라 제어

        private void FollowCamera()
        {
            if (m_playerObject == null || m_camera == null || m_mapRange == null)
            {
                if (m_camera == null)
                {
                    LogManager.LogWarning("[Camera] Main Camera is not assigned. Camera cannot follow.", LogManager.LogCategory.PlayerBase);
                }
                return;
            }
            
            Vector3 targetPosition = new Vector3(m_playerObject.transform.position.x, m_playerObject.transform.position.y, m_camera.transform.position.z);
            Bounds mapBounds = m_mapRange.bounds;
            float cameraHalfWidth = m_camera.orthographicSize * m_camera.aspect;
            float cameraHalfHeight = m_camera.orthographicSize;

            targetPosition.x = Mathf.Clamp(targetPosition.x, mapBounds.min.x + cameraHalfWidth, mapBounds.max.x - cameraHalfWidth);
            targetPosition.y = Mathf.Clamp(targetPosition.y, mapBounds.min.y + cameraHalfHeight, mapBounds.max.y - cameraHalfHeight);
            
            LogManager.Log($"[Camera] Following player to Target Position: {targetPosition}", LogManager.LogCategory.PlayerBase);

            m_camera.transform.position = Vector3.SmoothDamp(m_camera.transform.position, targetPosition, ref m_cameraVelocity, m_cameraSmoothTime);
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

                if (!_autoAttackEnabledByToggle && m_isAutoAttackActive)
                {
                    DisableAutoMoveAttack();
                }
            }
        }
        
        public void EnableAutoMoveAttack()
        {
            if (m_isAutoAttackActive) return;
            m_isAutoAttackActive = true;
            
            m_autoMoveAttackCTS?.Cancel();
            m_autoMoveAttackCTS?.Dispose();
            
            m_autoMoveAttackCTS = new CancellationTokenSource();
            AutoMoveAttackLoop(m_autoMoveAttackCTS.Token).Forget();
        }

        public void DisableAutoMoveAttack()
        {
            if (!m_isAutoAttackActive) return;
            m_isAutoAttackActive = false;
            
            if (m_autoMoveAttackCTS != null)
            {
                m_autoMoveAttackCTS.Cancel();
                m_autoMoveAttackCTS.Dispose();
                m_autoMoveAttackCTS = null;
            }
            m_currentTarget = null;
            LogManager.Log("자동 공격 비활성화됨.", LogManager.LogCategory.PlayerBase);
        }

        private async UniTaskVoid AutoMoveAttackLoop(CancellationToken token)
        {
            LogManager.Log("자동 공격 활성화됨.", LogManager.LogCategory.PlayerBase);
            while (!token.IsCancellationRequested)
            {
                if (!m_isGameStarted || m_playerCharacter == null || !m_isAutoAttackActive)
                {
                    await UniTask.Yield();
                    continue;
                }

                VamserMobBase closestEnemy = FindClosestEnemy();
                if (closestEnemy != null)
                {
                    if (closestEnemy.gameObject != m_currentTarget)
                    {
                        m_currentTarget = closestEnemy.gameObject;
                        LogManager.Log($"자동 공격, 새 타겟 지정: {closestEnemy.name}", LogManager.LogCategory.PlayerBase);
                    }

                    Vector3 enemyPos = closestEnemy.transform.position;
                    // 이동 주체인 GameManager로부터 현재 위치를 가져옵니다.
                    Vector3 playerPos = m_gameManager.PlayerPos();
                    Vector3 dir = (enemyPos - playerPos).normalized;

                    // 1. 이동: 항상 이상적인 공격 위치로 부드럽게 이동합니다.
                    float distanceToEnemy = Vector3.Distance(playerPos, enemyPos);
                    
                    // 공격 사거리보다 멀리 있을 때만 적으로 이동
                    m_autoMoveDirection = distanceToEnemy > m_attackRadius ? dir : Vector3.zero;
                    
                    // 3. 공격: 사거리 내에 들어오면 공격을 시작합니다.
                    float distance = Vector3.Distance(m_gameManager.PlayerPos(), enemyPos);
                    if (distance <= m_attackRadius * 1.1f) // 약간의 버퍼를 주어 안정적으로 공격하게 함
                    {
                        if (!m_isAttacking)
                        {
                            await PlayerAttack(dir);
                        }
                    }
                }
                else
                {
                    if (m_currentTarget != null) m_currentTarget = null;
                    m_autoMoveDirection = Vector3.zero;
                }

                await UniTask.Yield();
            }
        }

        private VamserMobBase FindClosestEnemy()
        {
            Vector2 searchPosition = m_playerObject.transform.position;
            int count = Physics2D.OverlapCircle(searchPosition, m_detectionRadius, m_contactFilter, _enemyColliders);
            
            VamserMobBase closest = null;
            float minDist = float.MaxValue;
            
            for (int i = 0; i < count; i++)
            {
                var enemyCollider = _enemyColliders[i];
                if (enemyCollider.TryGetComponent(out VamserMobBase mob) && !mob.IsDead)
                {
                    float dist = Vector2.Distance(searchPosition, mob.transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closest = mob;
                    }
                }
            }
            
            System.Array.Clear(_enemyColliders, 0, count);
            return closest;
        }
        
        // GC Alloc을 피하기 위해 배열을 캐싱합니다.
        private readonly Collider2D[] _enemyColliders = new Collider2D[50];

        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (m_playerObject == null) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(m_playerObject.transform.position, m_detectionRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(m_playerObject.transform.position, m_attackRadius);
        }
        #endif
        
        #endregion
    }
}
