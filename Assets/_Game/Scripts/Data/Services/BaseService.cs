using System;
using BackEnd;
using Cysharp.Threading.Tasks;

namespace InGame.Services
{
    /// <summary>
    /// 모든 서버 서비스의 기초가 되는 추상 클래스입니다.
    /// 공통적인 비동기 호출 래퍼 및 로그 기능을 제공합니다.
    /// </summary>
    public abstract class BaseService
    {
        #region 내부 필드
        protected readonly UniTaskCompletionSource<bool> m_backendInitialized;
        #endregion

        #region 생성자
        protected BaseService(UniTaskCompletionSource<bool> backendInitialized)
        {
            m_backendInitialized = backendInitialized;
        }
        #endregion

        #region 공통 유틸리티 메서드

        /// <summary>
        /// 뒤끝 비동기 콜백 메서드를 UniTask로 변환합니다.
        /// </summary>
        protected UniTask<BackendReturnObject> BackendCallAsync(Action<Backend.BackendCallback> backendCall)
        {
            var tcs = new UniTaskCompletionSource<BackendReturnObject>();
            backendCall(bro => tcs.TrySetResult(bro));
            return tcs.Task;
        }

        /// <summary>
        /// 오류 로그를 출력합니다.
        /// </summary>
        protected void LogError(string category, BackendReturnObject bro)
        {
            LogManager.LogError($"[{category} Error] {bro.GetStatusCode()} / {bro.GetErrorCode()} / {bro.GetMessage()}", LogManager.LogCategory.ServerManager);
        }

        /// <summary>
        /// 일반 로그를 출력합니다.
        /// </summary>
        protected void Log(string message)
        {
            LogManager.Log(message, LogManager.LogCategory.ServerManager);
        }

        /// <summary>
        /// 경고 로그를 출력합니다.
        /// </summary>
        protected void LogWarning(string message)
        {
            LogManager.LogWarning(message, LogManager.LogCategory.ServerManager);
        }

        #endregion
    }
}
