using System;
using BackEnd;
using Cysharp.Threading.Tasks;

namespace InGame.Services
{
    /// <summary>
    /// [설명]: 모든 서버 서비스의 기초가 되는 추상 클래스입니다.
    /// </summary>
    public abstract class BaseService
    {
        #region 내부 필드 

        /// <summary>
        /// [설명]: 백엔드 초기화 상태를 제어하는 비동기 완료 신호입니다.
        /// </summary>
        protected readonly UniTaskCompletionSource<bool> m_backendInitialized;

        #endregion

        #region 초기화 

        /// <summary>
        /// [설명]: 백엔드 초기화 신호를 받아 저장하는 초기화 메서드입니다.
        /// </summary>
        protected BaseService(UniTaskCompletionSource<bool> backendInitialized)
        {
            m_backendInitialized = backendInitialized;
        }

        #endregion

        #region 내부 메서드 

        /// <summary>
        /// [설명]: 뒤끝 비동기 콜백 메서드를 UniTask로 변환합니다.
        /// </summary>
        protected UniTask<BackendReturnObject> BackendCallAsync(Action<Backend.BackendCallback> backendCall)
        {
            var tcs = new UniTaskCompletionSource<BackendReturnObject>();
            backendCall(bro => tcs.TrySetResult(bro));
            return tcs.Task;
        }

        /// <summary>
        /// [설명]: 일반 로그를 출력합니다.
        /// </summary>
        protected void Log(string message)
        {
            LogManager.Log(message, LogManager.LogCategory.ServerManager);
        }

        /// <summary>
        /// [설명]: 경고 로그를 출력합니다.
        /// </summary>
        protected void LogWarning(string message)
        {
            LogManager.LogWarning(message, LogManager.LogCategory.ServerManager);
        }

        /// <summary>
        /// [설명]: 오류 로그를 출력합니다.
        /// </summary>
        protected void LogError(string category, BackendReturnObject bro)
        {
            LogManager.LogError($"[{category} Error] {bro.GetStatusCode()} / {bro.GetErrorCode()} / {bro.GetMessage()}", LogManager.LogCategory.ServerManager);
        }

        #endregion
    }
}