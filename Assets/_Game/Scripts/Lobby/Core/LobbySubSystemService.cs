using InGame.Lobby;
using InGame.UI.Popups;

namespace Lobby.Core
{
    /// <summary>
    /// [설명]: 우편, 퀘스트, 상점 등 로비의 서브 시스템들을 통합 관리하고 동작을 수행하는 구체 클래스입니다.
    /// </summary>
    public class LobbySubSystemService : ILobbySubSystemService
    {
        private readonly PostView m_postManager;
        private readonly QuestInfoView m_questPanelManager;
        private readonly StoreView m_storeManager;
        private readonly InventoryView m_inventoryManager;

        public LobbySubSystemService(
            PostView postManager,
            QuestInfoView questPanelManager,
            StoreView storeManager,
            InventoryView inventoryManager)
        {
            m_postManager = postManager;
            m_questPanelManager = questPanelManager;
            m_storeManager = storeManager;
            m_inventoryManager = inventoryManager;
        }

        public void OpenPostBox()
        {
            if (m_postManager != null) m_postManager.OpenPostBoxPanel();
        }

        public void GetPostReward()
        {
            if (m_postManager != null) m_postManager.OnClickDetailRewardBtn();
        }

        public void OpenQuestPanel()
        {
            if (m_questPanelManager != null) m_questPanelManager.OpenQuestPanel();
        }

        public void OpenStore()
        {
            if (m_storeManager != null) m_storeManager.OpenStorePanel();
        }

        public void OpenInventory()
        {
            if (m_inventoryManager != null)
            {
                UnityEngine.Debug.Log("[LobbySubSystemService] Opening Inventory Panel");
                m_inventoryManager.OpenInventoryPanel();
            }
            else
            {
                UnityEngine.Debug.LogError("[LobbySubSystemService] m_inventoryManager is NULL!");
            }
        }
    }
}
