using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WebGL 환경에서 화면 비율을 9:16으로 강제하여 일관된 게임 플레이 경험을 제공하는 매니저입니다.
/// 화면 양 옆이나 위아래에 레터박스(검은 띠)를 추가하여 비율을 맞춥니다.
/// </summary>
public class CanvasManager : MonoBehaviour
{
    // 목표 화면 비율 (9:16)
    private const float TargetAspectRatio = 9.0f / 16.0f;
    
    [Header("설정")]
    [Tooltip("비율을 제어할 메인 카메라입니다. 비워두면 Camera.main을 사용합니다.")]
    [SerializeField] private Camera _mainCamera; 
    private CanvasScaler[] _managedCanvasScalers;
    private int _lastScreenWidth;
    private int _lastScreenHeight;

    void Start()
    {
#if UNITY_WEBGL || UNITY_EDITOR
        // 메인 카메라를 찾아 캐싱합니다.
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogError("Main Camera not found. CanvasManager cannot function.");
            enabled = false; // 스크립트 비활성화
            return;
        }

        // DontDestroyOnLoad(gameObject); // 씬 전환 시에도 유지되도록 설정 (필요 시)
        
        // 시작 시 화면 비율을 한 번 설정합니다.
        UpdateCameraRect();
#else
        // WebGL이 아닌 다른 플랫폼에서는 이 스크립트를 비활성화합니다.
        enabled = false;
#endif
    }

    // Update 대신 LateUpdate를 사용하여, 해당 프레임의 모든 로직이 끝난 후 마지막에 비율을 조정합니다.
    // 이는 다른 스크립트나 CanvasScaler의 내부 로직에 의해 설정이 덮어쓰이는 것을 방지하는 가장 안정적인 방법입니다.
    private void LateUpdate()
    {
#if UNITY_WEBGL || UNITY_EDITOR
        // 화면 해상도가 변경되었을 때만 Rect를 다시 계산하여 성능을 최적화합니다.
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
        {
            UpdateCameraRect();
        }
#endif
    }

    private void UpdateCameraRect()
    {
        if (_mainCamera == null) return;

        // [수정] 화면 비율을 업데이트할 때마다 씬의 모든 CanvasScaler를 다시 찾아, 동적으로 로드된 UI도 처리할 수 있도록 합니다.
        // 이는 실행 순서에 따른 참조 누락 문제를 근본적으로 해결합니다.
        _managedCanvasScalers = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        _lastScreenWidth = (int)screenWidth;
        _lastScreenHeight = (int)screenHeight;

        float currentAspectRatio = screenWidth / screenHeight;

        Rect rect = _mainCamera.rect;

        if (currentAspectRatio > TargetAspectRatio) // 현재 화면이 목표 비율보다 가로로 길 경우 (예: PC 와이드 스크린)
        {
            // 높이를 1로 고정하고, 너비를 비율에 맞게 줄여서 좌우에 레터박스를 생성합니다.
            float newWidth = TargetAspectRatio / currentAspectRatio;
            rect.width = newWidth;
            rect.height = 1.0f;
            rect.x = (1.0f - newWidth) / 2.0f;
            rect.y = 0;

            // UI가 높이에 맞춰 스케일되도록 설정 (Match Height)
            foreach (var scaler in _managedCanvasScalers)
            {
                if (scaler != null)
                {
                    scaler.matchWidthOrHeight = 1;
                }
            }
        }
        else // 현재 화면이 목표 비율보다 세로로 길 경우 (예: 모바일 세로 모드)
        {
            // 너비를 1로 고정하고, 높이를 비율에 맞게 줄여서 위아래에 레터박스를 생성합니다.
            float newHeight = currentAspectRatio / TargetAspectRatio;
            rect.width = 1.0f;
            rect.height = newHeight;
            rect.x = 0;
            rect.y = (1.0f - newHeight) / 2.0f;
            
            // UI가 너비에 맞춰 스케일되도록 설정 (Match Width)
            foreach (var scaler in _managedCanvasScalers)
            {
                if (scaler != null)
                {
                    scaler.matchWidthOrHeight = 0;
                }
            }
        }

        _mainCamera.rect = rect;
    }
}
