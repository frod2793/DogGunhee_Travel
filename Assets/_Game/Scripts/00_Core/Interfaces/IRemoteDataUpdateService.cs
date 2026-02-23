using System.Threading;
using Cysharp.Threading.Tasks;

namespace InGame.Data.Managers
{
    /// <summary>
    /// [설명]: 리모트 데이터 동기화를 담당하는 서비스 인터페이스입니다.
    /// </summary>
    public interface IRemoteDataUpdateService
    {
        UniTask UpdateAllRemoteDataAsync(SkillDatabase skillDb = null, StageDatabase stageDb = null, CancellationToken ct = default, bool force = false);
    }
}
