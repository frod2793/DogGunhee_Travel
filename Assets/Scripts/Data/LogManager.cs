using UnityEngine;
using System.Collections.Generic;

//모든 스크립트의 디버그 로그 를 관여하는 디버그 로그 매니져및 로그매니져 
public class LogManager : MonoBehaviour
{
    public enum LogCategory
    {
        Default,
        ServerManager,
        UIManager,
        SoundManager,
        SettingsManager,
        ItemManager,
        PostManager,
        CharacterManager,
        ObjectPoolSpawner,
        PlayStateManager,
        VamserLikeGameManager,
        NormalMob,
        PlayerBase,
        SceneLoader,
        InventoryManager,
        PlayerDataManager,
        VamserLikeUI,
        mobBase,
        Weapon
    }
    public static LogManager Instance { get; private set; }
    [SerializeField] private bool enableDebugLog = true;
    [SerializeField] private bool enableErrorLog = true;
    [SerializeField] private bool enableWarningLog = true;
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

    // 각 매니저별 로그 활성화 설정
    [System.Serializable]
    public class LogCategorySetting {
        public LogCategory category;
        public bool enableLog = true;
    }
    [SerializeField]
    private List<LogCategorySetting> logCategorySettings = new List<LogCategorySetting> {
        new LogCategorySetting { category = LogCategory.Default, enableLog = true },
        new LogCategorySetting { category = LogCategory.ServerManager, enableLog = true },
        new LogCategorySetting { category = LogCategory.UIManager, enableLog = true },
        new LogCategorySetting { category = LogCategory.SoundManager, enableLog = true },
        new LogCategorySetting { category = LogCategory.SettingsManager, enableLog = true },
        new LogCategorySetting { category = LogCategory.ItemManager, enableLog = true },
        new LogCategorySetting { category = LogCategory.PostManager, enableLog = true },
        new LogCategorySetting { category = LogCategory.CharacterManager, enableLog = true },
        new LogCategorySetting { category = LogCategory.ObjectPoolSpawner, enableLog = true },
        new LogCategorySetting { category = LogCategory.PlayStateManager, enableLog = true },
        new LogCategorySetting { category = LogCategory.VamserLikeGameManager, enableLog = true },
        new LogCategorySetting { category = LogCategory.NormalMob, enableLog = true },
        new LogCategorySetting { category = LogCategory.PlayerBase, enableLog = true },
        new LogCategorySetting { category = LogCategory.SceneLoader, enableLog = true },
        new LogCategorySetting { category = LogCategory.InventoryManager, enableLog = true },
        new LogCategorySetting { category = LogCategory.PlayerDataManager, enableLog = true }
        ,
        new LogCategorySetting { category = LogCategory.VamserLikeUI, enableLog = true },
        new LogCategorySetting { category = LogCategory.mobBase, enableLog = true },
        new LogCategorySetting { category = LogCategory.Weapon, enableLog = true }
        // 필요에 따라 추가
    };

    private bool IsCategoryEnabled(LogCategory category)
    {
        var setting = logCategorySettings.Find(x => x.category == category);
        return setting != null ? setting.enableLog : true;
    }

    public static void Log(string message, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Instance == null || !Instance.enableDebugLog) return;
        if (!Instance.IsCategoryEnabled(category)) return;
        Debug.Log($"[{category}] {message}", context);
#endif
    }

    public static void LogWarning(string message, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Instance == null || !Instance.enableWarningLog) return;
        if (!Instance.IsCategoryEnabled(category)) return;
        Debug.LogWarning($"[{category}] {message}", context);
#endif
    }

    public static void LogError(string message, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Instance == null || !Instance.enableErrorLog) return;
        if (!Instance.IsCategoryEnabled(category)) return;
        Debug.LogError($"[{category}] {message}", context);
#endif
    }

    public static void LogException(System.Exception exception, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Instance == null || !Instance.enableErrorLog) return;
        if (!Instance.IsCategoryEnabled(category)) return;
        Debug.LogException(exception, context);
#endif
    }

    public void SetDebugLog(bool enabled) => enableDebugLog = enabled;
    public void SetErrorLog(bool enabled) => enableErrorLog = enabled;
    public void SetWarningLog(bool enabled) => enableWarningLog = enabled;

}
