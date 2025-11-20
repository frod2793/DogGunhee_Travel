using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DogGuns_Games.vamsir
{
    /// <summary>
    /// 플레이어의 이동, 공격, 카메라 추적 등을 제어하는 메인 컨트롤러입니다.
    /// 조이스틱 입력 또는 자동 공격 모드를 지원합니다.
    /// </summary>
    public class VamPlayerControll : MonoBehaviour
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
        [SerializeField] private float m_detectionRadius = 10f; // 30f는 너무 클 수 있어 조정 권장
        
        [Tooltip("공격 사거리 (이 거리 안에서 멈춤)")]
        [FormerlySerializedAs("attackRadius")]
        [SerializeField] private float m_attackRadius = 1.5f;

        #endregion

        #region 내부 상태 변수

        // 상수
        private const float k_JoystickInputThreshold = 0.1f;
        private const int k_MaxEnemyColliders = 20; // 탐지할 최대 적 수

        // 외부 참조
        private VamserLikeGameManager m_gameManager;
        private Animator m_playerAnimator;
        private Camera m_mainCamera;
        private VariableJoystick m_joystick;

        // UI
        private Slider m_playerHpSlider;

        // 상태 플래그
        private bool m_isGameStarted;
        private bool m_isAttacking;
        private bool m_isAutoAttackActive;
        private bool m_autoAttackEnabledByToggle;

        // 자동 공격 관련
        private CancellationTokenSource m_autoMoveAttackCts;
        private Vector3 m_autoMoveDirection;
        private GameObject m_currentTarget;
        
        // 물리 및 카메라
        private Vector3 m_cameraVelocity = Vector3.zero;
        private ContactFilter2D m_contactFilter;
        private readonly Collider2D[] m_enemyColliders = new Collider2D[k_MaxEnemyColliders]; // 캐싱된 배열

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
            m_gameManager = VamserLikeGameManager.Instance;
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
                m_playerCharacter.OnHealthChanged -= UpdatePlayerHpSlider;
            }
        }

        public void AssignCharacter(PlayerBase character)
        {
            if (character == null) return;

            m_playerCharacter = character;
            m_playerCharacter.transform.SetParent(m_playerObject.transform, false);
            m_playerAnimator = m_playerCharacter.GetComponent<Animator>();
            m_mainCamera = VamserLikeGameManager.Instance.MainCamera;
            
            LogManager.Log($"[Controller] Character Assigned: {character.name}", LogManager.LogCategory.PlayerBase);
            
            CreateHpSlider();
            m_playerCharacter.OnHealthChanged += UpdatePlayerHpSlider;
        }

        private void CreateHpSlider()
        {
            if (m_playerHpSliderPrefab == null) return;

            m_playerHpSlider = Instantiate(m_playerHpSliderPrefab, m_playerObject.transform);
            m_playerHpSlider.transform.localPosition = new Vector3(0, -0.8f, 0); // 위치 조정
            UpdatePlayerHpSlider(m_playerCharacter.CurrentHealth, m_playerCharacter.MaxHealth);
        }

        private void UpdatePlayerHpSlider(float current, float max)
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
        }

        #endregion

        #region 이동 및 조작 로직

        private void HandleMovementInput()
        {
            // 조이스틱 입력 확인
            Vector3 joystickDir = GetJoystickInputDirection();
            bool isJoystickActive = joystickDir.sqrMagnitude > k_JoystickInputThreshold * k_JoystickInputThreshold;

            if (isJoystickActive)
            {
                // 수동 조작 시 자동 공격 해제
                if (m_isAutoAttackActive) DisableAutoMoveAttack();
                
                MoveDirection = joystickDir;
                TryAttack(MoveDirection);
            }
            else
            {
                // 입력 없으면 자동 공격 체크
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

            // 맵 범위 제한 적용 후 이동
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
                // 단순히 x축 방향에 따라 뒤집기 (Flip)
                float yRot = dir.x < 0 ? 0f : 180f; // 원본 스프라이트 방향에 따라 0/180 조정 필요
                m_playerCharacter.transform.rotation = Quaternion.Euler(0, yRot, 0);
            }
        }

        private void UpdateAnimationState(float speed)
        {
            if (m_playerAnimator != null)
            {
                m_playerAnimator.SetFloat("Walk", speed);
            }
        }

        #endregion

        #region 공격 시스템

        private void TryAttack(Vector3 dir)
        {
            if (!m_isAttacking)
            {
                PerformAttackAsync(dir).Forget();
            }
        }

        private async UniTask PerformAttackAsync(Vector3 dir)
        {
            if (m_playerCharacter == null || m_playerCharacter.WeaphonBase == null) return;

            m_isAttacking = true;
            
            m_playerCharacter.WeaphonBase.Weaphon_Attack(dir);

            // 쿨타임 대기
            float coolTime = m_playerCharacter.WeaphonBase.coolTime;
            if (coolTime > 0)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(coolTime), cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            
            m_isAttacking = false;
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

                    // 1. 이동 결정 (사거리 밖이면 이동)
                    // 공격 사거리보다 약간 여유를 두고 멈춤
                    if (dist > m_attackRadius * 0.9f)
                    {
                        m_autoMoveDirection = dirToTarget;
                    }
                    else
                    {
                        m_autoMoveDirection = Vector3.zero;
                    }

                    // 2. 공격 시도 (사거리 내)
                    if (dist <= m_attackRadius * 1.2f && !m_isAttacking)
                    {
                        await PerformAttackAsync(dirToTarget);
                    }
                }
                else
                {
                    // 타겟 없으면 정지
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

            // 배열 초기화 불필요 (덮어쓰기 방식)
            return closest;
        }
        #endregion

        #region 카메라 추적

        private void FollowCamera()
        {
            if (m_mainCamera == null || m_playerObject == null || m_mapRange == null) return;

            Vector3 targetPos = m_playerObject.transform.position;
            targetPos.z = m_mainCamera.transform.position.z;

            // 맵 범위 제한
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