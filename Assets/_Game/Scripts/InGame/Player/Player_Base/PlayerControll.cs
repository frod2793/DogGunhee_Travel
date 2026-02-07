using InGame.Manager;
using InGame.Mob.MobBase;
using UnityEngine;
using UnityEngine.UI;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 전체적인 제어를 관리하는 Facade 클래스입니다.
    /// </summary>
    public class PlayerControll : MonoBehaviour
    {
        #region 인스펙터 필드
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

        #region 하위 컴포넌트
        private PlayerInputHandler m_inputHandler;
        private PlayerMovement m_movement;
        private PlayerCameraController m_cameraController;
        private PlayerUIHandler m_uiHandler;
        private PlayerAutoAttackSystem m_autoAttack;
        #endregion

        #region 내부 상태 변수
        private Animator m_playerAnimator;
        private bool m_isGameStarted;
        private float m_previousHealth;

        private static readonly int k_AnimWalk = Animator.StringToHash("Walk");
        private static readonly int k_AnimHit = Animator.StringToHash("Hit");
        private static readonly int k_AnimDie = Animator.StringToHash("Die");

        public Vector3 MoveDirection => m_inputHandler != null ? (Vector3)m_inputHandler.MoveDirection : Vector3.zero;

        public bool AutoAttackEnabledByToggle
        {
            get => m_autoAttack != null && m_autoAttack.EnabledByToggle;
            set { if (m_autoAttack != null) m_autoAttack.EnabledByToggle = value; }
        }
        #endregion

        #region 유니티 생명주기
        private void Awake()
        {
            // POCO 클래스는 필요한 시점에 생성합니다.
            // PlayerAutoAttackSystem은 아직 MonoBehaviour입니다.
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
            m_inputHandler?.HandleInput(); // 입력 처리
            HandleControlLogic();
        }

        private void LateUpdate()
        {
             m_cameraController?.OnLateUpdate(); // 카메라 추적
        }
        #endregion

        #region 메인 로직
        private void HandleControlLogic()
        {
            Vector3 joystickDir = m_inputHandler.MoveDirection;
            bool isJoystickActive = m_inputHandler.IsMoving;

            if (isJoystickActive)
            {
                if (m_autoAttack.IsActive) m_autoAttack.Disable();
                m_movement.Move(joystickDir);
            }
            else
            {
                if (m_autoAttack.EnabledByToggle && !m_autoAttack.IsActive)
                {
                    m_autoAttack.Enable();
                }

                if (m_autoAttack.IsActive)
                {
                    m_movement.Move(m_autoAttack.AutoMoveDirection);
                }
            }

            float moveSpeed = isJoystickActive ? joystickDir.magnitude : (m_autoAttack.IsActive ? m_autoAttack.AutoMoveDirection.magnitude : 0f);
            UpdateAnimationState(moveSpeed);

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

        #region 초기화 및 이벤트 관리
        private void SubscribeEvents()
        {
            PlayStateManager.OnGameStart += OnGameStart;
            PlayStateManager.OnGamePause += OnGamePause;
            PlayStateManager.OnGameResume += OnGameResume;
            PlayStateManager.OnGameOver += OnGameOver;
            
            m_autoAttack.OnAttackRequested += TryAttack;
        }

        private void UnsubscribeEvents()
        {
            PlayStateManager.OnGameStart -= OnGameStart;
            PlayStateManager.OnGamePause -= OnGamePause;
            PlayStateManager.OnGameResume -= OnGameResume;
            PlayStateManager.OnGameOver -= OnGameOver;
            
            if (m_playerCharacter != null)
            {
                m_playerCharacter.OnHealthChanged -= OnPlayerHealthChanged;
            }
            
            if (m_autoAttack != null)
            {
                m_autoAttack.OnAttackRequested -= TryAttack;
            }
        }

        public void AssignCharacter(PlayerBase character)
        {
            if (character == null) return;
            
            m_playerCharacter = character;
            m_playerCharacter.transform.SetParent(m_playerObject.transform, false);
            m_playerAnimator = m_playerCharacter.GetComponent<Animator>();
            
            m_movement = new PlayerMovement(m_playerCharacter, m_playerObject.transform, m_playerCharacter.transform, m_mapRange);
            m_cameraController = new PlayerCameraController(GameManager.Instance.MainCamera, m_playerObject.transform, m_mapRange, m_cameraSmoothTime);
            m_uiHandler = new PlayerUIHandler(m_playerHpSliderPrefab, m_playerObject.transform);
            m_autoAttack.Init(m_playerObject.transform, m_playerCharacter, m_enemyLayer, m_detectionRadius, m_attackRadius);

            m_previousHealth = m_playerCharacter.CurrentHealth;
            m_uiHandler.UpdateHpUI(m_playerCharacter.CurrentHealth, m_playerCharacter.MaxHealth);
            
            m_playerCharacter.OnHealthChanged += OnPlayerHealthChanged;
            m_playerCharacter.SetTargetProvider(GetCalculatedAttackDirection);
            m_cameraController.ResetPosition();
        }

        private Vector3 GetCalculatedAttackDirection()
        {
            if (m_inputHandler == null || m_autoAttack == null) return transform.up;

            Vector3 joystickDir = m_inputHandler.MoveDirection;
            bool isJoystickActive = m_inputHandler.IsMoving;

            MobBase closestEnemy = m_autoAttack.FindClosestEnemy();

            if (closestEnemy != null)
            {
                return (closestEnemy.transform.position - m_playerObject.transform.position).normalized;
            }
            
            if (isJoystickActive)
            {
                return joystickDir;
            }

            // 자동 이동 중이면 자동 이동 방향
            if (m_autoAttack.IsActive)
            {
                return m_autoAttack.AutoMoveDirection;
            }
            
            return Vector3.zero;
        }

        private void OnPlayerHealthChanged(float current, float max)
        {
            m_uiHandler.UpdateHpUI(current, max);
            if (current < m_previousHealth && current > 0)
            {
                if (m_playerAnimator != null) m_playerAnimator.SetTrigger(k_AnimHit);
            }
            m_previousHealth = current;
        }
        #endregion

        #region 게임 상태 핸들러
        private void OnGameStart() => m_isGameStarted = true;
        private void OnGamePause() => m_isGameStarted = false;
        private void OnGameResume() => m_isGameStarted = true;
        
        private void OnGameOver()
        {
            m_isGameStarted = false;
            m_autoAttack?.Disable();
            if (m_playerAnimator != null) m_playerAnimator.SetTrigger(k_AnimDie);
        }
        #endregion

        #region 공격 시스템
        private void TryAttack(Vector3 dir)
        {
            if (m_playerCharacter == null || m_playerCharacter.Weapons == null || m_playerCharacter.Weapons.Count == 0) return;
            foreach (var weapon in m_playerCharacter.Weapons)
            {
                if (weapon != null) weapon.Weapon_Attack(dir);
            }
        }

        private void UpdateAnimationState(float speed)
        {
            if (m_playerAnimator != null) m_playerAnimator.SetFloat(k_AnimWalk, speed);
        }
        #endregion
    }
}