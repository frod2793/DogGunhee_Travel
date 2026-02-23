using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InGame.ScriptableObjects
{
    /// <summary>
    /// [설명]: 게임 내 모든 아이템의 정적 데이터(변하지 않는 정보)를 관리하는 데이터베이스입니다.
    /// JSON 파일로부터 데이터를 로드하여 캐싱하고, 아이템 코드를 통해 조회 기능을 제공합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabaseSO", menuName = "GameData/ItemDatabaseSO")]
    public class ItemDatabaseSO : ScriptableObject
    {
        #region 에디터 설정

        [Header("데이터 소스")]
        [SerializeField, Tooltip("아이템 정보가 담긴 JSON 파일")]
        private TextAsset m_itemDataJsonFile;

        [Header("디버깅")]
        [SerializeField, Tooltip("데이터 로드 여부 (읽기 전용)")]
        private bool m_isInitialized;

        #endregion

        #region 내부 변수

        /// <summary> [설명]: 아이템 코드(Key)와 아이템 데이터(Value)를 매핑한 캐시입니다. </summary>
        private readonly Dictionary<int, ItemDataSO> m_itemDataCache = new Dictionary<int, ItemDataSO>();

        #endregion

        #region 초기화 로직

        /// <summary>
        /// [설명]: JSON 파일로부터 아이템 데이터를 파싱하여 캐시를 초기화합니다.
        /// 게임 시작 시 또는 데이터 리로드 시 호출해야 합니다.
        /// </summary>
        public void Initialize()
        {
            if (m_itemDataJsonFile == null)
            {
                LogManager.LogError("[ItemDatabaseSO] JSON 파일이 할당되지 않았습니다.", LogManager.LogCategory.System);
                return;
            }

            try
            {
                JsonItemDataList jsonItemsWrapper = JsonUtility.FromJson<JsonItemDataList>(m_itemDataJsonFile.text);
                if (jsonItemsWrapper == null || jsonItemsWrapper.items == null)
                {
                    LogManager.LogError("[ItemDatabaseSO] JSON 파싱 실패 혹은 데이터가 비어있습니다.", LogManager.LogCategory.System);
                    return;
                }

                m_itemDataCache.Clear();

                foreach (var jsonItem in jsonItemsWrapper.items)
                {
                    if (jsonItem == null) continue;

                    // 런타임 SO 생성 (메모리 상에만 존재)
                    ItemDataSO itemData = ScriptableObject.CreateInstance<ItemDataSO>();
                    itemData.name = $"Item_{jsonItem.itemCode}"; // 디버깅 용이성을 위해 이름 설정
                    itemData.itemName = jsonItem.itemName;
                    itemData.itemCode = jsonItem.itemCode;
                    itemData.itemtype = jsonItem.itemtype;
                    itemData.itemCount = jsonItem.itemCount; // 기본 수량 (스택 가능 여부 등은 기획에 따라 다를 수 있음)
                    itemData.itemcoinType = jsonItem.itemcoinType;
                    itemData.itemcoinCount = jsonItem.itemcoinCount;

                    // 캐시에 추가
                    if (!m_itemDataCache.ContainsKey(itemData.itemCode))
                    {
                        m_itemDataCache.Add(itemData.itemCode, itemData);
                    }
                    else
                    {
                        LogManager.LogWarning($"[ItemDatabaseSO] 중복된 아이템 코드가 발견되었습니다: {itemData.itemCode}", LogManager.LogCategory.System);
                    }
                }

                m_isInitialized = true;
                LogManager.Log($"[ItemDatabaseSO] 아이템 데이터 로드 완료 (총 {m_itemDataCache.Count}개)", LogManager.LogCategory.System);
            }
            catch (Exception e)
            {
                LogManager.LogError($"[ItemDatabaseSO] 초기화 중 예외 발생: {e.Message}\n{e.StackTrace}", LogManager.LogCategory.System);
            }
        }

        #endregion

        #region 공개 조회 API

        /// <summary>
        /// [설명]: 아이템 코드로 아이템 데이터를 조회합니다.
        /// </summary>
        /// <param name="itemCode">조회할 아이템의 고유 코드</param>
        /// <returns>해당 아이템 데이터 (ItemDataSO), 없으면 null</returns>
        public ItemDataSO GetItemData(int itemCode)
        {
            EnsureInitialized();

            if (m_itemDataCache.TryGetValue(itemCode, out ItemDataSO itemData))
            {
                return itemData;
            }

            LogManager.LogWarning($"[ItemDatabaseSO] 존재하지 않는 아이템 코드 요청: {itemCode}", LogManager.LogCategory.System);
            return null;
        }

        /// <summary>
        /// [설명]: 아이템 이름으로 아이템 데이터를 조회합니다. (비권장, 가급적 코드로 조회하세요)
        /// </summary>
        public ItemDataSO GetItemDataByName(string itemName)
        {
            EnsureInitialized();

            foreach (var item in m_itemDataCache.Values)
            {
                if (item.itemName == itemName)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// [설명]: 데이터베이스가 초기화되지 않았다면 초기화를 시도합니다.
        /// </summary>
        private void EnsureInitialized()
        {
            if (!m_isInitialized)
            {
                Initialize();
            }
        }

        #endregion

        #region JSON 데이터 구조

        [Serializable]
        private class JsonItemDataList
        {
            public JsonItemData[] items;
        }

        [Serializable]
        private class JsonItemData
        {
            public string itemName;
            public int itemCode;
            public string itemtype;
            public int itemCount;
            public string itemcoinType;
            public int itemcoinCount;
        }

        #endregion
    }
}
