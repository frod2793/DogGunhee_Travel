using System.Collections.Generic;
using UnityEngine;

namespace InGame
{
    [CreateAssetMenu(fileName = "InventoryDataSO", menuName = "GameData/InventoryDataSO")]
    public class InventoryDataSO : ScriptableObject
    {

        public List<InventoryEntry> inventory = new List<InventoryEntry>(); // 인벤토리

        [System.Serializable]
        public class InventoryEntry
        {
            public ItemDataSO item; // 아이템 데이터
            public int count; // 아이템 개수
        }

        /// <summary>
        /// 인벤토리에 아이템 추가
        /// </summary>
        /// <param name="item"></param>
        public void AddItem(ItemDataSO item)
        {
            InventoryEntry existingEntry = inventory.Find(entry => entry.item.itemCode == item.itemCode);
            if (existingEntry != null)
            {
                existingEntry.count++;
            }
            else
            {
                inventory.Add(new InventoryEntry { item = item, count = 1 });
            }
        }

        /// <summary>
        /// 인벤토리 초기화 
        /// </summary>
        public void InitInventory()
        {
            inventory.Clear();
        }
    }
}