using InGame.Managers;
using InGame.Mob.MobBase;
using InGame.Mob.Systems;
using UnityEngine;
using InGame.Core.Interfaces;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// [설명]: 플레이어의 전체적인 제어를 관리하는 최상위 컨트롤러(Facade) 클래스입니다.
    /// 입력 처리, 이동, 애니메이션 제어 및 하위 시스템(자동 공격 등)의 생명주기를 주도합니다.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        #region 에디터 설정

        [Header("오브젝트 참조")]
        [SerializeField, Tooltip("플레이어의 최상위 루트 오브젝트")]
        private GameObject m_playerObject;

        [SerializeField, Tooltip("플레이어 데이터 및 기본 기능을 담당하는 인스턴스")]
        private PlayerBase m_playerCharacter;

        [SerializeField, Tooltip("맵의 이동 제한 범위를 정의하는 SpriteRenderer")]
        private SpriteRenderer m_mapRange;

        [Header("자동 공격 설정")]
        [SerializeField, Tooltip("적으로 간주할 레이어")]
        private LayerMask m_enemyLayer;

        [SerializeField, Tooltip("자동 공격 시 적을 탐지하는 반경")]
        private float m_detectionRadius = 10f;

        [SerializeField, Tooltip("자동 공격 및 근접 판단 사거리")]
        private float m_attackRadius = 1.5f;

        #endregion

        #region 하위 시스템 및 컴포넌트

        /// <summary> 조이스틱 및 키보드 입력을 해석하는 시스템 </summary>
        private PlayerInputHandler m_inputHandler;

        /// <summary> 물리 기반 또는 트랜스폼 기반 이동 로직 </summary>
        private PlayerMovement m_movement;

        /// <summary> 주변 적 탐지 및 추적/공격 제어 시스템 </summary>
        private PlayerAutoAttackSystem m_autoAttack;

        /// <summary> 캐릭터 애니메이션 재생을 위한 애니메이터 캐시 </summary>
        private Animator m_playerAnimator;

        // [수정]: 인터페이스 기반 의존성
        private IGameStateService m_gameState;
        private IPlayerContext m_playerCtx;

        #endregion

        #region 내부 상태 필드

        /// <summary> 게임이 공식적으로 시작되었는지 여부 </summary>
        private bool m_isGameStarted;

        /// <summary> 이벤트 구독 여부 확인 플래그 </summary>
        private bool m_isSubscribed;

        /// <summary> 피격 판정을 위한 이전 프레임 체력 값 </summary>
        private float m_previousHealth;

        // 애니메이터 파라미터 해시값 캐싱
        private static readonly int k_AnimWalk = Animator.StringToHash("Walk");
        private static readonly int k_AnimHit = Animator.StringToHash("Hit");
        private static readonly int k_AnimDie = Animator.StringToHash("Die");

        #endregion

        #region 공개 프로퍼티

        /// <summary>
        /// [설명]: 현재 플레이어의 실시간 이동 입력 방향 벡터를 반환합니다.
        /// </summary>
        public Vector3 MoveDirection => m_inputHandler != null ? (Vector3)m_inputHandler.MoveDirection : Vector3.zero;

        /// <summary>
        /// [설명]: 시스템 토글 설정을 통해 활성화된 자동 공격 사용 여부를 제어합니다.
        /// </summary>
        public bool AutoAttackEnabledByToggle
        {
            get => m_autoAttack != null && m_autoAttack.EnabledByToggle;
            set
            {
                if (m_autoAttack != null)
                {
                    m_autoAttack.EnabledByToggle = value;

                    // [추가]: 토글이 켜지는 순간 게임이 시작된 상태라면 즉시 공격 루프 가동 시도
                    if (value && m_isGameStarted)
                    {
                        if (!m_autoAttack.IsActive)
                        {
                            m_autoAttack.Enable();
                        }
                    }
                }
            }
        }

        /// <summary> [설명]: 자동 공격 시스템에 대한 직접적인 접근을 제공합니다. </summary>
        public PlayerAutoAttackSystem AutoAttack => m_autoAttack;

        #endregion

        #region 유니티 생명주기

        /// <summary> [설명]: 필수 하위 시스템 컴포넌트를 동적으로 생성하고 부착합니다. </summary>
        private void Awake()
        {
            m_autoAttack = gameObject.AddComponent<PlayerAutoAttackSystem>();
        }

        /// <summary> [설명]: 조이스틱 등 외부 의존성을 확인하여 입력 시스템을 가동합니다. </summary>
        private void Start()
        {
            if (m_playerCtx != null && m_inputHandler == null)
            {
                m_inputHandler = new PlayerInputHandler(m_playerCtx.Joystick);
            }

            SubscribeEvents();
        }

        /// <summary> [설명]: 비활성화 시 연동된 이벤트를 해제하고 시스템을 안전하게 중단합니다. </summary>
        private void OnDisable()
        {
            UnsubscribeEvents();
            if (m_autoAttack != null)
            {
                m_autoAttack.Disable();
            }
        }

        /// <summary> [설명]: 게임 상태에 따라 매 프레임 입력 및 캐릭터 제어 로직을 실행합니다. </summary>
        private void Update()
        {
            if (!m_isGameStarted)
            {
                return;
            }

            m_inputHandler?.HandleInput();
            HandleControlLogic();
        }

        #endregion

        #region 초기화 및 바인딩

        /// <summary>
        /// [설명]: 게임 시스템 및 컨텍스트를 주입받아 초기화합니다.
        /// </summary>
        public void Initialize(IGameStateService gameState, IPlayerContext playerContext)
        {
            m_gameState = gameState;
            m_playerCtx = playerContext;

            if (m_playerCtx != null && m_inputHandler == null)
            {
                m_inputHandler = new PlayerInputHandler(m_playerCtx.Joystick);
            }

            // 의존성이 주입된 시점에 즉시 이벤트 구독 시도
            SubscribeEvents();
        }

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 선택된 캐릭터 인스턴스를 컨트롤러에 할당하고 카메라 및 UI 등 연계 모듈을 동기화합니다.
        /// </summary>
        public void AssignCharacter(PlayerBase character, MobManager mobManager, PlayerCameraAgent cameraAgent = null, PlayerHUD playerHUD = null)
        {
            if (character == null)
            {
                return;
            }

            m_playerCharacter = character;
            m_playerCharacter.transform.SetParent(m_playerObject.transform, false);

            if (cameraAgent != null)
            {
                cameraAgent.SetTarget(m_playerCharacter.transform);
            }

            if (playerHUD != null)
            {
                playerHUD.Initialize(m_playerCharacter);
            }

            m_playerAnimator = m_playerCharacter.GetComponent<Animator>();

            m_movement = new PlayerMovement(m_playerCharacter, m_playerObject.transform, m_playerCharacter.transform, m_mapRange);

            m_autoAttack.Init(m_playerObject.transform, m_playerCharacter, mobManager, m_enemyLayer, m_detectionRadius, m_attackRadius);

            m_previousHealth = m_playerCharacter.CurrentHealth;

            m_playerCharacter.OnHealthChanged += OnPlayerHealthChanged;
        }

        /// <summary>
        /// [설명]: 주위에 적이 있을 경우 적 방향을, 없을 경우 입력 방향을 고려한 최적의 공격 방향을 계산합니다.
        /// </summary>
        public Vector3 GetCalculatedAttackDirection()
        {
            if (m_autoAttack == null)
            {
                return Vector3.zero;
            }

            ITargetable closestEnemy = m_autoAttack.FindClosestEnemy(m_autoAttack.DetectionRadius);
            if (closestEnemy != null)
            {
                return (closestEnemy.Position - m_playerObject.transform.position).normalized;
            }

            if (m_inputHandler != null && m_inputHandler.IsMoving)
            {
                return m_inputHandler.MoveDirection;
            }

            return Vector3.zero;
        }

        #endregion

        #region 핵심 제어 로직

        /// <summary>
        /// [설명]: 조이스틱 방향과 자동 공격 시스템 상태를 비교하여 이동 및 애니메이션 우선순위를 결정합니다.
        /// </summary>
        private void HandleControlLogic()
        {
            if (m_inputHandler == null || m_movement == null || m_autoAttack == null)
            {
                return;
            }

            Vector3 joystickDir = m_inputHandler.MoveDirection;
            bool isJoystickActive = m_inputHandler.IsMoving;

            // 이동 우선순위: 수동 조작(조이스틱) > 자동 추적
            if (isJoystickActive)
            {
                if (m_autoAttack.IsActive)
                {
                    m_autoAttack.Disable();
                }
                m_movement.Move(joystickDir);
            }
            else
            {
                // 입력이 끊겼을 때 토글이 온(On) 상태라면 자동 추적 가동
                if (m_autoAttack.EnabledByToggle && !m_autoAttack.IsActive)
                {
                    m_autoAttack.Enable();
                }

                if (m_autoAttack.IsActive)
                {
                    m_movement.Move(m_autoAttack.AutoMoveDirection);
                }
            }

            // 이동 속도에 따른 애니메이션 파라미터 갱신
            float currentMoveSpeed = isJoystickActive
                ? joystickDir.magnitude
                : (m_autoAttack.IsActive ? m_autoAttack.AutoMoveDirection.magnitude : 0f);

            UpdateAnimationState(currentMoveSpeed);

            // 현재 시스템이 자동 모드가 아닐 때만 수동 조준/공격 판정을 별도로 실행
            if (!m_autoAttack.IsActive)
            {
                ProcessManualAttack(joystickDir, isJoystickActive);
            }
        }

        /// <summary>
        /// [설명]: 수동 조작 중에 탐색 범위 내의 적을 우선 조준하거나 입력 방향으로 무기 공격을 시도합니다.
        /// </summary>
        private void ProcessManualAttack(Vector3 joystickDir, bool isJoystickActive)
        {
            Vector3 attackDirection = Vector3.zero;

            // 항상 사거리 내의 최우선 적을 찾아 조준 지향
            ITargetable closestEnemy = m_autoAttack.FindClosestEnemy(m_autoAttack.DetectionRadius);

            if (closestEnemy != null)
            {
                attackDirection = (closestEnemy.Position - m_playerObject.transform.position).normalized;
            }
            else if (isJoystickActive)
            {
                attackDirection = joystickDir;
            }

            if (attackDirection != Vector3.zero)
            {
                TryAttack(attackDirection);
            }
        }

        /// <summary>
        /// [설명]: 플레이어가 보유한 모든 활성 무기 인스턴스에 공격 실행 명령을 내립니다.
        /// </summary>
        /// <param name="dir">공격 타겟 방향</param>
        private void TryAttack(Vector3 dir)
        {
            if (m_playerCharacter == null || m_playerCharacter.Weapons == null)
            {
                return;
            }

            foreach (var weapon in m_playerCharacter.Weapons)
            {
                if (weapon != null)
                {
                    weapon.Attack(dir);
                }
            }
        }

        #endregion

        #region 시각 연출 및 애니메이션

        /// <summary>
        /// [설명]: 이동 속도 값을 기반으로 애니메이터의 걷기 블렌드 트리를 갱신합니다.
        /// </summary>
        private void UpdateAnimationState(float speed)
        {
            if (m_playerAnimator != null)
            {
                m_playerAnimator.SetFloat(k_AnimWalk, speed);
            }
        }

        /// <summary>
        /// [설명]: 플레이어의 체력 변화를 감지하여 데미지 입을 시 피격 애니메이션 트리거를 작동시킵니다.
        /// </summary>
        private void OnPlayerHealthChanged(float current, float max)
        {
            if (current < m_previousHealth && current > 0)
            {
                if (m_playerAnimator != null)
                {
                    m_playerAnimator.SetTrigger(k_AnimHit);
                }
            }

            m_previousHealth = current;
        }

        #endregion

        #region 이벤트 핸들링

        /// <summary> [설명]: 외부 전역 상태 변경 및 내부 모듈 이벤트를 구독합니다. </summary>
        private void SubscribeEvents()
        {
            if (m_isSubscribed) return;

            if (m_gameState != null && m_gameState.State != null)
            {
                m_gameState.State.OnGameStart += OnGameStart;
                m_gameState.State.OnGamePause += OnGamePause;
                m_gameState.State.OnGameResume += OnGameResume;
                m_gameState.State.OnGameOver += OnGameOver;
                m_isSubscribed = true;
                LogManager.Log("[PlayerController] 게임 상태 이벤트 구독 성공", LogManager.LogCategory.System);
            }

            if (m_autoAttack != null)
            {
                m_autoAttack.OnAttackRequested += TryAttack;
            }
        }

        /// <summary> [설명]: 객체 파기 또는 비활성화 시 모든 이벤트 연결을 해제합니다. </summary>
        private void UnsubscribeEvents()
        {
            if (!m_isSubscribed) return;

            if (m_gameState != null && m_gameState.State != null)
            {
                m_gameState.State.OnGameStart -= OnGameStart;
                m_gameState.State.OnGamePause -= OnGamePause;
                m_gameState.State.OnGameResume -= OnGameResume;
                m_gameState.State.OnGameOver -= OnGameOver;
                m_isSubscribed = false;
            }

            if (m_playerCharacter != null)
            {
                m_playerCharacter.OnHealthChanged -= OnPlayerHealthChanged;
            }

            if (m_autoAttack != null)
            {
                m_autoAttack.OnAttackRequested -= TryAttack;
            }
        }

        /// <summary> [설명]: 게임 시작 시 제어 플래그를 활성화합니다. </summary>
        private void OnGameStart() => m_isGameStarted = true;

        /// <summary> [설명]: 일시 정지 시 제어 플래그를 해제하여 동작을 멈춥니다. </summary>
        private void OnGamePause() => m_isGameStarted = false;

        /// <summary> [설명]: 일시 정지 해제 시 제어 플래그를 다시 활성화합니다. </summary>
        private void OnGameResume() => m_isGameStarted = true;

        /// <summary>
        /// [설명]: 게임 오버 상황에서 모든 입력을 차단하고 캐릭터의 사망 연출 트리거를 호출합니다.
        /// timeScale이 0이 되어도 애니메이션이 플레이될 수 있도록 updateMode를 변경합니다.
        /// </summary>
        private void OnGameOver()
        {
            m_isGameStarted = false;

            if (m_autoAttack != null)
            {
                m_autoAttack.Disable();
            }

            if (m_playerAnimator != null)
            {
                m_playerAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
                m_playerAnimator.SetTrigger(k_AnimDie);
            }
        }

        #endregion
    }
}