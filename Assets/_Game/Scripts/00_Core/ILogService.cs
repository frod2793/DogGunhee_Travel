using System;
using UnityEngine;

namespace InGame.Services
{
    /// <summary>
    /// [설명]: 로그 서비스의 인터페이스입니다.
    /// 다양한 로그 레벨과 카테고리를 지원하며, 의존성 주입을 위해 사용됩니다.
    /// </summary>
    public interface ILogService
    {
        void Log(string message, LogManager.LogCategory category = LogManager.LogCategory.Default, UnityEngine.Object context = null);
        void LogWarning(string message, LogManager.LogCategory category = LogManager.LogCategory.Default, UnityEngine.Object context = null);
        void LogError(string message, LogManager.LogCategory category = LogManager.LogCategory.Default, UnityEngine.Object context = null);
        void LogException(Exception exception, LogManager.LogCategory category = LogManager.LogCategory.Default, UnityEngine.Object context = null);
        void LogAssert(bool condition, string message, LogManager.LogCategory category = LogManager.LogCategory.Default, UnityEngine.Object context = null);
    }
}
