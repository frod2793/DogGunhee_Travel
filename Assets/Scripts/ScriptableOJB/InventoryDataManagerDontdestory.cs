using System.Collections.Generic;
using System.IO;
using BackEnd;
using BackEnd.BackndNewtonsoft.Json;
using UnityEngine;
using System;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace DogGuns_Games.Lobby
{
    /// <summary>
    /// 인벤토리 데이터를 관리하고 암호화하여 저장/로드하는 매니저 클래스
    /// DontDestroyOnLoad로 씬 전환 시에도 유지됩니다.
    /// </summary>
    public class InventoryDataManagerDontdestory : MonoBehaviour
    {
        #region 필드 및 변수

        [SerializeField] private TextAsset itemDataJsonFile;
        public Inventory_Data scritpableobjInventoryData;
        public string inventorydataString;

        private HybridEncryption _encryption;
        private string _localSavePath;

        // 캐시 필드
        private Dictionary<int, Item_Data> _itemDataCache = new Dictionary<int, Item_Data>();
        private bool _isDataLoaded;
        
        /// <summary>
        /// 인게임 세션 중에만 사용되는 임시 인벤토리. 무기, 장신구 등을 저장합니다.
        /// </summary>
        public List<Item_Data> InGameAcquiredItems { get; private set; }

        #endregion

        #region 싱글톤 패턴

        private static InventoryDataManagerDontdestory instance;
        private static readonly object LockObject = new object();

        public static InventoryDataManagerDontdestory Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (LockObject) // 스레드 안전성 보장
                    {
                        if (instance == null)
                        {
                            instance = FindAnyObjectByType<InventoryDataManagerDontdestory>();
                            if (instance == null)
                            {
                                var container = new GameObject("InventoryDataManager");
                                instance = container.AddComponent<InventoryDataManagerDontdestory>();
                                DontDestroyOnLoad(container);
                                LogManager.Log("인벤토리 데이터 매니저 인스턴스가 생성되었습니다.", LogManager.LogCategory.InventoryManager);
                            }
                        }
                    }
                }

                return instance;
            }
        }

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            // 싱글톤 패턴 구현
            if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            
            // 초기화
            _encryption = new HybridEncryption();
            _localSavePath = Path.Combine(Application.persistentDataPath, "inventoryData.encrypted");
            
            // Inventory_Data 초기화
            if (scritpableobjInventoryData == null)
                scritpableobjInventoryData = ScriptableObject.CreateInstance<Inventory_Data>();
            
            // 인게임 인벤토리 초기화
            InGameAcquiredItems = new List<Item_Data>();
        }

        private void Start()
        {
            LoadItemDataFromJson();

            if (!_isDataLoaded)
            {
                LogManager.LogWarning("JSON에서 아이템 데이터를 불러오지 못했습니다.", LogManager.LogCategory.InventoryManager);
            }

            // 서버에서 인벤토리 데이터 로드
            LoadDataFromServerAsync().Forget();
        }
        
        #endregion

        #region 초기화 및 데이터 로드

        /// <summary>
        /// 기본 인벤토리 데이터를 생성합니다.
        /// </summary>
        private async UniTask CreateDefaultInventoryAsync()
        {
            try
            {
                LogManager.Log("새 인벤토리 데이터를 생성합니다.", LogManager.LogCategory.InventoryManager);
                
                // 인벤토리 데이터가 이미 초기화되어 있는지 확인
                if (scritpableobjInventoryData == null)
                {
                    scritpableobjInventoryData = ScriptableObject.CreateInstance<Inventory_Data>();
                }
                else
                {
                    scritpableobjInventoryData.InitInventory();
                }
                
                // 기본 아이템 추가 (필요한 경우)
                // 예: 시작용 기본 장비 추가
                if (_isDataLoaded && _itemDataCache.Count > 0)
                {
                    // 아이템 목록에서 첫 번째 아이템을 기본 아이템으로 추가 (예시)
                    var defaultItemCode = _itemDataCache.Keys.First();
                    var defaultItem = _itemDataCache[defaultItemCode];
                    
                    scritpableobjInventoryData.AddItem(defaultItem);
                    LogManager.Log($"기본 아이템 추가: {defaultItem.itemName} (코드: {defaultItem.itemCode})", LogManager.LogCategory.InventoryManager);
                }
                
                // 인벤토리 JSON 문자열 생성
                inventorydataString = JsonConvert.SerializeObject(scritpableobjInventoryData, Formatting.Indented);
                
                // 로컬에 저장
                SaveInventoryData();
                
                // 서버에 저장
                await UploadDataToServerAsync();
            }
            catch (Exception ex)
            {
                LogManager.LogError($"기본 인벤토리 생성 중 오류 발생: {ex.Message}", LogManager.LogCategory.InventoryManager);
            }
        }

        /// <summary>
        /// JSON 파일에서 아이템 데이터를 로드합니다.
        /// </summary>
        private void LoadItemDataFromJson()
        {
            if (itemDataJsonFile == null)
            {
                LogManager.LogError("아이템 데이터 JSON 파일이 인스펙터에서 할당되지 않았습니다.", LogManager.LogCategory.InventoryManager);
                return;
            }

            try
            {
                JsonItemData[] jsonItems = JsonConvert.DeserializeObject<JsonItemData[]>(itemDataJsonFile.text);

                if (jsonItems == null || jsonItems.Length == 0)
                {
                    LogManager.LogWarning("JSON 데이터에서 아이템을 찾을 수 없습니다.", LogManager.LogCategory.InventoryManager);
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
                LogManager.Log($"JSON 데이터에서 {_itemDataCache.Count}개의 아이템을 성공적으로 로드했습니다.", LogManager.LogCategory.InventoryManager);
            }
            catch (Exception e)
            {
                LogManager.LogError($"JSON 데이터 파싱 오류: {e.Message}", LogManager.LogCategory.InventoryManager);
            }
        }

        #endregion

        #region 인벤토리 및 아이템 관리

        /// <summary>
        /// 아이템 코드로 아이템 데이터를 가져옵니다.
        /// </summary>
        /// <param name="itemCode">찾을 아이템의 코드</param>
        /// <returns>아이템 데이터 또는 null</returns>
        public Item_Data GetItemByItemCode(int itemCode)
        {
            if (!_isDataLoaded)
            {
                LogManager.LogWarning("아이템 데이터가 아직 로드되지 않았습니다.", LogManager.LogCategory.InventoryManager);
                return null;
            }

            if (_itemDataCache.TryGetValue(itemCode, out Item_Data item))
            {
                return item;
            }

            LogManager.LogWarning($"코드 {itemCode}를 가진 아이템을 찾을 수 없습니다.", LogManager.LogCategory.InventoryManager);
            return null;
        }

        /// <summary>
        /// 인벤토리 데이터를 업데이트합니다.
        /// </summary>
        /// <param name="inventoryData">새로운 인벤토리 데이터</param>
        public void Update_Inventory_Data(Inventory_Data inventoryData)
        {
            if (inventoryData == null)
            {
                LogManager.LogWarning("업데이트할 인벤토리 데이터가 null입니다.", LogManager.LogCategory.InventoryManager);
                return;
            }
            scritpableobjInventoryData = inventoryData;
            try
            {
                inventorydataString = JsonConvert.SerializeObject(scritpableobjInventoryData, Formatting.Indented);
#if UNITY_EDITOR
                LogManager.Log("<color=green>인벤토리 데이터 업데이트 완료</color>", LogManager.LogCategory.InventoryManager);
                LogManager.Log($"인벤토리 데이터: {inventorydataString}", LogManager.LogCategory.InventoryManager);
#endif
            }
            catch (Exception ex)
            {
                LogManager.LogError($"인벤토리 데이터 직렬화 중 오류: {ex.Message}", LogManager.LogCategory.InventoryManager);
            }
            SaveInventoryData();
        }

        /// <summary>
        /// 현재 선택된 아이템 데이터를 업데이트합니다.
        /// </summary>
        /// <param name="itemData">업데이트할 아이템 데이터</param>
        public void UpdateItemData(Item_Data itemData)
        {
            if (itemData == null)
            {
                LogManager.LogWarning("업데이트할 아이템 데이터가 null입니다.", LogManager.LogCategory.InventoryManager);
                return;
            }
            _itemDataCache[itemData.itemCode] = itemData;
        }

        /// <summary>
        /// 인게임 세션용 인벤토리에 아이템을 추가합니다.
        /// </summary>
        /// <param name="itemData">추가할 아이템</param>
        public void AddInGameItem(Item_Data itemData)
        {
            if (itemData == null) return;
            InGameAcquiredItems.Add(itemData);
            LogManager.Log($"인게임 인벤토리에 아이템 추가: {itemData.itemName}", LogManager.LogCategory.InventoryManager);
        }

        /// <summary>
        /// 인게임 세션용 인벤토리를 초기화합니다.
        /// </summary>
        public void ClearInGameInventory()
        {
            InGameAcquiredItems.Clear();
            LogManager.Log("인게임 인벤토리가 초기화되었습니다.", LogManager.LogCategory.InventoryManager);
        }


        #endregion

        #region 데이터 저장 및 로드

        /// <summary>
        /// 인벤토리 데이터를 암호화하여 로컬에 저장합니다.
        /// </summary>
        public void SaveInventoryData()
        {
            if (scritpableobjInventoryData == null)
            {
                LogManager.LogWarning("저장할 인벤토리 데이터가 null입니다.", LogManager.LogCategory.InventoryManager);
                return;
            }
            try
            {
                if (_encryption == null)
                    _encryption = new HybridEncryption();
                string jsonData = JsonUtility.ToJson(scritpableobjInventoryData, true);
                string rsaPublicKey = PlayerDataManagerDontdesytoy.Instance.RsaPublicKey;
                if (string.IsNullOrEmpty(rsaPublicKey))
                {
                    LogManager.LogError("RSA 공개키를 찾을 수 없습니다. 암호화 없이 저장합니다.", LogManager.LogCategory.InventoryManager);
                    File.WriteAllText(_localSavePath.Replace(".encrypted", ".json"), jsonData);
                    return;
                }
                EncryptedPacket encryptedPacket = _encryption.Encrypt(jsonData, rsaPublicKey);
                string packetJson = JsonUtility.ToJson(encryptedPacket);
                File.WriteAllText(_localSavePath, packetJson);
                LogManager.Log($"인벤토리 데이터를 암호화하여 {_localSavePath}에 저장했습니다.", LogManager.LogCategory.InventoryManager);
            }
            catch (Exception e)
            {
                LogManager.LogError($"인벤토리 데이터 암호화 저장 실패: {e.Message}", LogManager.LogCategory.InventoryManager);
            }
        }

        /// <summary>
        /// 암호화된 인벤토리 데이터를 로드하고 복호화합니다.
        /// </summary>
        public void LoadEncryptedInventoryData()
        {
            try
            {
                // 암호화 객체가 초기화되지 않았다면 초기화
                if (_encryption == null)
                    _encryption = new HybridEncryption();

                if (File.Exists(_localSavePath))
                {
                    // 암호화된 데이터 로드
                    string packetJson = File.ReadAllText(_localSavePath);
                    EncryptedPacket encryptedPacket =
                        JsonUtility.FromJson<EncryptedPacket>(packetJson);

                    // 복호화에 사용할 개인키 확인
                    string rsaPrivateKey = PlayerDataManagerDontdesytoy.Instance.RsaPrivateKey;
                    if (string.IsNullOrEmpty(rsaPrivateKey))
                    {
                        LogManager.LogError("RSA 개인키를 찾을 수 없습니다. 데이터를 복호화할 수 없습니다.", LogManager.LogCategory.InventoryManager);
                        return;
                    }

                    // 데이터 복호화
                    string decryptedJson = _encryption.Decrypt(encryptedPacket, rsaPrivateKey);

                    // 복호화된 JSON을 Inventory_Data 객체로 변환
                    JsonUtility.FromJsonOverwrite(decryptedJson, scritpableobjInventoryData);

                    LogManager.Log("암호화된 인벤토리 데이터를 성공적으로 로드했습니다.", LogManager.LogCategory.InventoryManager);
                    LogManager.Log($"packetJson: {packetJson}", LogManager.LogCategory.InventoryManager);
                    LogManager.Log($"encryptedPacket.EncryptedSessionKey: {encryptedPacket.EncryptedSessionKeyBase64}", LogManager.LogCategory.InventoryManager);
                    LogManager.Log($"encryptedPacket.EncryptedData: {encryptedPacket.EncryptedDataBase64}", LogManager.LogCategory.InventoryManager);
                    LogManager.Log($"decryptedJson: {decryptedJson}", LogManager.LogCategory.InventoryManager);
                }
                else
                {
                    LogManager.Log("저장된 암호화 인벤토리 데이터 파일이 없습니다.", LogManager.LogCategory.InventoryManager);
                }
            }
            catch (Exception e)
            {
                LogManager.LogError($"인벤토리 데이터 복호화 중 오류 발생: {e.Message}", LogManager.LogCategory.InventoryManager);
            }
        }

        #endregion

        #region 서버 데이터 처리

        /// <summary>
        /// 서버에서 인벤토리 데이터를 가져옵니다.
        /// </summary>
        public async UniTask LoadDataFromServerAsync()
        {
            try
            {
                var serverDataJson = await ServerManager.Instance.DownloadDataAsync("Inventory_Data");

                // 서버에 데이터가 없는 경우
                if (serverDataJson == null)
                {
                    LogManager.Log("서버에 인벤토리 데이터가 없어 새로 생성합니다.", LogManager.LogCategory.InventoryManager);
                    await CreateDefaultInventoryAsync();
                    return;
                }

                // 서버 데이터 파싱 및 업데이트
                string inventoryJsonString = serverDataJson["Inventory"].ToString();
                Inventory_Data serverData = JsonConvert.DeserializeObject<Inventory_Data>(inventoryJsonString);

                if (serverData != null)
                {
                    Update_Inventory_Data(serverData);
                    SaveInventoryData(); // 로컬에도 저장
                    LogManager.Log("서버로부터 인벤토리 데이터를 성공적으로 로드하고 업데이트했습니다.", LogManager.LogCategory.InventoryManager);
                }
                else
                {
                    LogManager.LogWarning("서버 인벤토리 데이터 파싱에 실패했습니다. 새 인벤토리를 생성합니다.", LogManager.LogCategory.InventoryManager);
                    await CreateDefaultInventoryAsync();
                }
            }
            catch (Exception e)
            {
                LogManager.LogError($"인벤토리 데이터 조회 실패: {e.Message}", LogManager.LogCategory.InventoryManager);
                // 서버 조회 실패 시 로컬 데이터 로드 시도
                LoadEncryptedInventoryData();
            }
        }

        /// <summary>
        /// 현재 인벤토리 데이터를 서버에 업로드합니다.
        /// </summary>
        public async UniTask UploadDataToServerAsync()
        {
            if (scritpableobjInventoryData == null) return;

            inventorydataString = JsonConvert.SerializeObject(scritpableobjInventoryData, Formatting.Indented);

            Param param = new Param();
            param.Add("Inventory", inventorydataString);

            try
            {
                await ServerManager.Instance.UploadDataAsync("Inventory_Data", param);
                LogManager.Log("인벤토리 데이터를 서버에 성공적으로 업로드했습니다.", LogManager.LogCategory.InventoryManager);
            }
            catch (Exception e)
            {
                LogManager.LogError($"인벤토리 데이터 업로드 실패: {e.Message}", LogManager.LogCategory.InventoryManager);
            }
        }

        #endregion

        #region 유틸리티 메서드

        /// <summary>
        /// 인벤토리 데이터를 JSON 파일로 내보냅니다. (디버깅용)
        /// </summary>
        public void ExportInventoryDataToJson()
        {
            if (scritpableobjInventoryData == null)
                return;

            try
            {
                string savePath = Path.Combine(Application.persistentDataPath, "inventory_debug_export.json");
                string jsonData = JsonConvert.SerializeObject(scritpableobjInventoryData, Formatting.Indented);
                File.WriteAllText(savePath, jsonData);
                LogManager.Log($"인벤토리 데이터를 {savePath}에 내보냈습니다.", LogManager.LogCategory.InventoryManager);
            }
            catch (Exception e)
            {
                LogManager.LogError($"인벤토리 데이터 내보내기 실패: {e.Message}", LogManager.LogCategory.InventoryManager);
            }
        }

        /// <summary>
        /// 객체가 null인지 확인합니다.
        /// </summary>
        private static bool IsNullOrEmpty(UnityEngine.Object value)
        {
            return ReferenceEquals(value, null);
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
