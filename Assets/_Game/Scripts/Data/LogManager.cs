using UnityEngine;
using System;
using System.Diagnostics;
using InGame.Services;
using Debug = UnityEngine.Debug;

/// <summary>
/// [설명]: 게임 전역 로그 호출을 담당하는 정적 프록시 클래스입니다.
/// 내부적으로 ILogService 구현체를 사용하여 실제 로그를 출력합니다.
/// </summary>
public static class LogManager
{
    #region 로그 카테고리 정의 
    /// <summary> [설명]: 로그 분류 카테고리 </summary>
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
    private static ILogService s_service;
    #endregion

    #region 초기화
    /// <summary>
    /// [설명]: 로그 서비스를 주입하여 초기화합니다.
    /// </summary>
    public static void Initialize(ILogService service)
    {
        s_service = service;
    }
    #endregion

    #region 공개 API (Static Proxy)
    
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    [UnityEngine.HideInCallstack]
    public static void Log(string message, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
        s_service?.Log(message, category, context);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    [UnityEngine.HideInCallstack]
    public static void LogWarning(string message, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
        s_service?.LogWarning(message, category, context);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    [UnityEngine.HideInCallstack]
    public static void LogError(string message, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
        s_service?.LogError(message, category, context);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    [UnityEngine.HideInCallstack]
    public static void LogException(Exception exception, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
        s_service?.LogException(exception, category, context);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    [UnityEngine.HideInCallstack]
    public static void LogAssert(bool condition, string message, LogCategory category = LogCategory.Default, UnityEngine.Object context = null)
    {
        s_service?.LogAssert(condition, message, category, context);
    }

    #endregion
}