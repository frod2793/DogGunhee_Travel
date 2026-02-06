using System;
using BackEnd;
using TMPro;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class BackendConnet : MonoBehaviour
{
    [Header("UI 컴포넌트")]
    [Tooltip("구글 해시 키 가져오기를 실행할 버튼입니다.")]
    [SerializeField] private Button m_getHashKeyButton;
    
    [Tooltip("해시 키 결과가 출력될 입력 필드입니다.")]
    [SerializeField] private TMP_InputField m_hashKeyResultField;

    private void Start()
    {
        // 초기화 전에는 버튼 비활성화
        m_getHashKeyButton.interactable = false;
        m_getHashKeyButton.onClick.AddListener(GetGoogleHashKey);

        // 초기화 시작
        InitializeBackendAsync().Forget();
    }
    private async UniTaskVoid InitializeBackendAsync()
    {
        if (Backend.IsInitialized)
        {
            Debug.Log("뒤끝 SDK가 이미 초기화되어 있습니다.");
            m_getHashKeyButton.interactable = true;
            return;
        }

        Debug.Log("뒤끝 SDK 초기화를 시도합니다...");

        // BackendAsync 헬퍼와 뒤끝 SDK의 비동기 초기화 메서드를 사용합니다.
        var bro = await BackendAsync(Backend.InitializeAsync);

        if (bro.IsSuccess())
        {
            Debug.Log("뒤끝 SDK 초기화 성공");
            m_getHashKeyButton.interactable = true;
            if (m_hashKeyResultField != null)
                m_hashKeyResultField.text = "초기화 성공. 버튼을 눌러 해시 키를 확인하세요.";
        }
        else
        {
            Debug.LogError($"뒤끝 SDK 초기화 실패: {bro}");
            if (m_hashKeyResultField != null)
                m_hashKeyResultField.text = $"초기화 실패: {bro.GetMessage()}";
        }
    }
    /// <summary>
    /// 구글 해시 키 추출 (에디터 크래시 방지 적용)
    /// </summary>
    private void GetGoogleHashKey()
    {
        if (m_hashKeyResultField == null) return;

        // 1. 에디터 환경 체크 (가장 중요)
        // 빌드 타겟이 Android여도 에디터라면 실행하지 않아야 크래시가 안 납니다.
        if (Application.isEditor)
        {
            string msg = "에디터에서는 해시 키를 확인할 수 없습니다.\nAPK 빌드 후 모바일에서 확인해주세요.";
            Debug.LogWarning(msg);
            m_hashKeyResultField.text = msg;
            return;
        }

#if UNITY_ANDROID
        try
        {
            // 실제 안드로이드 기기에서만 실행됨
            string googleHash = Backend.Utils.GetGoogleHash();
            
            if (!string.IsNullOrEmpty(googleHash))
            {
                Debug.Log("구글 해시 키 : " + googleHash);
                m_hashKeyResultField.text = googleHash;
            }
            else
            {
                m_hashKeyResultField.text = "해시 키를 가져왔으나 비어있습니다.";
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"구글 해시 키 추출 실패: {e.Message}");
            m_hashKeyResultField.text = $"오류: {e.Message}";
        }
#else
        // 안드로이드 플랫폼이 아닐 경우
        Debug.Log("현재 플랫폼에서는 구글 해시 키를 지원하지 않습니다.");
        m_hashKeyResultField.text = "Android 플랫폼이 아닙니다.";
#endif
    }

    /// <summary>
    /// 콜백 -> UniTask 변환 헬퍼 (람다 축약)
    /// </summary>
    private UniTask<BackendReturnObject> BackendAsync(Action<Backend.BackendCallback> backendCall)
    {
        var tcs = new UniTaskCompletionSource<BackendReturnObject>();
        backendCall(bro => tcs.TrySetResult(bro));
        return tcs.Task;
    }
}