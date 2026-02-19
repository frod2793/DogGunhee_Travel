using System;
using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;

namespace InGame.Services
{
    /// <summary>
    /// [설명]: 게임 데이터(Table) 저장/로드를 담당하는 POCO 서비스입니다.
    /// </summary>
    public class GameDataService : BaseService, IGameDataService
    {
        #region 내부 필드 

        private readonly Dictionary<string, string> m_tableInDate = new Dictionary<string, string>(8);

        #endregion

        #region 생성자 

        public GameDataService(UniTaskCompletionSource<bool> backendInitialized) : base(backendInitialized)
        {
        }

        #endregion

        #region 공개 메서드 

        /// <summary>
        /// [설명]: 주어진 테이블 이름과 파라미터로 데이터를 업데이트하거나 삽입합니다.
        /// </summary>
        public async UniTask UploadDataAsync(string tableName, Param param)
        {
            await m_backendInitialized.Task;

            BackendReturnObject bro;

            if (m_tableInDate.TryGetValue(tableName, out string inDate))
            {
                Log($"[{tableName}] 업데이트 요청. inDate: {inDate} / 파라미터 수: {param.Count}");
                bro = await BackendCallAsync(callback => Backend.GameData.UpdateV2(tableName, inDate, Backend.UserInDate, param, callback));
            }
            else
            {
                Log($"[{tableName}] 삽입 요청 (신규 데이터). 파라미터 수: {param.Count}");
                bro = await BackendCallAsync(callback => Backend.GameData.Insert(tableName, param, callback));

                if (bro.IsSuccess())
                {
                    string newInDate = bro.GetInDate();
                    m_tableInDate[tableName] = newInDate;
                    Log($"[{tableName}] 삽입 성공. 새로운 inDate: {newInDate}");
                }
            }

            if (!bro.IsSuccess())
            {
                LogError("GameData", bro);
                throw new Exception($"데이터 업로드 실패 ({tableName}): {bro.GetMessage()}");
            }

            Log($"{tableName} 업로드(Update/Insert) 완료");
        }

        /// <summary>
        /// [설명]: 주어진 테이블 이름에 해당하는 데이터를 다운로드하고 캐시합니다.
        /// </summary>
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
                        LogWarning($"[{tableName}] 중복된 데이터 행 발견! (Count: {gameDataJson.Count}) - 최신 행을 사용해야 합니다.");
                    }

                    var row = gameDataJson[0];
                    m_tableInDate[tableName] = row["inDate"].ToString();
                    Log($"{tableName} 다운로드 완료 (inDate: {m_tableInDate[tableName]})");
                    return row;
                }

                Log($"{tableName} 데이터 없음");
                return null;
            }

            LogError("GameData", bro);
            throw new Exception($"데이터 다운로드 실패 ({tableName}): {bro.GetMessage()}");
        }

        /// <summary>
        /// [설명]: 특정 테이블의 캐시된 inDate를 반환합니다.
        /// </summary>
        public string GetCachedInDate(string tableName)
        {
            return m_tableInDate.TryGetValue(tableName, out string inDate) ? inDate : null;
        }

        #endregion
    }
}