using InGame.Managers;
using InGame.Player.Player_Base;
using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// [설명]: 플레이어 추적 카메라를 제어하는 MonoBehaviour 컴포넌트입니다.
    /// PlayerController에서 카메라 제어 책임을 분리하여 관리하며, 실질적인 계산은 PlayerCameraController에 위임합니다.
    /// </summary>
    public class PlayerCameraAgent : MonoBehaviour
    {
        #region 에디터 설정

        [Header("카메라 설정")]
        [SerializeField, Tooltip("추적할 타겟 (일반적으로 플레이어)")]
        private Transform m_targetTransform;

        [SerializeField, Tooltip("맵 경계 스프라이트 (카메라의 이동 범위를 제한하는 용도)")]
        private SpriteRenderer m_mapBoundary;

        [SerializeField, Tooltip("카메라 이동의 부드러움 계수 (값이 작을수록 반응 속도가 빠름)")]
        private float m_smoothTime = 0.1f;

        #endregion

        #region 내부 필드

        /// <summary> 카메라 이동 연산을 담당하는 순수 로직 컨트롤러 </summary>
        private PlayerCameraController m_cameraController;
        private Camera m_mainCamera;

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 카메라 추적 시스템을 구성하고 내부 컨트롤러를 생성하여 초기화합니다.
        /// </summary>
        /// <param name="mainCamera">메인 카메라 인스턴스</param>
        /// <param name="target">추적 대상 트랜스폼 (null일 경우 인스펙터 설정값 활용)</param>
        /// <param name="mapBoundary">맵 경계 렌더러 (null일 경우 인스펙터 설정값 활용)</param>
        public void Initialize(Camera mainCamera, Transform target = null, SpriteRenderer mapBoundary = null)
        {
            if (mainCamera != null)
            {
                m_mainCamera = mainCamera;
            }

            // 카메라가 주입되지 않은 경우 Camera.main 폴백
            if (m_mainCamera == null)
            {
                m_mainCamera = Camera.main;
            }

            if (target != null)
            {
                m_targetTransform = target;
            }

            if (mapBoundary != null)
            {
                m_mapBoundary = mapBoundary;
            }

            if (m_mainCamera != null && m_targetTransform != null)
            {
                m_cameraController = new PlayerCameraController(
                    m_mainCamera,
                    m_targetTransform,
                    m_mapBoundary,
                    m_smoothTime
                );

                // 초기 위치로 즉시 순간 이동 시켜 떨림 방지
                m_cameraController.ResetPosition();
            }
            else
            {
                LogManager.Log("[PlayerCameraAgent] 초기화 대기: 카메라 또는 타겟 미확보 — 이후 SetTarget 시 재시도", LogManager.LogCategory.System);
            }
        }

        #endregion

        #region 유니티 생명주기

        /// <summary>
        /// [설명]: 타겟 이동 완료 후 카메라가 따라가도록 LateUpdate에서 로직을 처리합니다.
        /// </summary>
        private void LateUpdate()
        {
            m_cameraController?.OnLateUpdate();
        }

        #endregion

        #region 공개 인터페이스

        /// <summary>
        /// [설명]: 런타임 중에 카메라가 추적할 대상을 동적으로 변경합니다.
        /// </summary>
        /// <param name="newTarget">새로운 타겟 트랜스폼</param>
        public void SetTarget(Transform newTarget)
        {
            m_targetTransform = newTarget;

            if (m_cameraController != null)
            {
                m_cameraController.SetTarget(newTarget, true);
            }
            else
            {
                // 아직 컨트롤러가 생성되지 않은 경우 초기화 시도
                Initialize(m_mainCamera, newTarget, m_mapBoundary);
            }
        }

        #endregion
    }
}
