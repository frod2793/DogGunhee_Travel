using Cysharp.Threading.Tasks;
using LitJson;
using BackEnd;

namespace InGame.Services
{
    /// <summary>
    /// [설명]: 게임 데이터(Table) 저장/로드를 담당하는 서비스 인터페이스입니다.
    /// </summary>
    public interface IGameDataService
    {
        /// <summary>
        /// [설명]: 지정된 테이블에 데이터를 업로드(Insert 또는 Update)합니다.
        /// </summary>
        UniTask UploadDataAsync(string tableName, Param param);

        /// <summary>
        /// [설명]: 지정된 테이블에서 데이터를 다운로드합니다.
        /// </summary>
        UniTask<JsonData> DownloadDataAsync(string tableName);
        
    }
}