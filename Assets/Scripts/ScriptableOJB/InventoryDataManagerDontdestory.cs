using System.Collections.Generic;
using BackEnd.BackndNewtonsoft.Json;
using UnityEngine;
using System;


namespace DogGuns_Games.Lobby
{
    public class InventoryDataManagerDontdestory : MonoBehaviour
    {
        #region 필드 및 변수

        [SerializeField] private TextAsset itemDataJsonFile;
        public Inventory_Data scritpableobjInventoryData;
        public string inventorydataString;

        // Cache fields
        private Item_Data _scritpableobjItemData;
        private Dictionary<int, Item_Data> _itemDataCache = new Dictionary<int, Item_Data>();
        private bool _isDataLoaded = false;

        #endregion
        
        #region 싱글톤 할당
        
        private static InventoryDataManagerDontdestory instance;
        private static readonly object Lock = new object();

        public static InventoryDataManagerDontdestory Instance
        {
            get
            {
                lock (Lock) // 스레드 안전성 확보
                {
                    if (instance == null)
                    {
                        instance = FindFirstObjectByType<InventoryDataManagerDontdestory>();

                        if (instance == null)
                        {
                            GameObject obj = new GameObject("InventoryDataManagerDontdestory");
                            instance = obj.AddComponent<InventoryDataManagerDontdestory>();
                            DontDestroyOnLoad(obj);
                        }
                    }
                    return instance;
                }
            }
        }

        private void Awake()
        {
            lock (Lock)
            {
                if (instance == null)
                {
                    instance = this;
                    DontDestroyOnLoad(gameObject);
                }
                else if (instance != this)
                {
                    Destroy(gameObject); // 중복 제거
                }
            }
        }
        #endregion

        #region Unity 라이프사이클

        private void Start()
        {
            InitializeInventory();
            LoadItemDataFromJson();
            
            if (!_isDataLoaded)
                Debug.LogWarning("Failed to load item data from JSON");
                
            ServerManager.Instance.Get_Inventory_Data(ServerManager.Instance.InventoryDataInsert);
        }

        #endregion
        
        #region 초기화 및 데이터 로드
        
        private void InitializeInventory()
        {
            if (scritpableobjInventoryData == null)
                scritpableobjInventoryData = ScriptableObject.CreateInstance<Inventory_Data>();
        }

        private void LoadItemDataFromJson()
        {
            if (itemDataJsonFile == null)
            {
                Debug.LogError("Item data JSON file not assigned in inspector");
                return;
            }

            try
            {
                JsonItemData[] jsonItems = JsonConvert.DeserializeObject<JsonItemData[]>(itemDataJsonFile.text);
                
                if (jsonItems == null || jsonItems.Length == 0)
                {
                    Debug.LogWarning("No items found in JSON data");
                    return;
                }
                
                _itemDataCache.Clear();
                
                foreach (var jsonItem in jsonItems)
                {
                    Item_Data itemData = ScriptableObject.CreateInstance<Item_Data>();
                    itemData.itemName = jsonItem.itemName;
                    itemData.itemCode = jsonItem.itemCode;
                    itemData.itemtype = jsonItem.itemtype;
                    itemData.itemCount = jsonItem.itemCount;
                    itemData.itemcoinType = jsonItem.itemcoinType;
                    itemData.itemcoinCount = jsonItem.itemcoinCount;
                    
                    _itemDataCache[itemData.itemCode] = itemData;
                }
                
                _isDataLoaded = true;
                Debug.Log($"Successfully loaded {_itemDataCache.Count} items from JSON data");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing JSON data: {e.Message}");
            }
        }
        
        #endregion
        
        #region 인벤토리 및 아이템 관리
        
        public Item_Data GetItemByItemCode(int itemCode)
        {
            if (!_isDataLoaded)
            {
                Debug.LogWarning("Item data not loaded yet");
                return null;
            }
            
            if (_itemDataCache.TryGetValue(itemCode, out Item_Data item))
            {
                _scritpableobjItemData = item;
                return item;
            }
            
            Debug.LogWarning($"Item with code {itemCode} not found");
            return null;
        }

        public void Update_Inventory_Data(Inventory_Data inventoryData)
        {
            scritpableobjInventoryData = inventoryData;
            
            #if UNITY_EDITOR
            // Only generate debugging JSON in editor builds
            inventorydataString = JsonConvert.SerializeObject(scritpableobjInventoryData, Formatting.Indented);
            Debug.Log("<color=green>" + inventorydataString + "</color>");
            #endif
        }

        public void UpdateItemData(Item_Data itemData)
        {
            _scritpableobjItemData = itemData;
        }
        
        #endregion

        #region 데이터 저장

        public void SaveInventoryData()
        {
            if (scritpableobjInventoryData == null)
                return;
                
            try 
            {
                string savePath = System.IO.Path.Combine(Application.persistentDataPath, "inventoryData.json");
                string jsonData = JsonUtility.ToJson(scritpableobjInventoryData, true);
                System.IO.File.WriteAllText(savePath, jsonData);
                Debug.Log($"Saved Inventory data to {savePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save Inventory data: {e.Message}");
            }
        }

        public void SaveItemData()
        {
            if (_scritpableobjItemData == null)
                return;
                
            try
            {
                string savePath = System.IO.Path.Combine(Application.persistentDataPath, "itemData.json");
                string jsonData = JsonUtility.ToJson(_scritpableobjItemData, true);
                System.IO.File.WriteAllText(savePath, jsonData);
                Debug.Log($"Saved item data to {savePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save item data: {e.Message}");
            }
        }
        
        #endregion

        #region 헬퍼 클래스
        
        [System.Serializable]
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