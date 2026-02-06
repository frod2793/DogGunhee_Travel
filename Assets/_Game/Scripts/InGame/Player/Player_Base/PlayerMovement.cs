using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 물리적 이동과 회전을 담당하는 POCO 클래스입니다.
    /// MonoBehaviour를 상속받지 않으며, 로직만 수행합니다.
    /// </summary>
    public class PlayerMovement
    {
        #region 내부 변수
        private readonly PlayerBase m_playerBase;
        private readonly Transform m_moveTarget;
        private readonly Transform m_characterTransform;
        private readonly SpriteRenderer m_mapRange;
        #endregion

        #region 생성자
        public PlayerMovement(PlayerBase playerBase, Transform moveTarget, Transform characterTransform, SpriteRenderer mapRange)
        {
            m_playerBase = playerBase;
            m_moveTarget = moveTarget;
            m_characterTransform = characterTransform;
            m_mapRange = mapRange;
        }
        #endregion

        #region 이동 동작
        /// <summary>
        /// 입력된 방향으로 플레이어를 이동시킵니다.
        /// </summary>
        public void Move(Vector2 direction)
        {
            if (m_playerBase == null || m_moveTarget == null || direction == Vector2.zero) return;

            float speed = m_playerBase.MoveSpeed * Time.deltaTime;
            Vector3 targetPos = m_moveTarget.position + (Vector3)direction * speed;

            // 맵 경계 제한
            m_moveTarget.position = ClampPositionToMap(targetPos);
            
            // 캐릭터 회전 처리
            UpdateRotation(direction);
        }

        private Vector3 ClampPositionToMap(Vector3 position)
        {
            if (m_mapRange == null) return position;

            Bounds bounds = m_mapRange.bounds;
            float x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            float y = Mathf.Clamp(position.y, bounds.min.y, bounds.max.y);
            
            return new Vector3(x, y, 0f);
        }

        private void UpdateRotation(Vector2 direction)
        {
            if (m_characterTransform == null || direction.sqrMagnitude < 0.01f) return;

            // 왼쪽은 0도, 오른쪽은 180도
            float yRot = direction.x < 0 ? 0f : 180f;
            m_characterTransform.rotation = Quaternion.Euler(0, yRot, 0);
        }
        #endregion
    }
}
