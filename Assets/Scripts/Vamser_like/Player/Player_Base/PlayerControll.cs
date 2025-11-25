using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DG.Tweening;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 플레이어의 이동, 공격, 카메라 추적, 애니메이션을 제어하는 메인 컨트롤러입니다.
    /// </summary>
    public class PlayerControll : MonoBehaviour
    {
        #region 인스펙터 필드

        [Header("오브젝트 참조")]
        [Tooltip("실제 움직임을 담당하는 플레이어의 부모 오브젝트")]
        [FormerlySerializedAs("player")]
        [SerializeField] private GameObject m_playerObject;
        
        [Tooltip("애니메이션과 캐릭터 로직을 담당하는 자식 오브젝트")]
        [FormerlySerializedAs("playerCharactor")]
        [SerializeField] private PlayerBase m_playerCharacter;
        
        [Tooltip("HP 슬라이더 프리팹")]
        [FormerlySerializedAs("playerHpSliderPrefab")] 
        [SerializeField] private Slider m_playerHpSliderPrefab;
        
        [Tooltip("이동 제한 맵")]
        [FormerlySerializedAs("mapRange")]
        [SerializeField] private SpriteRenderer m_mapRange;

        [Header("카메라 설정")]
        [Tooltip("카메라 추적 부드러움 정도")]
        [FormerlySerializedAs("moveDuration")]
        [SerializeField] private float m_cameraSmoothTime = 0.1f;

        [Header("자동 공격 설정")] 
        [Tooltip("적 탐지 레이어")]
        [FormerlySerializedAs("enemyLayer")]
        [SerializeField] private LayerMask m_enemyLayer;
        
        [Tooltip("적 탐지 반경")]
        [FormerlySerializedAs("detectionRadius")]
        [SerializeField] private float m_detectionRadius = 10f;
        
        [Tooltip("공격 사거리 (이 거리 안에서 멈춤)")]
        [FormerlySerializedAs("attackRadius")]
        [SerializeField] private float m_attackRadius = 1.5f;

        #endregion

        #region 내부 상태 변수

        // 상수
        private const float k_JoystickInputThreshold = 0.1f;
        private const int k_MaxEnemyColliders = 20; 

        // 외부 참조
        private GameManager m_gameManager;
        private Animator m_playerAnimator;
        private Camera m_mainCamera;
        private VariableJoystick m_joystick;

        // UI 및 상태
        private Slider m_playerHpSlider;
        private float m_previousHealth; // 피격 감지용 이전 체력

        // 상태 플래그
        private bool m_isGameStarted;
        private bool m_isAutoAttackActive;
        private bool m_autoAttackEnabledByToggle;

        // 자동 공격 관련
        private CancellationTokenSource m_autoMoveAttackCts;
        private Vector3 m_autoMoveDirection;
        private GameObject m_currentTarget;
        
        // 물리 및 카메라
        private Vector3 m_cameraVelocity = Vector3.zero;
        private ContactFilter2D m_contactFilter;
        private readonly Collider2D[] m_enemyColliders = new Collider2D[k_MaxEnemyColliders]; 

        // 애니메이션 파라미터 해시 (최적화)
        private static readonly int k_AnimWalk = Animator.StringToHash("Walk"); // Float (0: Idle, >0: Move)
        private static readonly int k_AnimHit = Animator.StringToHash("Hit");   // Trigger
        private static readonly int k_AnimDie = Animator.StringToHash("Die");   // Trigger

        // 프로퍼티
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
            // ContactFilter 초기화
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

        private void OnDrawGizmosSelected()
        {
            if (m_playerObject == null) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(m_playerObject.transform.position, m_detectionRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(m_playerObject.transform.position, m_attackRadius);
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
            
            // 체력 초기화
            m_previousHealth = m_playerCharacter.CurrentHealth;
            
            LogManager.Log($"[플레이어 컨트롤러] 캐릭터 할당됨: {character.name}", LogManager.LogCategory.PlayerBase);
            
            CreateHpSlider();
            m_playerCharacter.OnHealthChanged += OnPlayerHealthChanged;
            
            // 캐릭터 할당 직후 카메라 위치 즉시 설정
            ResetCameraPosition();
        }

        private void CreateHpSlider()
        {
            if (m_playerHpSliderPrefab == null) return;

            m_playerHpSlider = Instantiate(m_playerHpSliderPrefab, m_playerObject.transform);
            m_playerHpSlider.transform.localPosition = new Vector3(0, -0.4f, 0); 
            UpdateHpSliderUI(m_playerCharacter.CurrentHealth, m_playerCharacter.MaxHealth);
        }

        /// <summary>
        /// 플레이어 체력 변경 시 호출 (피격 애니메이션 처리 포함)
        /// </summary>
        private void OnPlayerHealthChanged(float current, float max)
        {
            UpdateHpSliderUI(current, max);

            // [피격 애니메이션 처리]
            // 체력이 감소했고, 죽은 상태가 아닐 때만 피격 애니메이션 재생
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

            // [죽음 애니메이션 처리]
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

            if (isJoystickActive)
            {
                if (m_isAutoAttackActive) DisableAutoMoveAttack();
                MoveDirection = joystickDir;
                TryAttack(MoveDirection);
            }
            else
            {
                if (m_autoAttackEnabledByToggle && !m_isAutoAttackActive)
                {
                    EnableAutoMoveAttack();
                }
                MoveDirection = m_isAutoAttackActive ? m_autoMoveDirection : Vector3.zero;
            }

            UpdateAnimationState(MoveDirection.magnitude);
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

        /// <summary>
        /// 이동 속도에 따라 애니메이션 상태(Idle/Move)를 갱신합니다.
        /// </summary>
        private void UpdateAnimationState(float speed)
        {
            if (m_playerAnimator != null)
            {
                // Walk 파라미터가 0이면 Idle, 0보다 크면 Move로 전환되도록 Animator 설정 필요
                m_playerAnimator.SetFloat(k_AnimWalk, speed);
            }
        }

        #endregion

        #region 공격 시스템

        /// <summary>
        /// 보유한 모든 무기의 공격을 시도합니다.
        /// 각 무기는 자체 쿨타임에 따라 공격 실행 여부를 결정합니다.
        /// </summary>
        /// <param name="dir">공격 방향</param>
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

        #region 자동 공격 시스템 (Auto Play)

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

                VamserMobBase target = FindClosestEnemy();
                
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

        private VamserMobBase FindClosestEnemy()
        {
            if (m_playerObject == null) return null;

            int count = Physics2D.OverlapCircle(m_playerObject.transform.position, m_detectionRadius, m_contactFilter, m_enemyColliders);
            
            VamserMobBase closest = null;
            float minDstSqr = float.MaxValue;
            Vector3 myPos = m_playerObject.transform.position;

            for (int i = 0; i < count; i++)
            {
                var col = m_enemyColliders[i];
                if (col.TryGetComponent(out VamserMobBase mob) && !mob.IsDead)
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

        /// <summary>
        /// 카메라 위치를 플레이어 위치로 즉시 설정합니다. (부드러운 이동 없음)
        /// </summary>
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