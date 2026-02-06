using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어를 추적하는 카메라 로직을 담당하는 POCO 클래스입니다.
    /// </summary>
    public class PlayerCameraController
    {
        #region 설정 변수
        private readonly float m_smoothTime;
        #endregion

        #region 내부 변수
        private readonly Camera m_mainCamera;
        private readonly Transform m_target;
        private readonly SpriteRenderer m_mapRange;
        private Vector3 m_velocity = Vector3.zero;
        #endregion

        #region 생성자
        public PlayerCameraController(Camera mainCamera, Transform target, SpriteRenderer mapRange, float smoothTime = 0.1f)
        {
            m_mainCamera = mainCamera;
            m_target = target;
            m_mapRange = mapRange;
            m_smoothTime = smoothTime;
        }
        #endregion

        #region 카메라 로직
        /// <summary>
        /// 매 프레임 후반에 호출되어 카메라를 이동시킵니다. (LateUpdate()에서 호출)
        /// </summary>
        public void OnLateUpdate()
        {
            FollowTarget();
        }

        public void ResetPosition()
        {
            if (m_mainCamera == null || m_target == null) return;
            m_mainCamera.transform.position = CalculateTargetPosition();
            m_velocity = Vector3.zero; // 위치 리셋 시 속도도 초기화
        }

        private void FollowTarget()
        {
            if (m_mainCamera == null || m_target == null) return;

            Vector3 targetPosition = CalculateTargetPosition();
            m_mainCamera.transform.position = Vector3.SmoothDamp(
                m_mainCamera.transform.position, 
                targetPosition, 
                ref m_velocity, 
                m_smoothTime
            );
        }

        private Vector3 CalculateTargetPosition()
        {
            Vector3 targetPos = m_target.position;
            targetPos.z = m_mainCamera.transform.position.z;

            if (m_mapRange != null)
            {
                Bounds bounds = m_mapRange.bounds;
                float camHeight = m_mainCamera.orthographicSize;
                float camWidth = camHeight * m_mainCamera.aspect;

                targetPos.x = Mathf.Clamp(targetPos.x, bounds.min.x + camWidth, bounds.max.x - camWidth);
                targetPos.y = Mathf.Clamp(targetPos.y, bounds.min.y + camHeight, bounds.max.y - camHeight);
            }

            return targetPos;
        }
        #endregion
    }
}
