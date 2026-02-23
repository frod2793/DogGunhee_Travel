using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// [설명]: 플레이어를 부드럽게 추적하고 맵 경계 내에서 카메라의 이동 범위를 제한하는 로직 클래스입니다.
    /// MonoBehaviour가 아닌 일반 C# 클래스로 설계되어 생명주기 관리는 소유자(PlayerCameraAgent 등)가 담당합니다.
    /// </summary>
    public class PlayerCameraController
    {
        #region 내부 설정 데이터

        /// <summary> 카메라 이동의 부드러움 계수 (SmoothDamp용 시간값) </summary>
        private readonly float m_smoothTime;

        #endregion

        #region 내부 상태 및 캐시

        /// <summary> 제어 대상 메인 카메라 인스턴스 </summary>
        private readonly Camera m_mainCamera;

        /// <summary> 카메라가 추적할 타겟의 트랜스폼 </summary>
        private Transform m_targetTransform;

        /// <summary> 카메라 이동 가능 범위를 제한하는 맵 경계 렌더러 </summary>
        private readonly SpriteRenderer m_mapBoundary;

        /// <summary> SmoothDamp 연산에 사용되는 내부 참조 속도 </summary>
        private Vector3 m_currentVelocity = Vector3.zero;

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 카메라 컨트롤러를 생성하고 필수 파라미터를 할당합니다.
        /// </summary>
        /// <param name="mainCamera">제어할 카메라 객체</param>
        /// <param name="target">추적할 타겟 트랜스폼</param>
        /// <param name="mapRange">맵 경계를 나타내는 SpriteRenderer (없으면 null)</param>
        /// <param name="smoothTime">스무싱 적용 시간 (기본값 0.1초)</param>
        public PlayerCameraController(Camera mainCamera, Transform target, SpriteRenderer mapRange, float smoothTime = 0.1f)
        {
            m_mainCamera = mainCamera;
            m_targetTransform = target;
            m_mapBoundary = mapRange;
            m_smoothTime = smoothTime;
        }

        #endregion

        #region 공개 인터페이스

        /// <summary>
        /// [설명]: 카메라의 위치를 계산된 타겟 위치로 즉시 이동(순간이동)시킵니다.
        /// </summary>
        public void ResetPosition()
        {
            if (m_mainCamera == null || m_targetTransform == null)
            {
                return;
            }

            m_currentVelocity = Vector3.zero;
            m_mainCamera.transform.position = CalculateClampedTargetPosition();
        }

        /// <summary>
        /// [설명]: 런타임 중에 카메라가 추적할 대상을 변경하며, 필요 시 즉시 위치시킵니다.
        /// </summary>
        /// <param name="newTarget">새로운 추적 타겟</param>
        /// <param name="snapToTarget">즉시 이동 여부</param>
        public void SetTarget(Transform newTarget, bool snapToTarget = false)
        {
            m_targetTransform = newTarget;

            if (snapToTarget)
            {
                ResetPosition();
            }
        }

        /// <summary>
        /// [설명]: 매 프레임 후반부(LateUpdate)에서 호출되어 카메라를 부드럽게 이동시킵니다.
        /// </summary>
        public void OnLateUpdate()
        {
            if (m_mainCamera == null || m_targetTransform == null)
            {
                return;
            }

            FollowTargetSmoothly();
        }

        #endregion

        #region 내부 비즈니스 로직

        /// <summary>
        /// [설명]: 타겟의 목표 위치를 향해 SmoothDamp 알고리즘을 사용하여 부드럽게 가속 및 감속 이동을 수행합니다.
        /// </summary>
        private void FollowTargetSmoothly()
        {
            Vector3 targetPos = CalculateClampedTargetPosition();

            m_mainCamera.transform.position = Vector3.SmoothDamp(
                m_mainCamera.transform.position,
                targetPos,
                ref m_currentVelocity,
                m_smoothTime
            );
        }

        /// <summary>
        /// [설명]: 타겟의 현재 좌표를 기반으로 맵 경계(Boundary)와 카메라 뷰 크기를 고려하여 유효한 최종 좌표를 산출합니다.
        /// </summary>
        /// <returns>클램핑 처리가 완료된 카메라 목표 월드 좌표</returns>
        private Vector3 CalculateClampedTargetPosition()
        {
            Vector3 targetPos = m_targetTransform.position;

            // Z축은 카메라 기본 설정값 유지
            targetPos.z = m_mainCamera.transform.position.z;

            // 맵 경계가 설정된 경우 화면 밖으로 나가지 않도록 제한
            if (m_mapBoundary != null)
            {
                Bounds mapBounds = m_mapBoundary.bounds;

                float camHalfHeight = m_mainCamera.orthographicSize;
                float camHalfWidth = camHalfHeight * m_mainCamera.aspect;

                float minX = mapBounds.min.x + camHalfWidth;
                float maxX = mapBounds.max.x - camHalfWidth;
                float minY = mapBounds.min.y + camHalfHeight;
                float maxY = mapBounds.max.y - camHalfHeight;

                // 맵 영역이 카메라 뷰보다 작을 경우 중앙에 고정
                if (minX > maxX)
                {
                    targetPos.x = mapBounds.center.x;
                }
                else
                {
                    targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
                }

                if (minY > maxY)
                {
                    targetPos.y = mapBounds.center.y;
                }
                else
                {
                    targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);
                }
            }

            return targetPos;
        }

        #endregion
    }
}