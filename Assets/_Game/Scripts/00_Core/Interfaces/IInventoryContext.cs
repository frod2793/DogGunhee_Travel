using System.Collections.Generic;
using InGame.Lobby;

namespace InGame.Core.Interfaces
{
    /// <summary>
    /// [설명]: 인게임 세션 중에 획득한 스킬 및 인벤토리 정보를 관리하는 인터페이스입니다.
    /// </summary>
    public interface IInventoryContext
    {
        IReadOnlyList<SkillData> InGameAcquiredSkills { get; }
        InGame.Lobby.InventorySystem System { get; }
        void AddInGameSkill(SkillData skillData);
        void ClearInGameSkills();
        InGame.ItemDataSO GetItemInfo(int itemCode);
        InGame.ItemDataSO GetItemDataByName(string itemName);
        void SaveInventory();
        Cysharp.Threading.Tasks.UniTask UploadInventoryAsync();
    }
}
