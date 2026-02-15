using System;
using System.Collections.Generic;
using UnityEngine;
using InGame.Data;

namespace InGame.Services
{
    /// <summary>
    /// [설명]: Pure C# 기반의 로그 서비스 구현체입니다.
    /// LogSettings 데이터를 기반으로 로그 필터링 및 포맷팅을 수행합니다.
    /// </summary>
    public class LogService : ILogService
    {
        #region 내부 필드
        private readonly LogSettings m_settings;
        private readonly Dictionary<LogManager.LogCategory, bool> m_categoryEnables = new();
        private readonly Dictionary<LogManager.LogCategory, string> m_prefixCache = new();
        #endregion

        #region 초기화
        public LogService(LogSettings settings)
        {
            m_settings = settings;
            InitializeCache();
        }

        private void InitializeCache()
        {
            if (m_settings == null) return;

            foreach (var setting in m_settings.CategorySettings)
            {
                m_categoryEnables[setting.Category] = setting.Enabled;
                
                string colorHex = ColorUtility.ToHtmlStringRGB(setting.Color == default ? Color.white : setting.Color);
                m_prefixCache[setting.Category] = $"<color=#{colorHex}><b>[{setting.Category}]</b></color>";
            }
        }
        #endregion

        #region 인터페이스 구현
        [UnityEngine.HideInCallstack]
        public void Log(string message, LogManager.LogCategory category = LogManager.LogCategory.Default, UnityEngine.Object context = null)
        {
            if (m_settings == null || !m_settings.EnableLog || !IsCategoryEnabled(category)) return;
            Debug.Log(GetFormattedMessage(category, message), context);
        }

        [UnityEngine.HideInCallstack]
        public void LogWarning(string message, LogManager.LogCategory category = LogManager.LogCategory.Default, UnityEngine.Object context = null)
        {
            if (m_settings == null || !m_settings.EnableWarning || !IsCategoryEnabled(category)) return;
            Debug.LogWarning(GetFormattedMessage(category, message), context);
        }

        [UnityEngine.HideInCallstack]
        public void LogError(string message, LogManager.LogCategory category = LogManager.LogCategory.Default, UnityEngine.Object context = null)
        {
            if (m_settings == null || !m_settings.EnableError || !IsCategoryEnabled(category)) return;
            Debug.LogError(GetFormattedMessage(category, message), context);
        }

        [UnityEngine.HideInCallstack]
        public void LogException(Exception exception, LogManager.LogCategory category = LogManager.LogCategory.Default, UnityEngine.Object context = null)
        {
            if (m_settings == null || !m_settings.EnableError || !IsCategoryEnabled(category)) return;
            Debug.LogException(exception, context);
        }

        [UnityEngine.HideInCallstack]
        public void LogAssert(bool condition, string message, LogManager.LogCategory category = LogManager.LogCategory.Default, UnityEngine.Object context = null)
        {
            if (condition) return;
            LogError($"Assert Failed: {message}", category, context);
        }
        #endregion

        #region 내부 로직
        private bool IsCategoryEnabled(LogManager.LogCategory category)
        {
            if (m_categoryEnables.TryGetValue(category, out bool isEnabled))
            {
                return isEnabled;
            }
            return true; // 설정이 없으면 기본 활성화
        }

        private string GetFormattedMessage(LogManager.LogCategory category, string message)
        {
            if (m_prefixCache.TryGetValue(category, out string prefix))
            {
                return $"{prefix} {message}";
            }
            return $"[{category}] {message}";
        }
        #endregion
    }
}
