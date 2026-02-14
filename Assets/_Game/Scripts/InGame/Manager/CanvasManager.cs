using UnityEngine;
using UnityEngine.UI;

namespace InGame.Managers
{
    /// <summary>
    /// [설명]: WebGL 환경에서 화면 비율을 9:16으로 강제하여 일관된 게임 플레이 경험을 제공하는 매니저 클래스입니다.
    /// 화면 양 옆이나 위아래에 레터박스(검은 띠)를 추가하여 비율을 맞춥니다.
    /// </summary>
    public class CanvasManager : MonoBehaviour
    {
        #region 상수 및 설정

        /// <summary> 목표 화면 비율 (9:16) </summary>
        private const float k_TargetAspectRatio = 9.0f / 16.0f;

        [Header("설정")]
        [Tooltip("비율을 제어할 메인 카메라입니다. 비워두면 Camera.main을 사용합니다.")]
        [SerializeField] private Camera m_mainCamera;

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
            // 메인 카메라를 찾아 캐싱합니다.
            if (m_mainCamera == null)
            {
                m_mainCamera = Camera.main;
            }

            if (m_mainCamera == null)
            {
                Debug.LogError("[CanvasManager] Main Camera를 찾을 수 없습니다. 기능을 중지합니다.");
                enabled = false;
                return;
            }

            // 시작 시 화면 비율을 한 번 설정합니다.
            UpdateCameraRect();
#else
            // WebGL이 아닌 다른 플랫폼에서는 이 스크립트를 비활성화합니다.
            enabled = false;
#endif
        }

        /// <summary>
        /// [설명]: Update 대신 LateUpdate를 사용하여, 모든 로직이 끝난 후 마지막에 비율을 조정합니다.
        /// </summary>
        private void LateUpdate()
        {
#if UNITY_WEBGL || UNITY_EDITOR
            // 화면 해상도가 변경되었을 때만 Rect를 다시 계산하여 성능을 최적화합니다.
            if (Screen.width != m_lastScreenWidth || Screen.height != m_lastScreenHeight)
            {
                UpdateCameraRect();
            }
#endif
        }

        #endregion

        #region 내부 로직

        /// <summary>
        /// [설명]: 현재 화면 해상도를 기반으로 카메라의 Rect와 CanvasScaler의 Match 설정을 업데이트합니다.
        /// </summary>
        private void UpdateCameraRect()
        {
            if (m_mainCamera == null)
            {
                return;
            }

            // 동적으로 로드된 UI도 처리할 수 있도록 씬의 모든 CanvasScaler를 다시 찾습니다.
            m_managedCanvasScalers = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            m_lastScreenWidth = (int)screenWidth;
            m_lastScreenHeight = (int)screenHeight;

            float currentAspectRatio = screenWidth / screenHeight;
            Rect rect = m_mainCamera.rect;

            // 1. 현재 화면이 목표 비율보다 가로로 길 경우 (와이드 스크린)
            if (currentAspectRatio > k_TargetAspectRatio)
            {
                float newWidth = k_TargetAspectRatio / currentAspectRatio;
                rect.width = newWidth;
                rect.height = 1.0f;
                rect.x = (1.0f - newWidth) / 2.0f;
                rect.y = 0;

                // UI가 높이에 맞춰 스케일되도록 설정 (Match Height)
                UpdateCanvasScalersMatch(1);
            }
            // 2. 현재 화면이 목표 비율보다 세로로 길 경우 (모바일 세로 모드)
            else
            {
                float newHeight = currentAspectRatio / k_TargetAspectRatio;
                rect.width = 1.0f;
                rect.height = newHeight;
                rect.x = 0;
                rect.y = (1.0f - newHeight) / 2.0f;

                // UI가 너비에 맞춰 스케일되도록 설정 (Match Width)
                UpdateCanvasScalersMatch(0);
            }

            m_mainCamera.rect = rect;
        }

        /// <summary>
        /// [설명]: 관리 중인 모든 CanvasScaler의 Match 값을 일괄 변경합니다.
        /// </summary>
        private void UpdateCanvasScalersMatch(float matchValue)
        {
            if (m_managedCanvasScalers == null)
            {
                return;
            }

            foreach (var scaler in m_managedCanvasScalers)
            {
                if (scaler != null)
                {
                    scaler.matchWidthOrHeight = matchValue;
                }
            }
        }

        #endregion
    }
}


