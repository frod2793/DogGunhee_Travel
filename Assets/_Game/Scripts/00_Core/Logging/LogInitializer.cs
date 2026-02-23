using UnityEngine;
using InGame.Data;
using InGame.Services;

namespace InGame.Bootstrap
{
    /// <summary>
    /// [설명]: 게임 시작 시 로그 시스템을 자동으로 초기화하는 클래스입니다.
    /// Resources 폴더에서 설정을 로드하여 LogManager에 주입합니다.
    /// </summary>
    public static class LogInitializer
    {
        private const string k_SettingsPath = "Settings/LogSettings";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            LogSettings settings = Resources.Load<LogSettings>(k_SettingsPath);
            
            if (settings == null)
            {
                Debug.LogWarning($"[LogInitializer] LogSettings를 찾을 수 없습니다: Resources/{k_SettingsPath}. 기본 설정으로 시작합니다.");
            }

            ILogService logService = new LogService(settings);
            LogManager.Initialize(logService);
            
            LogManager.Log("로그 시스템이 성공적으로 초기화되었습니다.", LogManager.LogCategory.System);
        }
    }
}
