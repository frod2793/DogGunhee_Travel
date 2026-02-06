using System;
using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;

namespace InGame.Services
{
    /// <summary>
    /// 게임 데이터(Table) 저장/로드를 담당하는 POCO 서비스입니다.
    /// MonoBehaviour에 의존하지 않으며, UniTask를 사용하여 비동기 처리를 수행합니다.
    /// </summary>
    public class GameDataService
    {
        #region 내부 필드

        private readonly UniTaskCompletionSource<bool> m_backendInitialized;

        // [최적화] 테이블별 inDate 캐싱 (초기 용량 설정으로 재할당 방지)
        private readonly Dictionary<string, string> m_tableInDate = new Dictionary<string, string>(8);

        #endregion

        #region 생성자

        /// <summary>
        /// GameDataService 인스턴스를 생성합니다.
        /// </summary>
        /// <param name="backendInitialized">뒤끝 SDK 초기화 완료를 알리는 Task</param>
        public GameDataService(UniTaskCompletionSource<bool> backendInitialized)
        {
            m_backendInitialized = backendInitialized;
        }

        #endregion

        #region 공개 메서드 (데이터 저장/로드)

        /// <summary>
        /// 지정된 테이블에 데이터를 업로드(Insert 또는 Update)합니다.
        /// </summary>
        /// <param name="tableName">테이블 이름</param>
        /// <param name="param">저장할 데이터 파라미터</param>
        public async UniTask UploadDataAsync(string tableName, Param param)
        {
            await m_backendInitialized.Task;

            BackendReturnObject bro;

            if (m_tableInDate.TryGetValue(tableName, out string inDate))
            {
                // 기존 데이터 업데이트
                LogManager.Log($"[{tableName}] 업데이트 요청. inDate: {inDate} / 파라미터 수: {param.Count}", LogManager.LogCategory.ServerManager);
                bro = await BackendCallAsync(callback => Backend.GameData.UpdateV2(tableName, inDate, Backend.UserInDate, param, callback));
            }
            else
            {
                // 신규 데이터 삽입
                LogManager.Log($"[{tableName}] 삽입 요청 (신규 데이터). 파라미터 수: {param.Count}", LogManager.LogCategory.ServerManager);
                bro = await BackendCallAsync(callback => Backend.GameData.Insert(tableName, param, callback));

                if (bro.IsSuccess())
                {
                    string newInDate = bro.GetInDate();
                    m_tableInDate[tableName] = newInDate;
                    LogManager.Log($"[{tableName}] 삽입 성공. 새로운 inDate: {newInDate}", LogManager.LogCategory.ServerManager);
                }
            }

            if (!bro.IsSuccess())
            {
                LogError(bro);
                LogManager.LogError($"[{tableName}] 업로드 실패: {bro.GetStatusCode()} - {bro.GetMessage()}", LogManager.LogCategory.ServerManager);
                throw new Exception($"데이터 업로드 실패 ({tableName}): {bro.GetMessage()}");
            }

            LogManager.Log($"{tableName} 업로드(Update/Insert) 완료", LogManager.LogCategory.ServerManager);
        }

        /// <summary>
        /// 지정된 테이블에서 데이터를 다운로드합니다.
        /// </summary>
        /// <param name="tableName">테이블 이름</param>
        /// <returns>JSON 데이터 또는 데이터가 없으면 null</returns>
        public async UniTask<JsonData> DownloadDataAsync(string tableName)
        {
            await m_backendInitialized.Task;

            var bro = await BackendCallAsync(callback => Backend.GameData.GetMyData(tableName, new Where(), callback));

            if (bro.IsSuccess())
            {
                var gameDataJson = bro.FlattenRows();
                if (gameDataJson.Count > 0)
                {
                    if (gameDataJson.Count > 1)
                    {
                        LogManager.LogWarning($"[{tableName}] 중복된 데이터 행 발견! (Count: {gameDataJson.Count}) - 최신 행을 사용해야 합니다.", LogManager.LogCategory.ServerManager);
                    }

                    var row = gameDataJson[0];
                    m_tableInDate[tableName] = row["inDate"].ToString();
                    LogManager.Log($"{tableName} 다운로드 완료 (inDate: {m_tableInDate[tableName]})", LogManager.LogCategory.ServerManager);
                    return row;
                }

                LogManager.Log($"{tableName} 데이터 없음", LogManager.LogCategory.ServerManager);
                return null;
            }

            LogError(bro);
            throw new Exception($"데이터 다운로드 실패 ({tableName}): {bro.GetMessage()}");
        }

        /// <summary>
        /// 캐시된 inDate 값을 가져옵니다.
        /// </summary>
        /// <param name="tableName">테이블 이름</param>
        /// <returns>inDate 문자열 또는 null</returns>
        public string GetCachedInDate(string tableName)
        {
            return m_tableInDate.TryGetValue(tableName, out string inDate) ? inDate : null;
        }

        #endregion

        #region 내부 헬퍼

        /// <summary>
        /// 뒤끝 비동기 콜백 메서드를 UniTask로 변환합니다.
        /// </summary>
        private UniTask<BackendReturnObject> BackendCallAsync(Action<Backend.BackendCallback> backendCall)
        {
            var tcs = new UniTaskCompletionSource<BackendReturnObject>();
            backendCall(bro => tcs.TrySetResult(bro));
            return tcs.Task;
        }

        /// <summary>
        /// 오류 로그를 출력합니다.
        /// </summary>
        private void LogError(BackendReturnObject bro)
        {
            LogManager.LogError($"[GameData Error] {bro.GetStatusCode()} / {bro.GetErrorCode()} / {bro.GetMessage()}", LogManager.LogCategory.ServerManager);
        }

        #endregion
    }
}
