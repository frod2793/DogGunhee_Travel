using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어를 부드럽게 추적하고 맵 경계 내에서 카메라의 이동 범위를 제한하는 로직 클래스입니다.
    /// <br/> MonoBehaviour가 아닌 일반 클래스로 구성되어 있으며, PlayerController의 LateUpdate에서 호출되어야 합니다.
    /// </summary>
    public class PlayerCameraController
    {
        #region 1. 설정 데이터 (Settings)

        // 카메라 이동의 부드러움 정도 (작을수록 빠름)
        private readonly float m_smoothTime;

        #endregion

        #region 2. 내부 상태 및 캐시 (State & Cache)

        // 참조 컴포넌트
        private readonly Camera m_mainCamera;
        private Transform m_targetTransform;
        private readonly SpriteRenderer m_mapBoundary;

        // SmoothDamp용 참조 속도 변수
        private Vector3 m_currentVelocity = Vector3.zero;

        #endregion

        #region 3. 생성자 (Constructor)

        /// <summary>
        /// 카메라 컨트롤러를 초기화합니다.
        /// </summary>
        /// <param name="mainCamera">제어할 메인 카메라</param>
        /// <param name="target">추적할 타겟(플레이어)</param>
        /// <param name="mapRange">맵 경계 스프라이트 (없으면 null)</param>
        /// <param name="smoothTime">스무싱 시간 (기본값 0.1)</param>
        public PlayerCameraController(Camera mainCamera, Transform target, SpriteRenderer mapRange, float smoothTime = 0.1f)
        {
            m_mainCamera = mainCamera;
            m_targetTransform = target;
            m_mapBoundary = mapRange;
            m_smoothTime = smoothTime;
        }

        #endregion

        #region 4. 공개 메서드 (Public Methods)

        /// <summary>
        /// 카메라의 위치를 타겟 위치로 즉시 이동시킵니다. (텔레포트, 초기화 등)
        /// </summary>
        public void ResetPosition()
        {
            if (m_mainCamera == null || m_targetTransform == null) return;

            // 즉시 이동 시에는 속도 초기화
            m_currentVelocity = Vector3.zero;
            m_mainCamera.transform.position = CalculateClampedTargetPosition();
        }

        /// <summary>
        /// 추적 대상을 런타임에 변경합니다.
        /// </summary>
        /// <param name="newTarget">새로운 타겟 Transform</param>
        /// <param name="snapToTarget">즉시 이동 여부 (true면 텔레포트)</param>
        public void SetTarget(Transform newTarget, bool snapToTarget = false)
        {
            m_targetTransform = newTarget;
            
            if (snapToTarget)
            {
                ResetPosition();
            }
            // else: 부드럽게 새 타겟으로 이동
        }

        /// <summary>
        /// 매 프레임 후반부(LateUpdate)에 호출되어 카메라 이동 로직을 수행합니다.
        /// <br/> 플레이어의 이동(Update)이 끝난 후 카메라가 따라가야 지터링(떨림)이 없습니다.
        /// </summary>
        public void OnLateUpdate()
        {
            if (m_mainCamera == null || m_targetTransform == null) return;

            FollowTargetSmoothly();
        }

        #endregion

        #region 5. 내부 로직 (Internal Logic)

        /// <summary>
        /// 타겟을 부드럽게 따라갑니다.
        /// </summary>
        private void FollowTargetSmoothly()
        {
            // 목표 위치 계산 (맵 경계 포함)
            Vector3 targetPos = CalculateClampedTargetPosition();

            // SmoothDamp를 이용한 부드러운 이동
            // transform.position을 직접 수정합니다.
            m_mainCamera.transform.position = Vector3.SmoothDamp(
                m_mainCamera.transform.position,
                targetPos,
                ref m_currentVelocity,
                m_smoothTime
            );
        }

        /// <summary>
        /// 타겟의 현재 위치를 기반으로 맵 경계(Bounds)를 적용한 최종 카메라 좌표를 계산합니다.
        /// </summary>
        private Vector3 CalculateClampedTargetPosition()
        {
            // 1. 타겟의 기본 위치 가져오기
            Vector3 targetPos = m_targetTransform.position;
            
            // 2. 카메라는 2D 게임에서 Z축을 유지해야 함
            targetPos.z = m_mainCamera.transform.position.z;

            // 3. 맵 경계 클램핑 (Clamp)
            if (m_mapBoundary != null)
            {
                Bounds mapBounds = m_mapBoundary.bounds;
                
                // 카메라의 절반 크기(World Space 기준) 계산
                float camHalfHeight = m_mainCamera.orthographicSize;
                float camHalfWidth = camHalfHeight * m_mainCamera.aspect;

                // 맵 밖으로 카메라가 나가지 않도록 좌표 제한
                // Min = 맵 왼쪽/아래 끝 + 카메라 반폭/반높이
                // Max = 맵 오른쪽/위 끝 - 카메라 반폭/반높이
                float minX = mapBounds.min.x + camHalfWidth;
                float maxX = mapBounds.max.x - camHalfWidth;
                float minY = mapBounds.min.y + camHalfHeight;
                float maxY = mapBounds.max.y - camHalfHeight;

                // 맵이 카메라보다 작을 경우를 대비한 방어 코드 (Max가 Min보다 작아질 수 있음)
                if (minX > maxX) targetPos.x = mapBounds.center.x; 
                else targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);

                if (minY > maxY) targetPos.y = mapBounds.center.y;
                else targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);
            }

            return targetPos;
        }

        #endregion
    }
}