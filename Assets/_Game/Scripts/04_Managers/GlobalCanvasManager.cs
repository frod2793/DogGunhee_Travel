using UnityEngine;
using UnityEngine.UI;

namespace InGame.Managers
{
    /// <summary>
    /// [설명]: WebGL 환경에서 화면 비율을 9:16으로 강제하여 일관된 게임 플레이 경험을 제공하는 매니저 클래스입니다.
    /// 화면 양 옆이나 위아래에 레터박스(검은 띠)를 추가하여 비율을 맞춥니다.
    /// </summary>
    public class GlobalCanvasManager : MonoBehaviour
    {
        #region 상수 및 설정

        /// <summary> 목표 화면 비율 (9:16) </summary>
        private const float k_TargetAspectRatio = 9.0f / 16.0f;

        [Header("설정")]
        [Tooltip("비율을 제어할 메인 카메라입니다. 비워두면 Camera.main을 사용합니다.")]
        [SerializeField]
        private Camera m_mainCamera;

        #endregion

        #region 내부 필드

        private CanvasScaler[] m_managedCanvasScalers;
        private int m_lastScreenWidth;
        private int m_lastScreenHeight;

        #endregion

        #region 유니티 생명주기

        private void Start()
        {
#if UNITY_WEBGL || UNITY_EDITOR
            if (m_mainCamera == null)
            {
                m_mainCamera = Camera.main;
            }

            if (m_mainCamera == null)
            {
                Debug.LogError("[GlobalCanvasManager] 메인 카메라를 찾을 수 없습니다. 기능을 비활성화합니다.");
                enabled = false;
                return;
            }

            UpdateCameraRect();
#else
            enabled = false;
#endif
        }

        private void LateUpdate()
        {
#if UNITY_WEBGL || UNITY_EDITOR
            if (Screen.width != m_lastScreenWidth || Screen.height != m_lastScreenHeight)
            {
                UpdateCameraRect();
            }
#endif
        }

        #endregion

        #region 내부 로직

        /// <summary>
        /// [설명]: 메인 카메라의 화면 비율을 업데이트하여 레터박스를 생성합니다.
        /// </summary>
        private void UpdateCameraRect()
        {
            if (m_mainCamera == null)
            {
                return;
            }

            m_managedCanvasScalers = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            m_lastScreenWidth = (int)screenWidth;
            m_lastScreenHeight = (int)screenHeight;

            float currentAspectRatio = screenWidth / screenHeight;

            Rect rect = m_mainCamera.rect;

            if (currentAspectRatio > k_TargetAspectRatio)
            {
                float newWidth = k_TargetAspectRatio / currentAspectRatio;
                rect.width = newWidth;
                rect.height = 1.0f;
                rect.x = (1.0f - newWidth) / 2.0f;
                rect.y = 0;

                foreach (var scaler in m_managedCanvasScalers)
                {
                    if (scaler != null)
                    {
                        scaler.matchWidthOrHeight = 1;
                    }
                }
            }
            else
            {
                float newHeight = currentAspectRatio / k_TargetAspectRatio;
                rect.width = 1.0f;
                rect.height = newHeight;
                rect.x = 0;
                rect.y = (1.0f - newHeight) / 2.0f;

                foreach (var scaler in m_managedCanvasScalers)
                {
                    if (scaler != null)
                    {
                        scaler.matchWidthOrHeight = 0;
                    }
                }
            }

            m_mainCamera.rect = rect;
        }

        #endregion
    }
}