
using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Manager;
using InGame.Mob.MobBase;
using UnityEngine;
using UnityEngine.UI;
using InGame;

namespace InGame.Player.Player_Base
{
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

        #region 내부 상태 변수

        private const float k_JoystickInputThreshold = 0.1f;
        private const int k_MaxEnemyColliders = 20; 

        private GameManager m_gameManager;
        private Animator m_playerAnimator;
        private Camera m_mainCamera;
        private VariableJoystick m_joystick;

        private Slider m_playerHpSlider;
        private float m_previousHealth;

        private bool m_isGameStarted;
        private bool m_isAutoAttackActive;
        private bool m_autoAttackEnabledByToggle;

        private CancellationTokenSource m_autoMoveAttackCts;
        private Vector3 m_autoMoveDirection;
        
        private Vector3 m_cameraVelocity = Vector3.zero;
        private ContactFilter2D m_contactFilter;
        private readonly Collider2D[] m_enemyColliders = new Collider2D[k_MaxEnemyColliders]; 

        private static readonly int k_AnimWalk = Animator.StringToHash("Walk");
        private static readonly int k_AnimHit = Animator.StringToHash("Hit");
        private static readonly int k_AnimDie = Animator.StringToHash("Die");

        public Vector3 MoveDirection { get; private set; }

        public bool AutoAttackEnabledByToggle
        {
            get => m_autoAttackEnabledByToggle;
            set
            {
                if (m_autoAttackEnabledByToggle == value) return;
                m_autoAttackEnabledByToggle = value;
                if (!m_autoAttackEnabledByToggle && m_isAutoAttackActive)
                {
                    DisableAutoMoveAttack();
                }
            }
        }

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            m_contactFilter.useTriggers = true;
            m_contactFilter.SetLayerMask(m_enemyLayer);
            m_contactFilter.useLayerMask = true;
        }

        private void Start()
        {
            m_gameManager = GameManager.Instance;
            m_joystick = m_gameManager.Joystick;
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            DisableAutoMoveAttack();
        }

        private void FixedUpdate()
        {
            if (!m_isGameStarted) return;
            HandleMovementInput();
            ProcessMovement();
            FollowCamera();
        }

        #endregion
        
        #region 초기화 및 이벤트 관리

        private void SubscribeEvents()
        {
            PlayStateManager.OnGameStart += OnGameStart;
            PlayStateManager.OnGamePause += OnGamePause;
            PlayStateManager.OnGameResume += OnGameResume;
            PlayStateManager.OnGameOver += OnGameOver;
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
        }

        public void AssignCharacter(PlayerBase character)
        {
            if (character == null) return;
            m_playerCharacter = character;
            m_playerCharacter.transform.SetParent(m_playerObject.transform, false);
            m_playerAnimator = m_playerCharacter.GetComponent<Animator>();
            m_mainCamera = GameManager.Instance.MainCamera;
            m_previousHealth = m_playerCharacter.CurrentHealth;
            CreateHpSlider();
            m_playerCharacter.OnHealthChanged += OnPlayerHealthChanged;
            ResetCameraPosition();
        }

        private void CreateHpSlider()
        {
            if (m_playerHpSliderPrefab == null) return;
            m_playerHpSlider = Instantiate(m_playerHpSliderPrefab, m_playerObject.transform);
            m_playerHpSlider.transform.localPosition = new Vector3(0, -0.4f, 0); 
            UpdateHpSliderUI(m_playerCharacter.CurrentHealth, m_playerCharacter.MaxHealth);
        }

        private void OnPlayerHealthChanged(float current, float max)
        {
            UpdateHpSliderUI(current, max);
            if (current < m_previousHealth && current > 0)
            {
                if (m_playerAnimator != null)
                {
                    m_playerAnimator.SetTrigger(k_AnimHit);
                }
            }
            m_previousHealth = current;
        }

        private void UpdateHpSliderUI(float current, float max)
        {
            if (m_playerHpSlider != null)
            {
                m_playerHpSlider.value = current;
                m_playerHpSlider.maxValue = max;
            }
        }

        #endregion

        #region 게임 상태 핸들러

        private void OnGameStart() => m_isGameStarted = true;
        private void OnGamePause() => m_isGameStarted = false;
        private void OnGameResume() => m_isGameStarted = true;
        
        private void OnGameOver()
        {
            m_isGameStarted = false;
            DisableAutoMoveAttack();
            m_isAutoAttackActive = false;
            if (m_playerAnimator != null)
            {
                m_playerAnimator.SetTrigger(k_AnimDie);
            }
        }

        #endregion

        #region 이동 및 조작 로직

        private void HandleMovementInput()
        {
            Vector3 joystickDir = GetJoystickInputDirection();
            bool isJoystickActive = joystickDir.sqrMagnitude > k_JoystickInputThreshold * k_JoystickInputThreshold;

            // --- 1. 이동 방향 결정 및 자동 공격 상태 관리 ---
            if (isJoystickActive)
            {
                if (m_isAutoAttackActive) DisableAutoMoveAttack();
                MoveDirection = joystickDir;
            }
            else // 조이스틱 입력 없음
            {
                // 자동 공격 토글이 켜져 있고, 현재 자동 공격 모드가 아니라면 활성화합니다.
                if (m_autoAttackEnabledByToggle && !m_isAutoAttackActive)
                {
                    EnableAutoMoveAttack();
                }
                // 자동 공격 모드라면 적 방향으로 이동, 아니면 제자리에 멈춥니다.
                MoveDirection = m_isAutoAttackActive ? m_autoMoveDirection : Vector3.zero;
            }
            UpdateAnimationState(MoveDirection.magnitude);

            // --- 2. 공격 방향 결정 및 공격 실행 ---
            Vector3 attackDirection = Vector3.zero;
            MobBase closestEnemy = FindClosestEnemy();

            if (closestEnemy != null)
            {
                // 가장 가까운 적이 있으면 그 방향으로 공격
                attackDirection = (closestEnemy.transform.position - m_playerObject.transform.position).normalized;
            }
            else if (isJoystickActive)
            {
                // 적이 없고 조이스틱을 움직이면, 그 방향으로 공격
                attackDirection = joystickDir;
            }

            // [수정] 자동 공격 모드가 아닐 때, 공격 방향이 정해졌다면 공격 실행
            if (attackDirection != Vector3.zero && !m_isAutoAttackActive)
            {
                TryAttack(attackDirection);
            }
        }
        
        private Vector3 GetJoystickInputDirection()
        {
            if (m_joystick == null) return Vector3.zero;
            return new Vector3(m_joystick.Horizontal, m_joystick.Vertical, 0).normalized;
        }

        private void ProcessMovement()
        {
            if (m_playerObject == null || m_playerCharacter == null || MoveDirection == Vector3.zero) return;
            float speed = m_playerCharacter.MoveSpeed * Time.fixedDeltaTime;
            Vector3 targetPos = m_playerObject.transform.position + MoveDirection * speed;
            m_playerObject.transform.position = ClampPositionToMap(targetPos);
            UpdateCharacterRotation(MoveDirection);
        }

        private Vector3 ClampPositionToMap(Vector3 position)
        {
            if (m_mapRange == null) return position;
            Bounds bounds = m_mapRange.bounds;
            float x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            float y = Mathf.Clamp(position.y, bounds.min.y, bounds.max.y);
            return new Vector3(x, y, 0f);
        }

        private void UpdateCharacterRotation(Vector3 dir)
        {
            if (dir.sqrMagnitude > 0.01f && m_playerCharacter != null)
            {
                float yRot = dir.x < 0 ? 0f : 180f; 
                m_playerCharacter.transform.rotation = Quaternion.Euler(0, yRot, 0);
            }
        }

        private void UpdateAnimationState(float speed)
        {
            if (m_playerAnimator != null)
            {
                m_playerAnimator.SetFloat(k_AnimWalk, speed);
            }
        }

        #endregion

        #region 공격 시스템

        private void TryAttack(Vector3 dir)
        {
            if (m_playerCharacter == null || m_playerCharacter.Weapons.Count == 0) return;
            foreach (var weapon in m_playerCharacter.Weapons)
            {
                if (weapon != null)
                {
                    weapon.Weaphon_Attack(dir);
                }
            }
        }

        #endregion

        #region 자동 공격 시스템

        private void EnableAutoMoveAttack()
        {
            if (m_isAutoAttackActive) return;
            m_isAutoAttackActive = true;
            m_autoMoveAttackCts?.Cancel();
            m_autoMoveAttackCts = new CancellationTokenSource();
            AutoAttackLoopAsync(m_autoMoveAttackCts.Token).Forget();
        }

        private void DisableAutoMoveAttack()
        {
            if (!m_isAutoAttackActive) return;
            m_isAutoAttackActive = false;
            m_autoMoveDirection = Vector3.zero;
            m_autoMoveAttackCts?.Cancel();
            m_autoMoveAttackCts?.Dispose();
            m_autoMoveAttackCts = null;
        }

        private async UniTaskVoid AutoAttackLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!m_isGameStarted || m_playerCharacter == null)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    continue;
                }

                MobBase target = FindClosestEnemy();
                
                if (target != null)
                {
                    Vector3 targetPos = target.transform.position;
                    Vector3 myPos = m_playerObject.transform.position;
                    Vector3 dirToTarget = (targetPos - myPos).normalized;
                    float dist = Vector3.Distance(myPos, targetPos);

                    if (dist > m_attackRadius * 0.9f)
                    {
                        m_autoMoveDirection = dirToTarget;
                    }
                    else
                    {
                        m_autoMoveDirection = Vector3.zero;
                    }
                    if (dist <= m_attackRadius * 1.2f)
                    {
                        TryAttack(dirToTarget);
                    }
                }
                else
                {
                    m_autoMoveDirection = Vector3.zero;
                }
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        private MobBase FindClosestEnemy()
        {
            if (m_playerObject == null) return null;
            int count = Physics2D.OverlapCircle(m_playerObject.transform.position, m_detectionRadius, m_contactFilter, m_enemyColliders);
            MobBase closest = null;
            float minDstSqr = float.MaxValue;
            Vector3 myPos = m_playerObject.transform.position;

            for (int i = 0; i < count; i++)
            {
                var col = m_enemyColliders[i];
                if (col.TryGetComponent(out MobBase mob) && !mob.IsDead)
                {
                    float dstSqr = (mob.transform.position - myPos).sqrMagnitude;
                    if (dstSqr < minDstSqr)
                    {
                        minDstSqr = dstSqr;
                        closest = mob;
                    }
                }
            }
            return closest;
        }

        #endregion

        #region 카메라 추적

        private void ResetCameraPosition()
        {
            if (m_mainCamera == null || m_playerObject == null || m_mapRange == null) return;
            Vector3 targetPos = m_playerObject.transform.position;
            targetPos.z = m_mainCamera.transform.position.z;
            Bounds bounds = m_mapRange.bounds;
            float camHeight = m_mainCamera.orthographicSize;
            float camWidth = camHeight * m_mainCamera.aspect;
            targetPos.x = Mathf.Clamp(targetPos.x, bounds.min.x + camWidth, bounds.max.x - camWidth);
            targetPos.y = Mathf.Clamp(targetPos.y, bounds.min.y + camHeight, bounds.max.y - camHeight);
            m_mainCamera.transform.position = targetPos;
        }

        private void FollowCamera()
        {
            if (m_mainCamera == null || m_playerObject == null || m_mapRange == null) return;
            Vector3 targetPos = m_playerObject.transform.position;
            targetPos.z = m_mainCamera.transform.position.z;
            Bounds bounds = m_mapRange.bounds;
            float camHeight = m_mainCamera.orthographicSize;
            float camWidth = camHeight * m_mainCamera.aspect;
            targetPos.x = Mathf.Clamp(targetPos.x, bounds.min.x + camWidth, bounds.max.x - camWidth);
            targetPos.y = Mathf.Clamp(targetPos.y, bounds.min.y + camHeight, bounds.max.y - camHeight);
            m_mainCamera.transform.position = Vector3.SmoothDamp(m_mainCamera.transform.position, targetPos, ref m_cameraVelocity, m_cameraSmoothTime);
        }

        #endregion
    }
}