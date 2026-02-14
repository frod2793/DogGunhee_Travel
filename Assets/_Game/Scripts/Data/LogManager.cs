using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

/// <summary>
/// [설명]: 게임의 전역 로그 출력을 관리하며, 카테고리별 필터링 및 컬러링 기능을 제공하는 싱글톤 매니저입니다.
/// </summary>
public class LogManager : MonoBehaviour
{
    #region 로그 카테고리 정의 

    /// <summary>
    /// [설명]: 로그를 분류하기 위한 카테고리 열거형입니다.
    /// </summary>
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
        MobBase,
        Weapon,
        EffectManager,
        StoreManager,
        QuestManager,
        System
    }

    #endregion

    #region 내부 필드 

    [Header("로그 전체 활성화 설정")]
    [Tooltip("전체 일반 로그 활성화 여부")]
    [SerializeField]
    private bool m_enableDebugLog = true;

    [Tooltip("전체 경고 로그 활성화 여부")]
    [SerializeField]
    private bool m_enableWarningLog = true;

    [Tooltip("전체 에러 로그 활성화 여부")]
    [SerializeField]
    private bool m_enableErrorLog = true;

    [Header("카테고리별 개별 설정")]
    [SerializeField]
    private List<LogCategorySetting> m_categorySettings = new List<LogCategorySetting>();

    // 런타임 최적화를 위한 캐시 데이터
    private Dictionary<LogCategory, bool> m_logEnablesCached = new Dictionary<LogCategory, bool>();
    private Dictionary<LogCategory, string> m_categoryPrefixesCached = new Dictionary<LogCategory, string>();

    // 싱글톤
    private static LogManager s_instance;

    public static LogManager Instance
    {
        get
        {
            if (s_instance == null)
            {
                s_instance = FindFirstObjectByType<LogManager>();
            }

            return s_instance;
        }
    }

    [Serializable]
    public struct LogCategorySetting
    {
        public LogCategory Category;
        public bool Enabled;
        public Color TextColor;

        public LogCategorySetting(LogCategory category, bool enabled, Color color)
        {
            Category = category;
            Enabled = enabled;
            TextColor = color;
        }
    }

    #endregion

    #region 유니티 생명주기 

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeCache();
    }

    #endregion

    #region 초기화 

    /// <summary>
    /// [설명]: 카테고리별 활성화 여부와 프리픽스 문자열을 미리 계산하여 캐싱합니다.
    /// </summary>
    private void InitializeCache()
    {
        m_logEnablesCached.Clear();
        m_categoryPrefixesCached.Clear();

        var categories = Enum.GetValues(typeof(LogCategory)).Cast<LogCategory>();

        foreach (var category in categories)
        {
            // 1. 활성화 여부 캐싱
            var setting = m_categorySettings.Find(x => x.Category == category);
            bool isEnabled = m_categorySettings.Count == 0 || setting.Enabled; // 설정이 없으면 기본 활성
            m_logEnablesCached[category] = isEnabled;

            // 2. 프리픽스 문자열 캐싱 (GC Alloc 줄이기 위함)
            string colorHex = ColorUtility.ToHtmlStringRGB(setting.TextColor == default ? Color.white : setting.TextColor);
            m_categoryPrefixesCached[category] = $"<color=#{colorHex}><b>[{category}]</b></color>";
        }
    }

    /// <summary>
    /// [설명]: 카테고리가 활성화되어 있는지 확인합니다.
    /// </summary>
    private bool IsCategoryEnabled(LogCategory category)
    {
        if (m_logEnablesCached.TryGetValue(category, out bool isEnabled))
        {
            return isEnabled;
        }

        return true;
    }

    /// <summary>
    /// [설명]: 카테고리에 맞는 formatted 로그 메시지를 생성합니다.
    /// </summary>
    private string GetFormattedMessage(LogCategory category, string message)
    {
        if (m_categoryPrefixesCached.TryGetValue(category, out string prefix))
        {
            return $"{prefix} {message}";
        }

        return $"[{category}] {message}";
    }

    #endregion

    #region 공개 메서드 

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(string message, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
        if (Instance == null || !Instance.m_enableDebugLog || !Instance.IsCategoryEnabled(category)) return;
        Debug.Log(Instance.GetFormattedMessage(category, message), context);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogFormat(LogCategory category, string format, params object[] args)
    {
        if (Instance == null || !Instance.m_enableDebugLog || !Instance.IsCategoryEnabled(category)) return;
        string message = string.Format(format, args); // Note: params object[] still causes some alloc, but structured
        Debug.Log(Instance.GetFormattedMessage(category, message));
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(string message, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
        if (Instance == null || !Instance.m_enableWarningLog || !Instance.IsCategoryEnabled(category)) return;
        Debug.LogWarning(Instance.GetFormattedMessage(category, message), context);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogError(string message, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
        if (Instance == null || !Instance.m_enableErrorLog || !Instance.IsCategoryEnabled(category)) return;
        Debug.LogError(Instance.GetFormattedMessage(category, message), context);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogException(Exception exception, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
        if (Instance == null || !Instance.m_enableErrorLog || !Instance.IsCategoryEnabled(category)) return;
        Debug.LogException(exception, context);
    }

    /// <summary>
    /// [설명]: 조건이 거짓일 때 에러 로그를 출력합니다.
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogAssert(bool condition, string message, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
        if (condition) return;
        LogError($"Assert Failed: {message}", category, context);
    }

    #endregion

    #region 런타임 제어 

    public void SetDebugLog(bool enabled) => m_enableDebugLog = enabled;
    public void SetWarningLog(bool enabled) => m_enableWarningLog = enabled;
    public void SetErrorLog(bool enabled) => m_enableErrorLog = enabled;

    /// <summary>
    /// [설명]: 동적으로 카테고리 활성화 여부를 변경합니다.
    /// </summary>
    public void SetCategoryEnable(LogCategory category, bool enabled)
    {
        if (m_logEnablesCached.ContainsKey(category))
        {
            m_logEnablesCached[category] = enabled;
        }
    }

    #endregion
}