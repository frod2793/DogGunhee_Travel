using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어 입력을 처리하는 전용 POCO 클래스입니다.
    /// 조이스틱 및 키보드 입력을 추상화하여 제공합니다.
    /// </summary>
    public class PlayerInputHandler
    {
        #region 내부 변수
        private readonly VariableJoystick m_joystick;
        #endregion

        #region 프로퍼티
        public Vector2 MoveDirection { get; private set; }
        public bool IsMoving => MoveDirection.sqrMagnitude > 0.01f;
        #endregion

        #region 생성자
        public PlayerInputHandler(VariableJoystick joystick)
        {
            m_joystick = joystick;
        }
        #endregion

        #region 입력 로직
        /// <summary>
        /// 매 프레임 호출되어 입력을 처리합니다. (Update()에서 호출)
        /// </summary>
        public void HandleInput()
        {
            if (m_joystick == null)
            {
                MoveDirection = Vector2.zero;
                return;
            }

            MoveDirection = new Vector2(m_joystick.Horizontal, m_joystick.Vertical);
            
            // 정규화 (대각선 속도 보정)
            if (MoveDirection.sqrMagnitude > 1f)
            {
                MoveDirection.Normalize();
            }
        }
        #endregion
    }
}
