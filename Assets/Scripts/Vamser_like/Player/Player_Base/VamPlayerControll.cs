using Cysharp.Threading.Tasks;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

namespace DogGuns_Games.vamsir
{
    public class VamPlayerControll : MonoBehaviour
    {
        #region 필드 및 변수

        [Header("<color=green>Player Object")] [SerializeField]
        private Player_Base player;

        [Header("<color=green>Player HP_IU")] [SerializeField]
        private Slider playerHpSliderPrefab;

        private Animator _playerAnimator;

        [Header("<color=green>Joystick")] [SerializeField]
        private VariableJoystick variableJoystick;

        [Header("<color=green>camera")] [SerializeField]
        private Camera cameraTransform;

        [Header("<color=green>Move Duration")] [SerializeField]
        private float moveDuration = 0.1f;
        
        
        [Header("<color=green>Map Range")] 
        [SerializeField] private SpriteRenderer mapRange;


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
            }
        }

        #endregion

        #region 게임 상태 관리

        private void PlayerInit()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<Player_Base>();
                _playerAnimator = player.GetComponent<Animator>();
                cameraTransform = Camera.main;
                Set_playerHpSlider();
            }
            _isGameStart = true;
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
            Slider playerHpSlider = Instantiate(playerHpSliderPrefab, player.transform);
            playerHpSlider.transform.localPosition = new Vector3(0, -0.4f, 0);
            playerHpSlider.maxValue = player.Health;
            playerHpSlider.value = player.Health;
        }

        #endregion

        #region 플레이어 제어

        private void PlayerMovement()
        {
            if (player == null)
            {
                PlayerInit();
            }

            Vector3 moveVector = (Vector3.right * variableJoystick.Horizontal + Vector3.up * variableJoystick.Vertical);
            float deltaSpeed = player.MoveSpeed * Time.deltaTime;
            Vector3 targetPosition = player.transform.position + moveVector * deltaSpeed;

            // 맵 범위의 경계를 가져옴
            Bounds mapBounds = mapRange.bounds;

            // 맵 범위 내에서 목표 위치를 클램프
            targetPosition.x = Mathf.Clamp(targetPosition.x, mapBounds.min.x, mapBounds.max.x);
            targetPosition.y = Mathf.Clamp(targetPosition.y, mapBounds.min.y, mapBounds.max.y);

            player.transform.DOMove(targetPosition, moveDuration);
            _playerAnimator.SetFloat("Walk", moveVector.magnitude);

            // 조이스틱 컨트롤이 공격을 트리거
            if (moveVector.magnitude > 0 && !_isAttack)
            {
                PlayerAttack(moveVector).Forget();
            }

            if (moveVector != Vector3.zero)
            {
                float angle = Mathf.Atan2(moveVector.y, moveVector.x) * Mathf.Rad2Deg;
                float yRotation = (angle > 90 || angle < -90) ? 0f : 180f;
                player.transform.rotation = Quaternion.Euler(0, yRotation, 0);
            }
        }

        /// <summary>
        /// 플레이어 공격 호출 
        /// </summary>
        /// <param name="attackAngle">공격 방향</param>
        private async UniTask PlayerAttack(Vector3 attackAngle)
        {
            _isAttack = true;
            player.AttackAngle = attackAngle;
            player.PlayState = Player_Base.playerState.Attack;
            
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
            // 카메라가 맵 경계를 벗어나지 않도록 설정
            Vector3 cameraPosition = new Vector3(player.transform.position.x, player.transform.position.y, cameraTransform.transform.position.z);

            // 맵 범위의 경계를 가져옴
            Bounds mapBounds = mapRange.bounds;

            // 카메라의 절반 크기를 계산
            float cameraHalfWidth = cameraTransform.orthographicSize * cameraTransform.aspect;
            float cameraHalfHeight = cameraTransform.orthographicSize;

            // 맵 범위 내에서 카메라 위치를 클램프
            cameraPosition.x = Mathf.Clamp(cameraPosition.x, mapBounds.min.x + cameraHalfWidth, mapBounds.max.x - cameraHalfWidth);
            cameraPosition.y = Mathf.Clamp(cameraPosition.y, mapBounds.min.y + cameraHalfHeight, mapBounds.max.y - cameraHalfHeight);

            cameraTransform.transform.DOMove(cameraPosition, moveDuration);
        }

        #endregion
    }
}