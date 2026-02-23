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

        /// <summary>
        /// 아이템 판매를 처리하고 획득할 재화 정보를 반환합니다.
        /// 실질적인 재화(Gold/Diamond) 가산은 반환값을 이용해 외부(ViewModel/Manager)에서 처리해야 합니다.
        /// </summary>
        /// <param name="itemCode">판매할 아이템 코드</param>
        /// <param name="count">판매 수량</param>
        /// <returns>
        /// success: 판매 성공 여부
        /// currencyType: 획득할 재화 타입 (currency1: 골드, currency2: 다이아)
        /// totalAmount: 획득할 재화 총량
        /// </returns>
        public (bool success, string currencyType, int totalAmount) SellItem(int itemCode, int count)
        {
            if (count <= 0) return (false, null, 0);

            if (m_entryDict.TryGetValue(itemCode, out var entry))
            {
                if (entry.count >= count)
                {
                    // 판매 가치 계산
                    int pricePerUnit = entry.item.itemcoinCount;
                    string currencyType = entry.item.itemcoinType;
                    int totalAmount = pricePerUnit * count;

                    // 아이템 차감
                    entry.count -= count;

                    // 로그 (시스템 내부 로깅)
                    // LogManager 의존성이 없으므로 생략하거나, 필요한 경우 외부에서 로깅

                    // 수량이 0이 되면 목록에서 제거하는 것이 깔끔하지만, 
                    // 기존 TryUseItem 정책(0개 유지)과 일관성을 위해 일단 유지할 수도 있음.
                    // 단, 판매의 경우 보통 슬롯을 비우기를 원하므로 여기서는 0이 되면 제거하는 로직을 추가 검토 가능.
                    // 현재는 차감만 수행.

                    return (true, currencyType, totalAmount);
                }
            }

            return (false, null, 0);
        }

        #endregion

        #region 공개 메서드 (조회)

        /// <summary>
        /// 현재 인벤토리의 모든 아이템 엔트리 목록을 반환합니다.
        /// (UI 표시용)
        /// </summary>
        public List<InventoryDataSO.InventoryEntry> GetAllEntries()
        {
            return m_inventoryData != null ? m_inventoryData.inventory : new List<InventoryDataSO.InventoryEntry>();
        }

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
