using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 물리적 이동 처리 및 캐릭터 회전을 담당하는 POCO 클래스입니다.
    /// MonoBehaviour 의존성을 배제하고 순수 로직만 수행합니다.
    /// </summary>
    public class PlayerMovement
    {
        #region 내부 상태 및 캐시

        private readonly PlayerBase m_playerBase;
        private readonly Transform m_moveTarget;
        private readonly Transform m_characterTransform;
        private readonly SpriteRenderer m_mapRange;

        #endregion

        #region 초기화 및 제어

        public PlayerMovement(PlayerBase playerBase, Transform moveTarget, Transform characterTransform, SpriteRenderer mapRange)
        {
            m_playerBase = playerBase;
            m_moveTarget = moveTarget;
            m_characterTransform = characterTransform;
            m_mapRange = mapRange;
        }

        /// <summary>
        /// 입력된 방향으로 플레이어를 이동시키고 맵 경계 내로 제한합니다.
        /// </summary>
        /// <param name="direction">이동 벡터 (정규화된 방향)</param>
        public void Move(Vector2 direction)
        {
            if (m_playerBase == null || m_moveTarget == null || direction == Vector2.zero) return;

            float speed = m_playerBase.MoveSpeed * Time.deltaTime;
            Vector3 targetPos = m_moveTarget.position + (Vector3)direction * speed;

            // 맵 경계 제한 적용
            m_moveTarget.position = ClampPositionToMap(targetPos);
            
            // 캐릭터의 좌우 회전 업데이트
            UpdateRotation(direction);
        }

        #endregion

        #region 유틸리티 로직

        /// <summary>
        /// 지정된 위치가 맵 가동 범위를 벗어나지 않도록 클램핑합니다.
        /// </summary>
        private Vector3 ClampPositionToMap(Vector3 position)
        {
            if (m_mapRange == null) return position;

            Bounds bounds = m_mapRange.bounds;
            float x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            float y = Mathf.Clamp(position.y, bounds.min.y, bounds.max.y);
            
            return new Vector3(x, y, 0f);
        }

        /// <summary>
        /// 이동 방향에 따라 캐릭터의 스프라이트 방향(Y축 회전)을 업데이트합니다.
        /// </summary>
        private void UpdateRotation(Vector2 direction)
        {
            if (m_characterTransform == null || direction.sqrMagnitude < 0.01f) return;

            // 왼쪽 조작 시 0도, 오른쪽 조작 시 180도 (스프라이트 기본 에셋 기준)
            float yRot = direction.x < 0 ? 0f : 180f;
            m_characterTransform.rotation = Quaternion.Euler(0, yRot, 0);
        }

        #endregion
    }
}
