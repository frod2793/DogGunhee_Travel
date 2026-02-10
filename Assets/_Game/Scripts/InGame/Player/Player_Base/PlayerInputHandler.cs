using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 이동 입력을 처리하고 정규화된 방향 벡터를 제공하는 순수 로직(POCO) 클래스입니다.
    /// <br/> 조이스틱 입력을 기본으로 하며, 에디터 환경에서는 키보드 입력도 지원합니다.
    /// </summary>
    public class PlayerInputHandler
    {
        #region 1. 내부 변수 및 캐시

        private readonly VariableJoystick m_joystick;

        #endregion

        #region 2. 공개 프로퍼티

        /// <summary>
        /// 현재 프레임의 정규화된 이동 방향 벡터입니다. (크기는 0~1 사이로 제한됨)
        /// </summary>
        public Vector2 MoveDirection { get; private set; }

        /// <summary>
        /// 현재 유효한 이동 입력이 있는지 여부입니다. (Deadzone 처리 포함)
        /// </summary>
        public bool IsMoving => MoveDirection.sqrMagnitude > 0.01f;

        #endregion

        #region 3. 생성자

        /// <summary>
        /// 입력 핸들러를 초기화합니다.
        /// </summary>
        /// <param name="joystick">UI 조이스틱 참조</param>
        public PlayerInputHandler(VariableJoystick joystick)
        {
            m_joystick = joystick;
        }

        #endregion

        #region 4. 입력 처리 로직

        /// <summary>
        /// 매 프레임(Update) 호출되어 입력을 갱신합니다.
        /// </summary>
        public void HandleInput()
        {
            Vector2 input = Vector2.zero;

            // 1. 조이스틱 입력 처리
            if (m_joystick != null)
            {
                input = new Vector2(m_joystick.Horizontal, m_joystick.Vertical);
            }

            // 2. 에디터 키보드 입력 지원 (디버깅 편의성)
#if UNITY_EDITOR
            if (input == Vector2.zero)
            {
                input.x = Input.GetAxisRaw("Horizontal");
                input.y = Input.GetAxisRaw("Vertical");
            }
#endif

            // 3. 입력 벡터 정규화
            // 대각선 이동 시 속도가 1을 초과하지 않도록 제한하되,
            // 스틱을 살짝 기울였을 때의 미세한 속도(0~1)는 유지합니다.
            MoveDirection = Vector2.ClampMagnitude(input, 1f);
        }
        

        #endregion
    }
}