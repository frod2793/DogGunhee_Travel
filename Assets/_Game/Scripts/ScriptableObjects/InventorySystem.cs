using System.Collections.Generic;
using UnityEngine;

namespace InGame.Lobby
{
    /// <summary>
    /// 인벤토리 비즈니스 로직을 담당하는 POCO 시스템입니다.
    /// 아이템 추가, 제거, 검색 등의 핵심 로직을 처리하며 순수 C#으로 작성되었습니다.
    /// </summary>
    public class InventorySystem
    {
        #region 내부 상태

        // 빠른 검색을 위한 Dictionary (ItemCode -> InventoryEntry)
        private readonly Dictionary<int, InventoryDataSO.InventoryEntry> m_entryDict;
        
        // 원본 데이터 참조 (직렬화용)
        private readonly InventoryDataSO m_inventoryData;

        #endregion

        #region 생성자

        /// <summary>
        /// InventorySystem을 초기화합니다.
        /// </summary>
        /// <param name="data">관리할 인벤토리 데이터 SO</param>
        public InventorySystem(InventoryDataSO data)
        {
            if (data == null)
            {
                Debug.LogError("[InventorySystem] 전달된 데이터가 null입니다.");
                return;
            }

            m_inventoryData = data;
            m_entryDict = new Dictionary<int, InventoryDataSO.InventoryEntry>();

            // 초기 데이터 동기화
            RebuildDictionary();
        }

        #endregion

        #region 공개 메서드 (조작)

        /// <summary>
        /// 아이템을 인벤토리에 추가합니다.
        /// </summary>
        public void AddItem(ItemDataSO item, int count = 1)
        {
            if (item == null || count <= 0) return;

            if (m_entryDict.TryGetValue(item.itemCode, out var entry))
            {
                // 이미 존재하는 아이템이면 수량 증가
                entry.count += count;
            }
            else
            {
                // 새로운 아이템 생성
                var newEntry = new InventoryDataSO.InventoryEntry { item = item, count = count };
                
                // 리스트와 딕셔너리 양쪽에 추가 (데이터 동기화)
                m_inventoryData.inventory.Add(newEntry);
                m_entryDict[item.itemCode] = newEntry;
            }
        }

        /// <summary>
        /// 아이템 사용 시도 (수량 차감)
        /// </summary>
        /// <returns>사용 성공 여부</returns>
        public bool TryUseItem(int itemCode, int count = 1)
        {
            if (count <= 0) return false;

            if (m_entryDict.TryGetValue(itemCode, out var entry))
            {
                if (entry.count >= count)
                {
                    entry.count -= count;
                    
                    // 수량이 0이 되어도 목록에서 제거하지 않음 (일반적인 게임 인벤토리 정책)
                    // 필요 시 제거 로직 추가 가능
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 인벤토리를 초기화합니다.
        /// </summary>
        public void Clear()
        {
            m_inventoryData.inventory.Clear();
            m_entryDict.Clear();
        }

        #endregion

        #region 공개 메서드 (조회)

        /// <summary>
        /// 특정 아이템의 보유 수량을 반환합니다.
        /// </summary>
        public int GetItemCount(int itemCode)
        {
            if (m_entryDict.TryGetValue(itemCode, out var entry))
            {
                return entry.count;
            }
            return 0;
        }

        /// <summary>
        /// 아이템 보유 여부를 확인합니다.
        /// </summary>
        public bool HasItem(int itemCode, int count = 1)
        {
            return GetItemCount(itemCode) >= count;
        }

        /// <summary>
        /// 외부에서 데이터가 변경되었을 때 딕셔너리를 재구축합니다. (동기화)
        /// 로드 직후나 리셋 시 호출 필요.
        /// </summary>
        public void RebuildDictionary()
        {
            m_entryDict.Clear();
            if (m_inventoryData.inventory == null) return;

            foreach (var entry in m_inventoryData.inventory)
            {
                if (entry?.item != null)
                {
                    if (m_entryDict.ContainsKey(entry.item.itemCode))
                    {
                        Debug.LogWarning($"[InventorySystem] 중복된 아이템 코드가 발견되었습니다: {entry.item.itemCode}");
                        continue;
                    }
                    m_entryDict[entry.item.itemCode] = entry;
                }
            }
        }

        #endregion
    }
}
