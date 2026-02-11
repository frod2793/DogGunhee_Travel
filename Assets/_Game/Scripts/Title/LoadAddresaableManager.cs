using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using InGame;
using UnityEngine.Serialization;

/// <summary>
/// 어드레서블 에셋의 초기 로딩 및 다운로드를 관리하는 매니저 클래스입니다.
/// </summary>
public class LoadAddresaableManager : MonoBehaviour
{
    #region 1. 에디터 설정 (Inspector)

    [Header("UI 요소")]
    [Tooltip("로딩 상태를 표시할 텍스트")]
    [FormerlySerializedAs("statusText")]
    [SerializeField] private TMP_Text m_statusText;

    [Tooltip("다운로드 진행률을 표시할 슬라이더")]
    [FormerlySerializedAs("progressBar")]
    [SerializeField] private Slider m_progressBar;

    [Tooltip("상태 텍스트와 프로그레스 바를 포함하는 UI 캔버스")]
    [FormerlySerializedAs("popupCanvas")]
    [SerializeField] private GameObject m_popupCanvas;

    [Header("에셋 설정")]
    [Tooltip("게임 시작 시 미리 다운로드할 에셋들의 Addressable 그룹 이름 목록입니다.")]
    [FormerlySerializedAs("initialAssetGroups")]
    [SerializeField] private List<string> m_initialAssetGroups = new List<string>();

    [Tooltip("로딩 완료 후 전환할 씬의 이름")]
    [FormerlySerializedAs("nextSceneName")]
    [SerializeField] private string m_nextSceneName = "IntroScene";

    #endregion

    #region 2. 싱글톤 및 내부 상태

    /// <summary>
    /// 전역 접근을 위한 싱글톤 인스턴스
    /// </summary>
    public static LoadAddresaableManager Instance { get; private set; }

    #endregion

    #region 3. 유니티 생명주기

    /// <summary>
    /// 싱글톤 초기화 및 파괴 방지 설정을 수행합니다.
    /// </summary>
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

    /// <summary>
    /// 로딩 프로세스를 시작합니다.
    /// </summary>
    private async void Start()
    {
        // 로딩 UI 활성화
        if (m_popupCanvas != null) m_popupCanvas.SetActive(true);
        if (m_progressBar != null) m_progressBar.value = 0;

        try
        {
            await LoadInitialAssetsAsync();
            
            // 로딩이 완료되면 다음 씬으로 전환
            SceneLoader.Instance.LoadScene(m_nextSceneName);
        }
        catch (Exception e)
        {
            // 네트워크 오류 등 예외 처리
            UpdateStatus($"Error: {e.Message}\nPlease check your connection and restart.");
            Debug.LogError($"[LoadAddresaableManager] Failed to load Addressables: {e.Message}");
            // 여기서 사용자에게 재시도 또는 종료 버튼을 보여줄 수 있습니다.
        }
    }

    #endregion

    #region 4. 내부 로직 (비즈니스 로직)

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
        if (m_initialAssetGroups != null && m_initialAssetGroups.Count > 0)
        {
            UpdateStatus("Calculating download size...");
            var sizeHandle = Addressables.GetDownloadSizeAsync(m_initialAssetGroups);
            await sizeHandle.ToUniTask(Progress.Create<float>(p => UpdateProgressBar(0.4f + p * 0.1f))); // 40% -> 50%

            if (sizeHandle.Result > 0)
            {
                // 5. 필수 에셋 다운로드
                UpdateStatus($"Downloading assets ({sizeHandle.Result / 1024f / 1024f:F2} MB)...");
                var downloadHandle = Addressables.DownloadDependenciesAsync(m_initialAssetGroups, true);
                await downloadHandle.ToUniTask(Progress.Create<float>(p => UpdateProgressBar(0.5f + p * 0.5f))); // 50% -> 100%
                Addressables.Release(downloadHandle);
            }
            Addressables.Release(sizeHandle);
        }
        
        UpdateStatus("Loading complete!");
        UpdateProgressBar(1.0f);
        await UniTask.Delay(TimeSpan.FromMilliseconds(500)); // "완료" 메시지를 잠시 보여줍니다.
    }

    /// <summary>
    /// 현재 로딩 상태 메시지를 갱신하고 로그를 남깁니다.
    /// </summary>
    /// <param name="message">표시할 메시지</param>
    private void UpdateStatus(string message)
    {
        if (m_statusText != null) m_statusText.text = message;
        Debug.Log($"[LoadAddresaableManager] {message}");
    }

    /// <summary>
    /// 프로그레스 바의 값을 갱신합니다.
    /// </summary>
    /// <param name="progress">진행률 (0.0 ~ 1.0)</param>
    private void UpdateProgressBar(float progress)
    {
        if (m_progressBar != null) m_progressBar.value = progress;
    }

    #endregion
}
