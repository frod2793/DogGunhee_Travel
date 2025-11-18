using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

// 모든 스크립트의 디버그 로그를 관여하는 디버그 로그 매니저
public class LogManager : MonoBehaviour
{
    public enum LogCategory // 로그 카테고리 열거형
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
        Weapon,
        EffectManager
    }

    [Tooltip("전체 디버그 로그 활성화 여부")]
    [SerializeField] private bool m_enableDebugLog = true;
    [Tooltip("전체 오류 로그 활성화 여부")]
    [SerializeField] private bool m_enableErrorLog = true;
    [Tooltip("전체 경고 로그 활성화 여부")]
    [SerializeField] private bool m_enableWarningLog = true;

    // 각 매니저별 로그 활성화 설정 (인스펙터 노출용)
    [SerializeField]
    private List<bool> m_logCategoryEnables = new List<bool>();

    // 성능 최적화를 위한 딕셔너리 캐시
    private readonly Dictionary<LogCategory, bool> m_logEnables = new Dictionary<LogCategory, bool>();

    public static LogManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 딕셔너리 캐시 초기화
        m_logEnables.Clear();
        var categories = Enum.GetValues(typeof(LogCategory)).Cast<LogCategory>().ToArray();

        // 리스트 크기가 Enum 개수와 맞지 않으면 초기화
        if (m_logCategoryEnables.Count != categories.Length)
        {
            m_logCategoryEnables = new List<bool>(new bool[categories.Length]);
            for (int i = 0; i < m_logCategoryEnables.Count; i++)
            {
                m_logCategoryEnables[i] = true;
            }
        }
        
        for (int i = 0; i < categories.Length; i++)
        {
            m_logEnables[categories[i]] = m_logCategoryEnables[i];
        }
    }

    private bool IsCategoryEnabled(LogCategory category)
    {
        // 딕셔너리를 사용하여 O(1) 시간 복잡도로 조회
        if (m_logEnables.TryGetValue(category, out bool isEnabled))
        {
            return isEnabled;
        }
        // 딕셔너리에 없는 경우(Awake 전 호출 등) 기본값 true 반환
        return true;
    }

    public static void Log(string message, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Instance == null || !Instance.m_enableDebugLog) return;
        if (!Instance.IsCategoryEnabled(category)) return;
        Debug.Log($"[{category}] {message}", context);
#endif
    }

    public static void LogWarning(string message, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Instance == null || !Instance.m_enableWarningLog) return;
        if (!Instance.IsCategoryEnabled(category)) return;
        Debug.LogWarning($"[{category}] {message}", context);
#endif
    }

    public static void LogError(string message, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Instance == null || !Instance.m_enableErrorLog) return;
        if (!Instance.IsCategoryEnabled(category)) return;
        Debug.LogError($"[{category}] {message}", context);
#endif
    }

    public static void LogException(Exception exception, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Instance == null || !Instance.m_enableErrorLog) return;
        if (!Instance.IsCategoryEnabled(category)) return;
        Debug.LogException(exception, context);
#endif
    }

    public void SetDebugLog(bool enabled) => m_enableDebugLog = enabled;
    public void SetErrorLog(bool enabled) => m_enableErrorLog = enabled;
    public void SetWarningLog(bool enabled) => m_enableWarningLog = enabled;

}
