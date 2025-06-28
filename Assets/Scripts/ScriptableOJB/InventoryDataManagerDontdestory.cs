using System.Collections.Generic;
using System.IO;
using BackEnd;
using BackEnd.BackndNewtonsoft.Json;
using UnityEngine;
using System;
using System.Linq;

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
        private Item_Data _currentItemData;
        private Dictionary<int, Item_Data> _itemDataCache = new Dictionary<int, Item_Data>();
        private bool _isDataLoaded = false;

        #endregion

        #region 싱글톤 패턴

        private static InventoryDataManagerDontdestory _instance;
        private static readonly object LockObject = new object();

        public static InventoryDataManagerDontdestory Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (LockObject) // 스레드 안전성 보장
                    {
                        if (_instance == null)
                        {
                            _instance = FindAnyObjectByType<InventoryDataManagerDontdestory>();
                            if (_instance == null)
                            {
                                var container = new GameObject("InventoryDataManager");
                                _instance = container.AddComponent<InventoryDataManagerDontdestory>();
                                DontDestroyOnLoad(container);
                                Debug.Log("인벤토리 데이터 매니저 인스턴스가 생성되었습니다.");
                            }
                        }
                    }
                }

                return _instance;
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
        }

        private void Start()
        {
            LoadItemDataFromJson();

            if (!_isDataLoaded)
            {
                Debug.LogWarning("JSON에서 아이템 데이터를 불러오지 못했습니다.");
            }

            // 서버에서 인벤토리 데이터 로드
            LoadDataFromServer();
        }
        
        #endregion

        #region 초기화 및 데이터 로드

        /// <summary>
        /// 기본 인벤토리 데이터를 생성합니다.
        /// </summary>
        private void CreateDefaultInventory()
        {
            try
            {
                Debug.Log("새 인벤토리 데이터를 생성합니다.");
                
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
                    Debug.Log($"기본 아이템 추가: {defaultItem.itemName} (코드: {defaultItem.itemCode})");
                }
                
                // 인벤토리 JSON 문자열 생성
                inventorydataString = JsonConvert.SerializeObject(scritpableobjInventoryData, Formatting.Indented);
                
                // 로컬에 저장
                SaveInventoryData();
                
                // 서버에 저장
                UploadDataToServer();
            }
            catch (Exception ex)
            {
                Debug.LogError($"기본 인벤토리 생성 중 오류 발생: {ex.Message}");
            }
        }

        /// <summary>
        /// JSON 파일에서 아이템 데이터를 로드합니다.
        /// </summary>
        private void LoadItemDataFromJson()
        {
            if (itemDataJsonFile == null)
            {
                Debug.LogError("아이템 데이터 JSON 파일이 인스펙터에서 할당되지 않았습니다.");
                return;
            }

            try
            {
                JsonItemData[] jsonItems = JsonConvert.DeserializeObject<JsonItemData[]>(itemDataJsonFile.text);

                if (jsonItems == null || jsonItems.Length == 0)
                {
                    Debug.LogWarning("JSON 데이터에서 아이템을 찾을 수 없습니다.");
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
                Debug.Log($"JSON 데이터에서 {_itemDataCache.Count}개의 아이템을 성공적으로 로드했습니다.");
            }
            catch (Exception e)
            {
                Debug.LogError($"JSON 데이터 파싱 오류: {e.Message}");
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
                Debug.LogWarning("아이템 데이터가 아직 로드되지 않았습니다.");
                return null;
            }

            if (_itemDataCache.TryGetValue(itemCode, out Item_Data item))
            {
                _currentItemData = item;
                return item;
            }

            Debug.LogWarning($"코드 {itemCode}를 가진 아이템을 찾을 수 없습니다.");
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
                Debug.LogWarning("업데이트할 인벤토리 데이터가 null입니다.");
                return;
            }
            
            scritpableobjInventoryData = inventoryData;
            
            // 직렬화된 JSON 문자열 생성 (디버깅용)
            try
            {
                inventorydataString = JsonConvert.SerializeObject(scritpableobjInventoryData, Formatting.Indented);
                #if UNITY_EDITOR
                Debug.Log("<color=green>인벤토리 데이터 업데이트 완료</color>");
                    // joson데이터 로그 출력 
                Debug.Log($"인벤토리 데이터: {inventorydataString}");
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"인벤토리 데이터 직렬화 중 오류: {ex.Message}");
            }
            
            // 로컬에 자동 저장
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
                Debug.LogWarning("업데이트할 아이템 데이터가 null입니다.");
                return;
            }
            
            _currentItemData = itemData;
            
            // 캐시 업데이트
            if (_itemDataCache.ContainsKey(itemData.itemCode))
            {
                _itemDataCache[itemData.itemCode] = itemData;
            }
            else
            {
                _itemDataCache.Add(itemData.itemCode, itemData);
            }
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
                Debug.LogWarning("저장할 인벤토리 데이터가 null입니다.");
                return;
            }

            try
            {
                // 암호화 객체가 초기화되지 않았다면 초기화
                if (_encryption == null)
                    _encryption = new HybridEncryption();

                // 인벤토리 데이터를 JSON으로 변환
                string jsonData = JsonUtility.ToJson(scritpableobjInventoryData, true);

                // 암호화 키 확인
                string rsaPublicKey = PlayerDataManagerDontdesytoy.Instance.RsaPublicKey;
                if (string.IsNullOrEmpty(rsaPublicKey))
                {
                    Debug.LogError("RSA 공개키를 찾을 수 없습니다. 암호화 없이 저장합니다.");
                    File.WriteAllText(_localSavePath.Replace(".encrypted", ".json"), jsonData);
                    return;
                }

                // 데이터 암호화
                EncryptedPacket encryptedPacket = _encryption.Encrypt(jsonData, rsaPublicKey);

                // 바이트 배열을 Base64 문자열로 변환 (JSON 직렬화를 위해)
                SerializableEncryptedPacket serializablePacket = new SerializableEncryptedPacket
                {
                    EncryptedSessionKeyBase64 = Convert.ToBase64String(encryptedPacket.EncryptedSessionKey),
                    EncryptedDataBase64 = Convert.ToBase64String(encryptedPacket.EncryptedData)
                };

                // 암호화된 패킷을 JSON으로 변환하여 저장
                string packetJson = JsonUtility.ToJson(serializablePacket);
                File.WriteAllText(_localSavePath, packetJson);

                Debug.Log($"인벤토리 데이터를 암호화하여 {_localSavePath}에 저장했습니다.");
            }
            catch (Exception e)
            {
                Debug.LogError($"인벤토리 데이터 암호화 저장 실패: {e.Message}");
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
                    SerializableEncryptedPacket serializablePacket =
                        JsonUtility.FromJson<SerializableEncryptedPacket>(packetJson);

                    // Base64 문자열을 바이트 배열로 변환
                    EncryptedPacket encryptedPacket = new EncryptedPacket
                    {
                        EncryptedSessionKey = Convert.FromBase64String(serializablePacket.EncryptedSessionKeyBase64),
                        EncryptedData = Convert.FromBase64String(serializablePacket.EncryptedDataBase64)
                    };

                    // 복호화에 사용할 개인키 확인
                    string rsaPrivateKey = PlayerDataManagerDontdesytoy.Instance.RsaPrivateKey;
                    if (string.IsNullOrEmpty(rsaPrivateKey))
                    {
                        Debug.LogError("RSA 개인키를 찾을 수 없습니다. 데이터를 복호화할 수 없습니다.");
                        return;
                    }

                    // 데이터 복호화
                    string decryptedJson = _encryption.Decrypt(encryptedPacket, rsaPrivateKey);

                    // 복호화된 JSON을 Inventory_Data 객체로 변환
                    JsonUtility.FromJsonOverwrite(decryptedJson, scritpableobjInventoryData);

                    Debug.Log("암호화된 인벤토리 데이터를 성공적으로 로드했습니다.");
                }
                else
                {
                    Debug.Log("저장된 암호화 인벤토리 데이터 파일이 없습니다.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"인벤토리 데이터 복호화 중 오류 발생: {e.Message}");
            }
        }

        #endregion

        #region 서버 데이터 처리

        /// <summary>
        /// 서버에서 인벤토리 데이터를 가져옵니다.
        /// </summary>
        public void LoadDataFromServer()
        {
            ServerManager.Instance.DownloadData("Inventory_Data", (bro) =>
            {
                OnServerDataReceived(bro);
            });
        }

        private void OnServerDataReceived(BackendReturnObject bro)
        {
            if (!bro.IsSuccess())
            {
                Debug.LogError($"인벤토리 데이터 조회 실패: {bro}");
                if (bro.GetStatusCode() == "404")
                {
                    Debug.Log("서버에 인벤토리 데이터가 없어 새로 생성합니다.");
                    CreateDefaultInventory();
                }
                else
                {
                    // 서버 조회 실패 시 로컬 데이터 로드 시도
                    LoadEncryptedInventoryData();
                }
                return;
            }

            var gameDataJson = bro.FlattenRows();
            if (gameDataJson.Count <= 0)
            {
                Debug.LogWarning("서버에 인벤토리 데이터가 없습니다. 새로 생성합니다.");
                CreateDefaultInventory();
                return;
            }

            try
            {
                string inventoryJsonString = gameDataJson[0]["Inventory"].ToString();
                Inventory_Data serverData = JsonConvert.DeserializeObject<Inventory_Data>(inventoryJsonString);

                if (serverData != null)
                {
                    Update_Inventory_Data(serverData);
                    SaveInventoryData(); // 로컬에도 저장
                    Debug.Log("서버로부터 인벤토리 데이터를 성공적으로 로드하고 업데이트했습니다.");
                }
                else
                {
                    Debug.LogWarning("서버 인벤토리 데이터 파싱에 실패했습니다. 새 인벤토리를 생성합니다.");
                    CreateDefaultInventory();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"서버 인벤토리 데이터 처리 중 오류 발생: {ex.Message}");
                // 오류 발생 시 로컬 데이터 로드 또는 새 데이터 생성
                LoadEncryptedInventoryData();
            }
        }

        /// <summary>
        /// 현재 인벤토리 데이터를 서버에 업로드합니다.
        /// </summary>
        public void UploadDataToServer()
        {
            if (scritpableobjInventoryData == null) return;

            inventorydataString = JsonConvert.SerializeObject(scritpableobjInventoryData, Formatting.Indented);

            Param param = new Param();
            param.Add("Inventory", inventorydataString);

            ServerManager.Instance.UploadData("Inventory_Data", param, (bro) =>
            {
                if (bro.IsSuccess())
                {
                    Debug.Log("인벤토리 데이터를 서버에 성공적으로 업로드했습니다.");
                }
                else
                {
                    Debug.LogError($"인벤토리 데이터 업로드 실패: {bro}");
                }
            });
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
                Debug.Log($"인벤토리 데이터를 {savePath}에 내보냈습니다.");
            }
            catch (Exception e)
            {
                Debug.LogError($"인벤토리 데이터 내보내기 실패: {e.Message}");
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

