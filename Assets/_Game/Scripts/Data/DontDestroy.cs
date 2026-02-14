using UnityEngine;

public class DontDestroy : MonoBehaviour
{
    #region 유니티 생명주기 

    private void Awake()
    {
        // 이 오브젝트가 이미 존재하는지 확인
        if (FindObjectsByType<DontDestroy>(FindObjectsSortMode.None).Length > 1)
        {
            Destroy(gameObject); // 중복된 오브젝트는 삭제
        }
        else
        {
            DontDestroyOnLoad(gameObject); // 이 오브젝트를 파괴하지 않음
        }
    }

    private void Start()
    {
        SetScreenResolution();
    }

    private void OnDestroy()
    {
        // 오브젝트가 파괴될 때 로그 출력
        LogManager.Log("DontDestroy 오브젝트가 파괴되었습니다.");
    }

    private void OnApplicationQuit()
    {
        // 애플리케이션이 종료될 때 로그 출력
        LogManager.Log("애플리케이션이 종료됩니다.");
    }

    #endregion

    #region 화면 설정 

    /// <summary>
    /// [설명]: 해상도에 따라 카메라 렌더 사각형을 조정하여 9:16 세로 비율을 고정하고, 남는 영역을 검은색으로 처리합니다.
    /// </summary>
    private void SetScreenResolution()
    {
        float targetAspect = 9f / 16f;
        float windowAspect = (float)Screen.width / (float)Screen.height;
        float scaleHeight = windowAspect / targetAspect;

        Camera camera = Camera.main;
        if (camera == null) return;

        camera.backgroundColor = Color.black; // 남는 영역을 검은색으로 처리
        camera.clearFlags = CameraClearFlags.SolidColor;

        if (scaleHeight < 1.0f)
        {
            Rect rect = camera.rect;
            rect.width = 1.0f;
            rect.height = scaleHeight;
            rect.x = 0;
            rect.y = (1.0f - scaleHeight) / 2.0f;
            camera.rect = rect;
        }
        else
        {
            float scaleWidth = 1.0f / scaleHeight;
            Rect rect = camera.rect;
            rect.width = scaleWidth;
            rect.height = 1.0f;
            rect.x = (1.0f - scaleWidth) / 2.0f;
            rect.y = 0;
            camera.rect = rect;
        }
    }

    #endregion
}