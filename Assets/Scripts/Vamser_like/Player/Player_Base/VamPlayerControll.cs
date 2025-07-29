using Cysharp.Threading.Tasks;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

namespace DogGuns_Games.vamsir
{
    public class VamPlayerControll : MonoBehaviour
    {
        
        //todo: 플레이어의 HP가 O 이하로 떨어지면 게임 오버 처리
        
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

        #endregion

        #region Unity 라이프사이클

        private void Start()
        {
            PlayStateManager.OnGameStart += PlayerInit;
            PlayStateManager.OnGamePause += Pause;
            PlayStateManager.OnGameResume += Resume;
        }

        private void OnDestroy()
        {
            PlayStateManager.OnGameStart -= PlayerInit;
            PlayStateManager.OnGamePause -= Pause;
            PlayStateManager.OnGameResume -= Resume;
        }

        private void FixedUpdate()
        {
            if (_isGameStart)
            {
                PlayerMovement();
                FallowCamera();
                UpdatePlayerHpSlider();
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
            playerCharactor.PlayState = PlayerBase.playerState.Attack;

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
    }
}