using InGame.Data;

namespace InGame.Core.Interfaces
{
    /// <summary>
    /// [설명]: 킬Count, 웨이브 번호, 플레이어 레벨 등 게임 진행 중에 발생하는 각종 데이터에 대한 읽기 권한을 제공합니다.
    /// </summary>
    public interface IGameDataProvider
    {
        PlayerDataDTO PlayerData { get; }
        int GetMobKillCount();
        int GetCurrentWave();
        int GetCurrentStageId();
        float GetPlayerLevel();
        float GetPlayerExpProgress();
        int GetCoinCount();
    }
}
