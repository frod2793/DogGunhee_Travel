using System.Collections.Generic;
using System.IO;
using BackEnd;
using UnityEngine;
using System;
using System.Linq;
using UnityEngine.Serialization;
using Cysharp.Threading.Tasks;

namespace InGame.Lobby
{
    /// <summary>
    /// 인벤토리 매니저 (구 InventoryDataManagerDontdestory)
    /// Core Logic -> InventorySystem (POCO)
    /// IO/Data -> InventoryPersistence (POCO)
    /// </summary>
    public class InventoryDataManagerDontdestory : MonoBehaviour
    {
        #region 필드 및 변수

        [Header("데이터 소스")]
        [Tooltip("아이템 정보가 담긴 JSON 파일")]
        [SerializeField] private TextAsset m_itemDataJsonFile;
        [Tooltip("런타임 인벤토리 데이터 SO")]
        [FormerlySerializedAs("scritpableobjInventoryData")]
        [SerializeField] private InventoryDataSO m_scriptableobjInventoryData;

        // POCO 시스템 위임
        private InventorySystem m_system;
        private InventoryPersistence m_persistence;

        // 아이템 데이터 캐시 (원본 정보)
        private readonly Dictionary<int, ItemDataSO> m_itemDataCache = new Dictionary<int, ItemDataSO>();
        private bool m_isDataLoaded;

        /// <summary>
        /// 인게임 세션 중에만 사용되는 임시 인벤토리 (변경 없음)
        /// </summary>
        public List<SkillData> InGameAcquiredSkills { get; private set; }

        public InventoryDataSO InventoryData => m_scriptableobjInventoryData;

        #endregion

        #region 싱글톤 패턴

        private static InventoryDataManagerDontdestory s_instance;
        private static readonly object s_lockObject = new object();

        public static InventoryDataManagerDontdestory Instance
        {
            get
            {
                if (s_instance == null)
                {
                    lock (s_lockObject)
                    {
                        if (s_instance == null)
                        {
                            s_instance = FindAnyObjectByType<InventoryDataManagerDontdestory>();
                            if (s_instance == null)
                            {
                                var container = new GameObject("InventoryDataManager");
                                s_instance = container.AddComponent<InventoryDataManagerDontdestory>();
                                DontDestroyOnLoad(container);
                            }
                        }
                    }
                }
                return s_instance;
            }
        }

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        private void Start()
        {
            LoadItemDataFromJson();
            
            // 데이터 로드 흐름 시작
            InitializeDataFlowAsync().Forget();
        }

        private void Initialize()
        {
            // SO 확인 및 생성
            if (m_scriptableobjInventoryData == null)
                m_scriptableobjInventoryData = ScriptableObject.CreateInstance<InventoryDataSO>();

            // POCO 시스템 초기화
            m_persistence = new InventoryPersistence();
            m_system = new InventorySystem(m_scriptableobjInventoryData);

            InGameAcquiredSkills = new List<SkillData>();
        }

        #endregion

        #region 초기화 Flow

        private async UniTaskVoid InitializeDataFlowAsync()
        {
            // 1. 서버 데이터 시도
            bool serverSuccess = await m_persistence.DownloadFromServerAsync(m_scriptableobjInventoryData);
            
            if (!serverSuccess)
            {
                // 2. 실패 시 로컬 로드 시도
                bool localSuccess = m_persistence.LoadLocal(m_scriptableobjInventoryData);
                
                if (!localSuccess)
                {
                    // 3. 둘 다 없으면 기본 생성
                    await CreateDefaultInventoryAsync();
                }
            }
            
            // 데이터 로드 후 시스템 동기화 (Dictionary 재구축)
            m_system.RebuildDictionary();
            
            // 저장 (최신 상태 동기화)
            SaveInventoryData(); 
        }

        private async UniTask CreateDefaultInventoryAsync()
        {
            // 인벤토리 초기화
            m_system.Clear();

            // 기본 아이템 지급 예시
            if (m_isDataLoaded && m_itemDataCache.Count > 0)
            {
                var defaultItemCode = m_itemDataCache.Keys.First();
                if (m_itemDataCache.TryGetValue(defaultItemCode, out var item))
                {
                    m_system.AddItem(item);
                    LogManager.Log($"[Init] 기본 아이템 지급: {item.itemName}");
                }
            }

            // 저장 및 업로드
            SaveInventoryData();
            await UploadDataToServerAsync();
        }

        #endregion

        #region 인벤토리 조작 Delegate

        public ItemDataSO GetItemByItemCode(int itemCode)
        {
            if (!m_isDataLoaded) return null;

            if (m_itemDataCache.TryGetValue(itemCode, out ItemDataSO item))
            {
                m_system.AddItem(item); // 획득 처리 위임
                SaveInventoryData();    // 자동 저장
                return item;
            }
            return null;
        }

        public void GetItemByName(string itemName, int quantity)
        {
            if (!m_isDataLoaded) return;

            var foundItem = m_itemDataCache.Values.FirstOrDefault(item => item.itemName == itemName);
            if (foundItem != null)
            {
                // SO 복사본 생성하여 독립성 보장 (기존 로직 유지)
                ItemDataSO itemToAdd = Instantiate(foundItem);
                
                m_system.AddItem(itemToAdd, quantity);
                SaveInventoryData();
            }
        }

        public void SaveInventoryData()
        {
            m_persistence?.SaveLocal(m_scriptableobjInventoryData);
        }

        public async UniTask UploadDataToServerAsync()
        {
            await m_persistence.UploadToServerAsync(m_scriptableobjInventoryData);
        }

        public void LoadEncryptedInventoryData()
        {
            if (m_persistence.LoadLocal(m_scriptableobjInventoryData))
            {
                m_system.RebuildDictionary();
            }
        }

        #endregion

        #region JSON 데이터 로드 (기존 유지)

        private void LoadItemDataFromJson()
        {
            if (m_itemDataJsonFile == null) return;

            try
            {
                JsonItemDataList jsonItemsWrapper = JsonUtility.FromJson<JsonItemDataList>(m_itemDataJsonFile.text);
                if (jsonItemsWrapper?.items == null) return;

                m_itemDataCache.Clear();
                foreach (var jsonItem in jsonItemsWrapper.items)
                {
                    ItemDataSO itemData = ScriptableObject.CreateInstance<ItemDataSO>();
                    itemData.itemName = jsonItem.itemName;
                    itemData.itemCode = jsonItem.itemCode;
                    itemData.itemtype = jsonItem.itemtype;
                    itemData.itemCount = jsonItem.itemCount;
                    itemData.itemcoinType = jsonItem.itemcoinType;
                    itemData.itemcoinCount = jsonItem.itemcoinCount;

                    m_itemDataCache[itemData.itemCode] = itemData;
                }
                m_isDataLoaded = true;
            }
            catch (Exception e)
            {
                LogManager.LogError($"JSON Parse Error: {e.Message}", LogManager.LogCategory.InventoryManager);
            }
        }

        #endregion

        #region 인게임 스킬 (기존 유지)

        public void AddInGameSkill(SkillData skillData)
        {
            if (skillData != null) InGameAcquiredSkills.Add(skillData);
        }

        public void ClearInGameSkills()
        {
            InGameAcquiredSkills.Clear();
        }

        #endregion

        #region Helper Classes
        
        [System.Serializable]
        private class JsonItemDataList
        {
            public JsonItemData[] items;
        }

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
