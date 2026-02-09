using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어를 부드럽게 추적하고 맵 경계 내에서 카메라 가동 범위를 제한하는 POCO 클래스입니다.
    /// </summary>
    public class PlayerCameraController
    {
        #region 설정 데이터

        private readonly float m_smoothTime;

        #endregion

        #region 내부 상태 및 캐시

        private readonly Camera m_mainCamera;
        private readonly Transform m_target;
        private readonly SpriteRenderer m_mapRange;
        private Vector3 m_velocity = Vector3.zero;

        #endregion

        #region 초기화 및 제어

        public PlayerCameraController(Camera mainCamera, Transform target, SpriteRenderer mapRange, float smoothTime = 0.1f)
        {
            m_mainCamera = mainCamera;
            m_target = target;
            m_mapRange = mapRange;
            m_smoothTime = smoothTime;
        }

        /// <summary>
        /// 카메라의 위치를 타겟 위치로 즉시 리셋합니다.
        /// </summary>
        public void ResetPosition()
        {
            if (m_mainCamera == null || m_target == null) return;
            m_mainCamera.transform.position = CalculateTargetPosition();
            m_velocity = Vector3.zero;
        }

        /// <summary>
        /// 매 프레임 후반부(LateUpdate)에 호출되어 카메라 이동을 수행합니다.
        /// </summary>
        public void OnLateUpdate()
        {
            FollowTarget();
        }

        #endregion

        #region 카메라 로직

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

        /// <summary>
        /// 타겟의 위치를 기반으로 맵 경계가 적용된 최종 카메라 좌표를 계산합니다.
        /// </summary>
        private Vector3 CalculateTargetPosition()
        {
            Vector3 targetPos = m_target.position;
            targetPos.z = m_mainCamera.transform.position.z;

            // 맵 경계가 설정된 경우 카메라 크기를 고려하여 클램핑
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
