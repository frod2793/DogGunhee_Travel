using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using Vamser_like;

public class LoadAddresaableManager : MonoBehaviour
{
    [Header("UI 요소")]
    [Tooltip("로딩 상태를 표시할 텍스트")]
    [SerializeField] private TMP_Text statusText;
    [Tooltip("다운로드 진행률을 표시할 슬라이더")]
    [SerializeField] private Slider progressBar;
    [Tooltip("상태 텍스트와 프로그레스 바를 포함하는 UI 캔버스")]
    [SerializeField] private GameObject popupCanvas;

    [Header("에셋 설정")]
    [Tooltip("게임 시작 시 미리 다운로드할 에셋들의 Addressable 그룹 이름 목록입니다.")]
    [SerializeField] private List<string> initialAssetGroups = new List<string>();
    [Tooltip("로딩 완료 후 전환할 씬의 이름")]
    [SerializeField] private string nextSceneName = "IntroScene";

    public static LoadAddresaableManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        // 로딩 UI 활성화
        if (popupCanvas != null) popupCanvas.SetActive(true);
        if (progressBar != null) progressBar.value = 0;

        try
        {
            await LoadInitialAssetsAsync();
            
            // 로딩이 완료되면 다음 씬으로 전환
            SceneLoader.Instance.LoadScene(nextSceneName);
        }
        catch (Exception e)
        {
            // 네트워크 오류 등 예외 처리
            UpdateStatus($"Error: {e.Message}\nPlease check your connection and restart.");
            Debug.LogError($"[LoadAddresaableManager] Failed to load Addressables: {e.Message}");
            // 여기서 사용자에게 재시도 또는 종료 버튼을 보여줄 수 있습니다.
        }
    }

    /// <summary>
    /// Addressables 초기화, 카탈로그 업데이트, 필수 에셋 다운로드를 순차적으로 처리합니다.
    /// </summary>
    private async UniTask LoadInitialAssetsAsync()
    {
        // 1. Addressables 초기화
        UpdateStatus("Initializing...");
        await Addressables.InitializeAsync().ToUniTask(Progress.Create<float>(p => UpdateProgressBar(p * 0.1f))); // 0% -> 10%
        
        // 2. 카탈로그 업데이트 확인
        UpdateStatus("Checking for updates...");
        var checkHandle = Addressables.CheckForCatalogUpdates(false);
        await checkHandle.ToUniTask(Progress.Create<float>(p => UpdateProgressBar(0.1f + p * 0.1f))); // 10% -> 20%

        if (checkHandle.Status == AsyncOperationStatus.Succeeded && checkHandle.Result.Count > 0)
        {
            // 3. 최신 카탈로그로 업데이트
            UpdateStatus("Downloading updates...");
            var updateHandle = Addressables.UpdateCatalogs(checkHandle.Result, false);
            await updateHandle.ToUniTask(Progress.Create<float>(p => UpdateProgressBar(0.2f + p * 0.2f))); // 20% -> 40%
            Addressables.Release(updateHandle);
        }
        Addressables.Release(checkHandle);

        // 4. 필수 에셋 다운로드 크기 확인
        if (initialAssetGroups != null && initialAssetGroups.Count > 0)
        {
            UpdateStatus("Calculating download size...");
            var sizeHandle = Addressables.GetDownloadSizeAsync(initialAssetGroups);
            await sizeHandle.ToUniTask(Progress.Create<float>(p => UpdateProgressBar(0.4f + p * 0.1f))); // 40% -> 50%

            if (sizeHandle.Result > 0)
            {
                // 5. 필수 에셋 다운로드
                UpdateStatus($"Downloading assets ({sizeHandle.Result / 1024f / 1024f:F2} MB)...");
                var downloadHandle = Addressables.DownloadDependenciesAsync(initialAssetGroups, true);
                await downloadHandle.ToUniTask(Progress.Create<float>(p => UpdateProgressBar(0.5f + p * 0.5f))); // 50% -> 100%
                Addressables.Release(downloadHandle);
            }
            Addressables.Release(sizeHandle);
        }
        
        UpdateStatus("Loading complete!");
        UpdateProgressBar(1.0f);
        await UniTask.Delay(TimeSpan.FromMilliseconds(500)); // "완료" 메시지를 잠시 보여줍니다.
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log($"[LoadAddresaableManager] {message}");
    }

    private void UpdateProgressBar(float progress)
    {
        if (progressBar != null) progressBar.value = progress;
    }
}
