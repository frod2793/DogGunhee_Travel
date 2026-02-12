using InGame.Manager;
using InGame.Player.Player_Base;
using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어 추적 카메라를 제어하는 MonoBehaviour 컴포넌트입니다.
    /// <br/> PlayerController에서 분리되어 단일 책임 원칙(SRP)을 준수합니다.
    /// </summary>
    public class PlayerCameraAgent : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("카메라 설정")]
        [SerializeField, Tooltip("추적할 타겟 (플레이어)")]
        private Transform m_targetTransform;

        [SerializeField, Tooltip("맵 경계 스프라이트 (이동 제한용)")]
        private SpriteRenderer m_mapBoundary;

        [SerializeField, Tooltip("카메라 이동 부드러움 정도 (작을수록 빠름)")]
        private float m_smoothTime = 0.1f;

        #endregion

        #region 2. 내부 로직 및 변수

        private PlayerCameraController m_cameraController;

        #endregion

        #region 3. 초기화 (Initialization)

        /// <summary>
        /// 카메라 추적 로직을 초기화합니다.
        /// </summary>
        /// <param name="target">추적할 타겟 트랜스폼 (선택사항, null이면 Inspector 설정 사용)</param>
        /// <param name="mapBoundary">맵 경계 (선택사항)</param>
        public void Initialize(Transform target = null, SpriteRenderer mapBoundary = null)
        {
            if (target != null) m_targetTransform = target;
            if (mapBoundary != null) m_mapBoundary = mapBoundary;

            if (GameManager.Instance != null && GameManager.Instance.MainCamera != null && m_targetTransform != null)
            {
                m_cameraController = new PlayerCameraController(
                    GameManager.Instance.MainCamera, 
                    m_targetTransform, 
                    m_mapBoundary, 
                    m_smoothTime
                );
                
                // 초기 위치로 즉시 이동
                m_cameraController.ResetPosition();
            }
            else
            {
                LogManager.Log("[PlayerCameraAgent] 초기화 실패: 필수 컴포넌트 누락", LogManager.LogCategory.System);
            }
        }

        #endregion

        #region 4. 유니티 생명주기 (Lifecycle)

        private void LateUpdate()
        {
            // 카메라 이동 로직 수행
            m_cameraController?.OnLateUpdate();
        }

        #endregion

        #region 5. 공개 메서드 (Public Methods)

        /// <summary>
        /// 런타임에 추적 대상을 변경합니다.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            m_targetTransform = newTarget;

            if (m_cameraController != null)
            {
                m_cameraController.SetTarget(newTarget, true); 
            }
            else
            {
                // 컨트롤러가 없으면 초기화
                Initialize(newTarget, m_mapBoundary);
            }
        }

        #endregion
    }
}
