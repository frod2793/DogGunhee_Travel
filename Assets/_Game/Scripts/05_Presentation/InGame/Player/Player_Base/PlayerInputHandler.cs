using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// [설명]: 플레이어의 이동 입력을 처리하고 정규화된 방향 벡터를 제공하는 순수 로직(POCO) 클래스입니다.
    /// 조이스틱 입력을 기본으로 하며, 에디터 환경에서는 키보드 입력을 병행하여 지원합니다.
    /// </summary>
    public class PlayerInputHandler
    {
        #region 내부 필드

        /// <summary> 사용자의 터치 입력을 받는 UI 조이스틱 객체 </summary>
        private readonly VariableJoystick m_joystick;

        #endregion

        #region 공개 프로퍼티

        /// <summary>
        /// [설명]: 현재 프레임의 정규화된 이동 방향 벡터입니다. 크기는 0에서 1 사이로 제한됩니다.
        /// </summary>
        public Vector2 MoveDirection { get; private set; }

        /// <summary>
        /// [설명]: 현재 유효한 이동 입력이 활성화되어 있는지 여부입니다. (데드존 판정 포함)
        /// </summary>
        public bool IsMoving => MoveDirection.sqrMagnitude > 0.01f;

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 입력 핸들러를 초기화하고 외부 조이스틱 참조를 캐싱합니다.
        /// </summary>
        /// <param name="joystick">UI에 배치된 VariableJoystick 참조</param>
        public PlayerInputHandler(VariableJoystick joystick)
        {
            m_joystick = joystick;
        }

        #endregion

        #region 입력 처리 로직

        /// <summary>
        /// [설명]: 매 프레임 업데이트에서 호출되어 조이스틱 또는 키보드 입력을 갱신합니다.
        /// </summary>
        public void HandleInput()
        {
            Vector2 input = Vector2.zero;

            // 조이스틱 입력 우선 처리
            if (m_joystick != null)
            {
                input = new Vector2(m_joystick.Horizontal, m_joystick.Vertical);
            }

            // 에디터 환경에서 키보드 입력 지원 (디버깅 편의성)
#if UNITY_EDITOR
            if (input == Vector2.zero)
            {
                input.x = Input.GetAxisRaw("Horizontal");
                input.y = Input.GetAxisRaw("Vertical");
            }
#endif

            // 입력 벡터의 크기를 1로 제한하여 정규화 수행
            MoveDirection = Vector2.ClampMagnitude(input, 1f);
        }

        #endregion
    }
}