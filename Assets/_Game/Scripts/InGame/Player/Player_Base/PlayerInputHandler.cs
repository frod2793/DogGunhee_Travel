using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어 입력을 처리하고 추상화된 이동 방향을 제공하는 POCO 클래스입니다.
    /// 조이스틱 입력을 기본으로 하며 대각선 이동 속도 보정을 수행합니다.
    /// </summary>
    public class PlayerInputHandler
    {
        #region 내부 상태 및 캐시

        private readonly VariableJoystick m_joystick;

        #endregion

        #region 프로퍼티

        /// <summary>
        /// 현재 입력된 이동 방향 벡터입니다.
        /// </summary>
        public Vector2 MoveDirection { get; private set; }

        /// <summary>
        /// 현재 유의미한 이동 입력이 있는지 여부입니다.
        /// </summary>
        public bool IsMoving => MoveDirection.sqrMagnitude > 0.01f;

        #endregion

        #region 초기화

        public PlayerInputHandler(VariableJoystick joystick)
        {
            m_joystick = joystick;
        }

        #endregion

        #region 제어 로직

        /// <summary>
        /// 매 프레임 호출되어 최신 입력을 읽어오고 정규화합니다.
        /// </summary>
        public void HandleInput()
        {
            if (m_joystick == null)
            {
                MoveDirection = Vector2.zero;
                return;
            }

            MoveDirection = new Vector2(m_joystick.Horizontal, m_joystick.Vertical);
            
            // 대각선 이동 시 속도가 빨라지는 것을 방지하기 위한 정규화
            if (MoveDirection.sqrMagnitude > 1f)
            {
                MoveDirection.Normalize();
            }
        }

        #endregion
    }
}
