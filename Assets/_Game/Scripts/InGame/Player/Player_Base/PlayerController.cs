using InGame.Manager;
using InGame.Mob.MobBase;
using UnityEngine;
using UnityEngine.UI;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 전체적인 제어를 관리하는 페사드(Facade) 클래스입니다.
    /// 입력 처리, 이동, 카메라 추적, 자동 공격 시스템 등을 통합 관리합니다.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        #region 설정 데이터

        [Header("오브젝트 참조")]
        [SerializeField] private GameObject m_playerObject;
        [SerializeField] private PlayerBase m_playerCharacter;
        [SerializeField] private Slider m_playerHpSliderPrefab;
        [SerializeField] private SpriteRenderer m_mapRange;

        [Header("카메라 설정")]
        [SerializeField] private float m_cameraSmoothTime = 0.1f;

        [Header("자동 공격 설정")] 
        [SerializeField] private LayerMask m_enemyLayer;
        [SerializeField] private float m_detectionRadius = 10f;
        [SerializeField] private float m_attackRadius = 1.5f;

        #endregion

        #region 하위 시스템 및 컴포넌트

        private PlayerInputHandler m_inputHandler;
        private PlayerMovement m_movement;
        private PlayerCameraController m_cameraController;
        private PlayerUIHandler m_uiHandler;
        private PlayerAutoAttackSystem m_autoAttack;
        private Animator m_playerAnimator;

        #endregion

        #region 내부 상태 변수

        private bool m_isGameStarted;
        private float m_previousHealth;

        // 애니메이터 파라미터 해시
        private static readonly int k_AnimWalk = Animator.StringToHash("Walk");
        private static readonly int k_AnimHit = Animator.StringToHash("Hit");
        private static readonly int k_AnimDie = Animator.StringToHash("Die");

        #endregion

        #region 프로퍼티

        /// <summary>
        /// 현재 플레이어의 이동 방향 (입력 기준)
        /// </summary>
        public Vector3 MoveDirection => m_inputHandler != null ? (Vector3)m_inputHandler.MoveDirection : Vector3.zero;

        /// <summary>
        /// 토글에 의한 자동 공격 활성화 여부
        /// </summary>
        public bool AutoAttackEnabledByToggle
        {
            get => m_autoAttack != null && m_autoAttack.EnabledByToggle;
            set { if (m_autoAttack != null) m_autoAttack.EnabledByToggle = value; }
        }

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            // 자동 공격 시스템은 MonoBehaviour로 유지하며 런타임에 추가합니다.
            m_autoAttack = gameObject.AddComponent<PlayerAutoAttackSystem>();
        }

        private void Start()
        {
            m_inputHandler = new PlayerInputHandler(GameManager.Instance.Joystick);
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            m_autoAttack?.Disable();
        }

        private void Update()
        {
            if (!m_isGameStarted) return;
            
            m_inputHandler?.HandleInput(); // 입력 갱신
            HandleControlLogic();          // 제어 로직 처리
        }

        private void LateUpdate()
        {
             m_cameraController?.OnLateUpdate(); // 카메라 추적
        }

        #endregion

        #region 초기화 및 제어 로직

        /// <summary>
        /// 플레이어 캐릭터를 할당하고 하위 시스템을 초기화합니다.
        /// </summary>
        public void AssignCharacter(PlayerBase character)
        {
            if (character == null) return;
            
            m_playerCharacter = character;
            m_playerCharacter.transform.SetParent(m_playerObject.transform, false);
            m_playerAnimator = m_playerCharacter.GetComponent<Animator>();
            
            // 하위 POCO 시스템들 생성 및 의존성 주입
            m_movement = new PlayerMovement(m_playerCharacter, m_playerObject.transform, m_playerCharacter.transform, m_mapRange);
            m_cameraController = new PlayerCameraController(GameManager.Instance.MainCamera, m_playerObject.transform, m_mapRange, m_cameraSmoothTime);
            m_uiHandler = new PlayerUIHandler(m_playerHpSliderPrefab, m_playerObject.transform);
            
            // MonoBehaviour 시스템 초기화
            m_autoAttack.Init(m_playerObject.transform, m_playerCharacter, m_enemyLayer, m_detectionRadius, m_attackRadius);

            // 초기 UI 상태 설정
            m_previousHealth = m_playerCharacter.CurrentHealth;
            m_uiHandler.UpdateHpUI(m_playerCharacter.CurrentHealth, m_playerCharacter.MaxHealth);
            
            // 플레이어 캐릭터와 브릿지 연결
            m_playerCharacter.OnHealthChanged += OnPlayerHealthChanged;
            m_playerCharacter.SetTargetProvider(GetCalculatedAttackDirection);
            m_cameraController.ResetPosition();
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

        private void HandleControlLogic()
        {
            Vector3 joystickDir = m_inputHandler.MoveDirection;
            bool isJoystickActive = m_inputHandler.IsMoving;

            if (isJoystickActive)
            {
                // 수동 조작 시 자동 공격 모드 중단
                if (m_autoAttack.IsActive) m_autoAttack.Disable();
                m_movement.Move(joystickDir);
            }
            else
            {
                // 입력이 없고 자동 공격 토글이 켜져 있으면 시스템 활성화
                if (m_autoAttack.EnabledByToggle && !m_autoAttack.IsActive)
                {
                    m_autoAttack.Enable();
                }

                if (m_autoAttack.IsActive)
                {
                    m_movement.Move(m_autoAttack.AutoMoveDirection);
                }
            }

            // 이동 애니메이션 업데이트
            float currentMoveSpeed = isJoystickActive ? joystickDir.magnitude : (m_autoAttack.IsActive ? m_autoAttack.AutoMoveDirection.magnitude : 0f);
            UpdateAnimationState(currentMoveSpeed);

            // 매 프레임 공격 판정 시도 (자동 공격 시스템 비활성 시에만 직접 처리)
            if (!m_autoAttack.IsActive)
            {
                ProcessAttack(joystickDir, isJoystickActive);
            }
        }

        private void ProcessAttack(Vector3 joystickDir, bool isJoystickActive)
        {
            Vector3 attackDirection = Vector3.zero;
            MobBase closestEnemy = m_autoAttack.FindClosestEnemy();

            if (closestEnemy != null)
            {
                attackDirection = (closestEnemy.transform.position - m_playerObject.transform.position).normalized;
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

        #endregion

        #region 애니메이션 및 시각 효과

        private void UpdateAnimationState(float speed)
        {
            if (m_playerAnimator != null) m_playerAnimator.SetFloat(k_AnimWalk, speed);
        }

        #endregion

        #region 공격 시스템

        private void TryAttack(Vector3 dir)
        {
            if (m_playerCharacter == null || m_playerCharacter.Weapons == null || m_playerCharacter.Weapons.Count == 0) return;
            
            foreach (var weapon in m_playerCharacter.Weapons)
            {
                weapon?.Attack(dir);
            }
        }

        #endregion

        #region 이벤트 핸들러

        private void SubscribeEvents()
        {
            if (GameManager.Instance.State == null) return;
            GameManager.Instance.State.OnGameStart += OnGameStart;
            GameManager.Instance.State.OnGamePause += OnGamePause;
            GameManager.Instance.State.OnGameResume += OnGameResume;
            GameManager.Instance.State.OnGameOver += OnGameOver;
            
            m_autoAttack.OnAttackRequested += TryAttack;
        }

        private void UnsubscribeEvents()
        {
            if (GameManager.Instance.State == null) return;
            GameManager.Instance.State.OnGameStart -= OnGameStart;
            GameManager.Instance.State.OnGamePause -= OnGamePause;
            GameManager.Instance.State.OnGameResume -= OnGameResume;
            GameManager.Instance.State.OnGameOver -= OnGameOver;
            
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
            m_autoAttack?.Disable();
            if (m_playerAnimator != null) m_playerAnimator.SetTrigger(k_AnimDie);
        }

        private void OnPlayerHealthChanged(float current, float max)
        {
            m_uiHandler.UpdateHpUI(current, max);
            
            // 데미지 입었을 때 피격 애니메이션 트리거
            if (current < m_previousHealth && current > 0)
            {
                if (m_playerAnimator != null) m_playerAnimator.SetTrigger(k_AnimHit);
            }
            m_previousHealth = current;
        }

        #endregion
    }
}