using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BackEnd;
using UnityEngine;
using UnityEngine.Serialization;
using Cysharp.Threading.Tasks;
using InGame.Services;

namespace InGame.Lobby
{
    /// <summary>
    /// [설명]: 인벤토리 데이터를 관리하고 서버/로컬 저장소와 동기화하는 매니저 클래스입니다.
    /// 핵심 비즈니스 로직은 InventorySystem(POCO)에, 데이터 영속성은 InventoryPersistence(POCO)에 위임합니다.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, null, null, "InventoryDataManagerDontdestory")]
    public class InventoryDataManager : MonoBehaviour
    {
        #region 에디터 설정

        [Header("데이터 소스")]
        [SerializeField, Tooltip("아이템 정보가 담긴 JSON 파일")]
        private TextAsset m_itemDataJsonFile;

        [SerializeField, Tooltip("런타임 인벤토리 데이터 ScriptableObject"), FormerlySerializedAs("scritpableobjInventoryData")]
        private InventoryDataSO m_scriptableobjInventoryData;

        #endregion

        #region 내부 변수

        /// <summary> [설명]: 인벤토리 핵심 로직을 담당하는 POCO 시스템 인스턴스입니다. </summary>
        private InventorySystem m_system;

        /// <summary> [설명]: 데이터 저장 및 로드(서버/로컬)를 담당하는 POCO 인스턴스입니다. </summary>
        private InventoryPersistence m_persistence;

        /// <summary> [설명]: 아이템 코드별 데이터 캐시입니다. </summary>
        private readonly Dictionary<int, ItemDataSO> m_itemDataCache = new Dictionary<int, ItemDataSO>();

        /// <summary> [설명]: 아이템 데이터 로드 완료 여부입니다. </summary>
        private bool m_isDataLoaded;

        #endregion

        #region 프로퍼티

        /// <summary>
        /// [설명]: 인게임 세션 중에만 사용되는 임시 인벤토리 리스트입니다.
        /// </summary>
        public List<SkillData> InGameAcquiredSkills { get; private set; }

        /// <summary>
        /// [설명]: 현재 관리 중인 인벤토리 데이터 SO 참조를 반환합니다.
        /// </summary>
        public InventoryDataSO InventoryData => m_scriptableobjInventoryData;

        #endregion

        #region 싱글톤

        private static InventoryDataManager s_instance;
        private static readonly object s_lockObject = new object();

        /// <summary>
        /// [설명]: InventoryDataManager의 전역 인스턴스입니다.
        /// </summary>
        public static InventoryDataManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    lock (s_lockObject)
                    {
                        if (s_instance == null)
                        {
                            s_instance = FindAnyObjectByType<InventoryDataManager>();
                            if (s_instance == null)
                            {
                                var container = new GameObject("InventoryDataManager");
                                s_instance = container.AddComponent<InventoryDataManager>();
                                DontDestroyOnLoad(container);
                            }
                        }
                    }
                }

                return s_instance;
            }
        }

        #endregion

        #region 유니티 생명주기

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);

            Init();
        }

        private void Start()
        {
            LoadItemDataFromJson();

            // 데이터 로드 흐름 시작
            InitializeDataFlowAsync().Forget();
        }

        #endregion

        #region 초기화 및 데이터 흐름

        /// <summary>
        /// [설명]: 인벤토리 시스템과 영속성 모듈을 초기화합니다.
        /// </summary>
        private void Init()
        {
            // ScriptableObject 확인 및 생성
            if (m_scriptableobjInventoryData == null)
            {
                m_scriptableobjInventoryData = ScriptableObject.CreateInstance<InventoryDataSO>();
            }

            // POCO 클래스 인스턴스화 (의존성 주입)
            var encryptionService = new EncryptionService();
            m_persistence = new InventoryPersistence(encryptionService);
            m_system = new InventorySystem(m_scriptableobjInventoryData);

            InGameAcquiredSkills = new List<SkillData>();
        }

        /// <summary>
        /// [설명]: 서버 및 로컬 단계별 데이터 로딩 시퀀스를 실행합니다.
        /// </summary>
        private async UniTaskVoid InitializeDataFlowAsync()
        {
            if (m_persistence == null || m_scriptableobjInventoryData == null)
            {
                return;
            }

            // 1. 서버 데이터 다운로드 시도
            bool serverSuccess = await m_persistence.DownloadFromServerAsync(m_scriptableobjInventoryData);

            if (!serverSuccess)
            {
                // 2. 실패 시 로컬 파일 로드 시도
                bool localSuccess = m_persistence.LoadLocal(m_scriptableobjInventoryData);

                if (!localSuccess)
                {
                    // 3. 데이터가 아예 없으면 기본 인벤토리 생성
                    await CreateDefaultInventoryAsync();
                }
            }

            // 시스템 동기화 (Dictionary 재구축)
            m_system.RebuildDictionary();

            // 초기 상태 저장
            SaveInventoryData();
        }

        /// <summary>
        /// [설명]: 초기 플레이어를 위한 기본 아이템 인벤토리를 구성합니다.
        /// </summary>
        private async UniTask CreateDefaultInventoryAsync()
        {
            m_system.Clear();

            // 기본 아이템 지급 처리
            if (m_isDataLoaded && m_itemDataCache.Count > 0)
            {
                var defaultItemCode = m_itemDataCache.Keys.First();
                if (m_itemDataCache.TryGetValue(defaultItemCode, out var item))
                {
                    if (item != null)
                    {
                        m_system.AddItem(item);
                        LogManager.Log($"[Init] 기본 아이템 지급: {item.itemName}");
                    }
                }
            }

            // 초기 데이터 저장 및 업로드
            SaveInventoryData();
            await UploadDataToServerAsync();
        }

        #endregion

        #region 공개 API

        /// <summary>
        /// [설명]: 특정 아이템 코드로 아이템 정보를 조회하고 인벤토리에 추가합니다.
        /// </summary>
        /// <param name="itemCode">조회할 아이템 고유 코드</param>
        /// <returns>조회된 아이템 데이터 정보</returns>
        public ItemDataSO GetItemByItemCode(int itemCode)
        {
            if (!m_isDataLoaded)
            {
                return null;
            }

            if (m_itemDataCache.TryGetValue(itemCode, out ItemDataSO item))
            {
                if (item != null)
                {
                    m_system.AddItem(item);
                    SaveInventoryData();
                }

                return item;
            }

            return null;
        }

        /// <summary>
        /// [설명]: 아이템 이름과 수량을 지정하여 인벤토리에 추가합니다.
        /// </summary>
        /// <param name="itemName">아이템 이름</param>
        /// <param name="quantity">추가할 수량</param>
        public void GetItemByName(string itemName, int quantity)
        {
            if (!m_isDataLoaded)
            {
                return;
            }

            var foundItem = m_itemDataCache.Values.FirstOrDefault(item => item != null && item.itemName == itemName);
            if (foundItem != null)
            {
                // SO 복사본 생성하여 독립성 보장
                ItemDataSO itemToAdd = Instantiate(foundItem);

                m_system.AddItem(itemToAdd, quantity);
                SaveInventoryData();
            }
        }

        /// <summary>
        /// [설명]: 현재 인벤토리 상태를 로컬 저장소에 저장합니다.
        /// </summary>
        public void SaveInventoryData()
        {
            if (m_persistence != null && m_scriptableobjInventoryData != null)
            {
                m_persistence.SaveLocal(m_scriptableobjInventoryData);
            }
        }

        /// <summary>
        /// [설명]: 현재 인벤토리 데이터를 서버에 업로드합니다.
        /// </summary>
        public async UniTask UploadDataToServerAsync()
        {
            if (m_persistence != null && m_scriptableobjInventoryData != null)
            {
                await m_persistence.UploadToServerAsync(m_scriptableobjInventoryData);
            }
        }

        /// <summary>
        /// [설명]: 로컬에 저장된 암호화 데이터를 로드하고 시스템을 갱신합니다.
        /// </summary>
        public void LoadEncryptedInventoryData()
        {
            if (m_persistence != null && m_scriptableobjInventoryData != null)
            {
                if (m_persistence.LoadLocal(m_scriptableobjInventoryData))
                {
                    m_system.RebuildDictionary();
                }
            }
        }

        /// <summary>
        /// [설명]: 인게임 세션 중 획득한 스킬 데이터를 추가합니다.
        /// </summary>
        /// <param name="skillData">추가할 스킬 데이터</param>
        public void AddInGameSkill(SkillData skillData)
        {
            if (skillData != null)
            {
                InGameAcquiredSkills.Add(skillData);
            }
        }

        /// <summary>
        /// [설명]: 인게임에서 획득했던 스킬 리스트를 비웁니다.
        /// </summary>
        public void ClearInGameSkills()
        {
            InGameAcquiredSkills.Clear();
        }

        #endregion

        #region 내부 로직

        /// <summary>
        /// [설명]: 설정된 JSON 파일로부터 아이템 기본 정보를 파싱하여 캐시를 생성합니다.
        /// </summary>
        private void LoadItemDataFromJson()
        {
            if (m_itemDataJsonFile == null)
            {
                return;
            }

            try
            {
                JsonItemDataList jsonItemsWrapper = JsonUtility.FromJson<JsonItemDataList>(m_itemDataJsonFile.text);
                if (jsonItemsWrapper == null || jsonItemsWrapper.items == null)
                {
                    return;
                }

                m_itemDataCache.Clear();
                foreach (var jsonItem in jsonItemsWrapper.items)
                {
                    if (jsonItem == null)
                    {
                        continue;
                    }

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

        #region 내부 클래스 및 구조체

        /// <summary>
        /// [설명]: JSON 아이템 리스트 파싱을 위한 래퍼 클래스입니다.
        /// </summary>
        [Serializable]
        private class JsonItemDataList
        {
            public JsonItemData[] items;
        }

        /// <summary>
        /// [설명]: JSON 파일 내의 단일 아이템 데이터 구조입니다.
        /// </summary>
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
