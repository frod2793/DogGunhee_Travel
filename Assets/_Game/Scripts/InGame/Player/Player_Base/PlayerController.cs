using InGame.Manager;
using InGame.Mob.MobBase;
using UnityEngine;
using UnityEngine.UI;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 전체적인 제어를 관리하는 최상위 컨트롤러(Facade)입니다.
    /// <br/> 입력 처리, 캐릭터 이동, 카메라 추적, 자동 공격 시스템 등을 조율합니다.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("오브젝트 참조")] [SerializeField, Tooltip("플레이어 최상위 오브젝트")]
        private GameObject m_playerObject;

        [SerializeField, Tooltip("플레이어 캐릭터 (데이터/모델)")]
        private PlayerBase m_playerCharacter;

        [SerializeField, Tooltip("HP 슬라이더 프리팹")]
        private Slider m_playerHpSliderPrefab;

        [SerializeField, Tooltip("맵 경계 스프라이트")]
        private SpriteRenderer m_mapRange;

        [Header("카메라 설정")] [SerializeField, Tooltip("카메라 이동 부드러움 정도")]
        private float m_cameraSmoothTime = 0.1f;

        [Header("자동 공격 설정")] [SerializeField, Tooltip("적 감지 레이어")]
        private LayerMask m_enemyLayer;

        [SerializeField, Tooltip("적 감지 반경")] private float m_detectionRadius = 10f;

        [SerializeField, Tooltip("자동 공격 사거리")] private float m_attackRadius = 1.5f;

        #endregion

        #region 2. 하위 시스템 및 컴포넌트

        private PlayerInputHandler m_inputHandler;
        private PlayerMovement m_movement;
        private PlayerCameraController m_cameraController;
        private PlayerUIHandler m_uiHandler;

        private PlayerAutoAttackSystem m_autoAttack;
        private Animator m_playerAnimator;

        #endregion

        #region 3. 내부 상태 변수

        private bool m_isGameStarted;
        private float m_previousHealth;

        // 애니메이터 파라미터 해싱 (최적화)
        private static readonly int k_AnimWalk = Animator.StringToHash("Walk");
        private static readonly int k_AnimHit = Animator.StringToHash("Hit");
        private static readonly int k_AnimDie = Animator.StringToHash("Die");

        #endregion

        #region 4. 공개 프로퍼티 (Properties)

        /// <summary>
        /// 현재 플레이어의 이동 입력 방향
        /// </summary>
        public Vector3 MoveDirection => m_inputHandler != null ? (Vector3)m_inputHandler.MoveDirection : Vector3.zero;

        /// <summary>
        /// 토글 UI에 의한 자동 공격 활성화 여부
        /// </summary>
        public bool AutoAttackEnabledByToggle
        {
            get => m_autoAttack != null && m_autoAttack.EnabledByToggle;
            set
            {
                if (m_autoAttack != null) m_autoAttack.EnabledByToggle = value;
            }
        }

        /// <summary>
        /// 자동 공격 시스템 참조
        /// </summary>
        public PlayerAutoAttackSystem AutoAttack => m_autoAttack;

        #endregion

        #region 5. 유니티 생명주기 (Lifecycle)

        private void Awake()
        {
            // 자동 공격 시스템 컴포넌트 동적 추가
            m_autoAttack = gameObject.AddComponent<PlayerAutoAttackSystem>();
        }

        private void Start()
        {
            // 입력 핸들러 초기화
            if (GameManager.Instance != null)
            {
                m_inputHandler = new PlayerInputHandler(GameManager.Instance.Joystick);
            }

            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            if (m_autoAttack != null)
            {
                m_autoAttack.Disable();
            }
        }

        private void Update()
        {
            if (!m_isGameStarted) return;

            // 1. 입력 처리
            m_inputHandler?.HandleInput();

            // 2. 메인 제어 로직 (이동 및 공격)
            HandleControlLogic();
        }

        private void LateUpdate()
        {
            // 3. 카메라 추적 (이동 후 처리)
            m_cameraController?.OnLateUpdate();
        }

        #endregion

        #region 6. 초기화 및 설정 (Initialization)

        /// <summary>
        /// 플레이어 캐릭터를 할당하고 관련 하위 시스템들을 초기화합니다.
        /// </summary>
        public void AssignCharacter(PlayerBase character)
        {
            if (character == null) return;

            // 캐릭터 설정
            m_playerCharacter = character;
            m_playerCharacter.transform.SetParent(m_playerObject.transform, false);
            m_playerAnimator = m_playerCharacter.GetComponent<Animator>();

            // 하위 POCO 시스템 초기화
            m_movement = new PlayerMovement(m_playerCharacter, m_playerObject.transform, m_playerCharacter.transform,
                m_mapRange);

            if (GameManager.Instance != null)
            {
                m_cameraController = new PlayerCameraController(GameManager.Instance.MainCamera,
                    m_playerObject.transform, m_mapRange, m_cameraSmoothTime);
            }

            m_uiHandler = new PlayerUIHandler(m_playerHpSliderPrefab, m_playerObject.transform);

            // 자동 공격 시스템 초기화
            m_autoAttack.Init(m_playerObject.transform, m_playerCharacter, m_enemyLayer, m_detectionRadius,
                m_attackRadius);

            // 초기 상태 동기화
            m_previousHealth = m_playerCharacter.CurrentHealth;
            m_uiHandler.UpdateHpUI(m_playerCharacter.CurrentHealth, m_playerCharacter.MaxHealth);

            // 이벤트 연결
            m_playerCharacter.OnHealthChanged += OnPlayerHealthChanged;

            // 카메라 초기 위치 리셋
            m_cameraController?.ResetPosition();
        }

        /// <summary>
        /// 현재 상황(적 위치, 입력 등)에 따른 최적의 공격 방향을 계산합니다.
        /// </summary>
        public Vector3 GetCalculatedAttackDirection()
        {
            if (m_autoAttack == null) return Vector3.zero;

            MobBase closestEnemy = m_autoAttack.FindClosestEnemy();

            // 1순위: 감지된 가장 가까운 적 방향 (공격 허용)
            if (closestEnemy != null)
            {
                return (closestEnemy.transform.position - m_playerObject.transform.position).normalized;
            }

            // 2순위: 주변에 적이 없으면 공격 중단 (Vector3.zero 반환)
            return Vector3.zero;
        }

        #endregion

        #region 7. 제어 로직 (Control Logic)

        private void HandleControlLogic()
        {
            if (m_inputHandler == null || m_movement == null || m_autoAttack == null) return;

            Vector3 joystickDir = m_inputHandler.MoveDirection;
            bool isJoystickActive = m_inputHandler.IsMoving;

            // 이동 처리 우선순위: 조이스틱 > 자동 이동
            if (isJoystickActive)
            {
                // 수동 조작 시 자동 공격 로직 일시 중단
                if (m_autoAttack.IsActive) m_autoAttack.Disable();
                m_movement.Move(joystickDir);
            }
            else
            {
                // 입력이 없고 토글이 켜져있으면 자동 공격 활성화
                if (m_autoAttack.EnabledByToggle && !m_autoAttack.IsActive)
                {
                    m_autoAttack.Enable();
                }

                // 자동 이동 수행
                if (m_autoAttack.IsActive)
                {
                    m_movement.Move(m_autoAttack.AutoMoveDirection);
                }
            }

            // 애니메이션 속도 갱신
            float currentMoveSpeed = isJoystickActive
                ? joystickDir.magnitude
                : (m_autoAttack.IsActive ? m_autoAttack.AutoMoveDirection.magnitude : 0f);

            UpdateAnimationState(currentMoveSpeed);

            // 수동 조작 중일 때의 공격 판정 (자동 공격 시스템은 이벤트로 처리됨)
            if (!m_autoAttack.IsActive)
            {
                ProcessManualAttack(joystickDir, isJoystickActive);
            }
        }

        /// <summary>
        /// 수동 조작 시의 공격 방향을 계산하고 공격을 시도합니다.
        /// </summary>
        private void ProcessManualAttack(Vector3 joystickDir, bool isJoystickActive)
        {
            Vector3 attackDirection = Vector3.zero;

            // 1. 가장 가까운 적이 있으면 그 쪽을 향해 공격
            MobBase closestEnemy = m_autoAttack.FindClosestEnemy();
            if (closestEnemy != null)
            {
                attackDirection = (closestEnemy.transform.position - m_playerObject.transform.position).normalized;
            }
            // 2. 적이 없으면 조이스틱 방향으로 공격
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
        /// 실제 무기 공격을 수행합니다.
        /// </summary>
        private void TryAttack(Vector3 dir)
        {
            if (m_playerCharacter == null || m_playerCharacter.Weapons == null) return;

            // 보유한 모든 무기 발사 시도
            foreach (var weapon in m_playerCharacter.Weapons)
            {
                if (weapon != null)
                {
                    weapon.Attack(dir);
                }
            }
        }

        #endregion

        #region 8. 애니메이션 및 UI (Visuals)

        private void UpdateAnimationState(float speed)
        {
            if (m_playerAnimator != null)
            {
                m_playerAnimator.SetFloat(k_AnimWalk, speed);
            }
        }

        private void OnPlayerHealthChanged(float current, float max)
        {
            // UI 갱신
            m_uiHandler?.UpdateHpUI(current, max);

            // 피격 애니메이션 (데미지를 입었을 때만)
            if (current < m_previousHealth && current > 0)
            {
                if (m_playerAnimator != null) m_playerAnimator.SetTrigger(k_AnimHit);
            }

            m_previousHealth = current;
        }

        #endregion

        #region 9. 이벤트 핸들러 (Events)

        private void SubscribeEvents()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != null)
            {
                GameManager.Instance.State.OnGameStart += OnGameStart;
                GameManager.Instance.State.OnGamePause += OnGamePause;
                GameManager.Instance.State.OnGameResume += OnGameResume;
                GameManager.Instance.State.OnGameOver += OnGameOver;
            }

            if (m_autoAttack != null)
            {
                m_autoAttack.OnAttackRequested += TryAttack;
            }
        }

        private void UnsubscribeEvents()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != null)
            {
                GameManager.Instance.State.OnGameStart -= OnGameStart;
                GameManager.Instance.State.OnGamePause -= OnGamePause;
                GameManager.Instance.State.OnGameResume -= OnGameResume;
                GameManager.Instance.State.OnGameOver -= OnGameOver;
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

        private void OnGameStart() => m_isGameStarted = true;
        private void OnGamePause() => m_isGameStarted = false;
        private void OnGameResume() => m_isGameStarted = true;

        private void OnGameOver()
        {
            m_isGameStarted = false;

            if (m_autoAttack != null)
            {
                m_autoAttack.Disable();
            }

            if (m_playerAnimator != null)
            {
                m_playerAnimator.SetTrigger(k_AnimDie);
            }
        }

        #endregion
    }
}