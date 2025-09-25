using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using System.Threading;

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
    private CancellationTokenSource _cts;

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

        // 씬에 있는 모든 CanvasScaler를 자동으로 찾아 관리 목록에 추가합니다.
        _managedCanvasScalers = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (_managedCanvasScalers.Length == 0)
        {
            Debug.LogWarning("No CanvasScalers found in the scene for CanvasManager to manage.", this);
        }

        // DontDestroyOnLoad(gameObject); // 씬 전환 시에도 유지되도록 설정 (필요 시)
        
        // 초기 화면 비율 설정
#if UNITY_EDITOR
        // 에디터에서는 Screen.width/height가 실시간으로 변하지 않으므로, Update에서 폴링하는 것이 더 안정적입니다.
        UpdateCameraRect();
#elif UNITY_WEBGL
        _cts = new CancellationTokenSource();
        CheckScreenSizeLoop(_cts.Token).Forget();
#endif
#else
        // WebGL이 아닌 다른 플랫폼에서는 이 스크립트를 비활성화합니다.
        enabled = false;
#endif
    }

#if UNITY_EDITOR
    private void Update()
    {
        // 에디터 플레이 모드에서 Game 뷰의 크기가 변경될 때마다 Rect를 다시 계산합니다.
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
        {
            UpdateCameraRect();
        }
    }
#endif

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private async UniTaskVoid CheckScreenSizeLoop(CancellationToken token)
    {
        // WebGL 빌드에서만 사용되는 비동기 루프입니다.
        UpdateCameraRect(); // 시작 시 한 번 실행
        while (!token.IsCancellationRequested)
        {
            await UniTask.WaitUntil(() => Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight, cancellationToken: token);
            UpdateCameraRect();
        }
    }

    private void UpdateCameraRect()
    {
        if (_mainCamera == null) return;

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
