using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.ScriptableObjects;
using InGame.Services;

namespace InGame.Lobby
{
    /// <summary>
    /// [설명]: 인벤토리 시스템의 수명 주기를 관리하고 전역 접근을 제공하는 매니저 클래스입니다.
    /// 기존 InventoryDataManager를 대체하며, 순수 로직(InventorySystem)과 데이터(ScriptableObject)를 연결합니다.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        #region 에디터 설정

        [Header("데이터 참조")]
        [SerializeField] private ItemDatabaseSO m_itemDatabase;
        [SerializeField] private InventoryDataSO m_inventoryData;

        #endregion

        #region 프로퍼티

        public static InventoryManager Instance { get; private set; }

        public InventorySystem System { get; private set; }
        public ItemDatabaseSO ItemDatabase => m_itemDatabase;

        private InventoryPersistence m_persistence;

        #endregion

        #region 유니티 생명주기

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Init(IGameDataService gameDataService)
        {
            InitializeService(gameDataService);
        }

        #endregion

        #region 초기화

        private void InitializeService(IGameDataService gameDataService)
        {
            // 1. 아이템 데이터베이스 초기화
            if (m_itemDatabase != null)
            {
                m_itemDatabase.Initialize();
            }
            else
            {
                LogManager.LogError("[InventoryManager] ItemDatabaseSO가 연결되지 않았습니다.");
            }

            // 2. 인벤토리 데이터 SO 확인
            if (m_inventoryData == null)
            {
                m_inventoryData = ScriptableObject.CreateInstance<InventoryDataSO>();
                LogManager.LogWarning("[InventoryManager] InventoryDataSO가 없어 임시 생성합니다.");
            }

            // 3. 영속성 모듈 초기화
            var encryptionService = new InGame.Services.EncryptionService();
            m_persistence = new InventoryPersistence(encryptionService, gameDataService);

            // 4. 시스템(POCO) 생성 및 주입
            System = new InventorySystem(m_inventoryData);

            // 5. 초기 데이터 로드 (서버/로컬) - 비동기 실행
            LoadDataFlowAsync().Forget();
            
            LogManager.Log("[InventoryManager] 서비스 초기화 완료", LogManager.LogCategory.System);
        }

        private async UniTaskVoid LoadDataFlowAsync()
        {
            if (m_persistence == null) return;

            // 서버 로드 -> 실패 시 로컬 로드 -> 실패 시 초기화
            bool success = await m_persistence.DownloadFromServerAsync(m_inventoryData);
            if (!success)
            {
                if (!m_persistence.LoadLocal(m_inventoryData))
                {
                    // 데이터 없음, 초기 상태 유지
                }
            }

            // Dictionary 재구축
            System.RebuildDictionary();
        }

        #endregion

        #region 데이터 영속성 (Save/Load)

        /// <summary>
        /// 현재 인벤토리 상태를 로컬에 저장합니다.
        /// </summary>
        public void SaveInventory()
        {
            if (m_persistence != null && m_inventoryData != null)
            {
                m_persistence.SaveLocal(m_inventoryData);
            }
        }

        /// <summary>
        /// 현재 인벤토리 상태를 서버에 업로드합니다.
        /// </summary>
        public async UniTask UploadInventoryAsync()
        {
            if (m_persistence != null && m_inventoryData != null)
            {
                await m_persistence.UploadToServerAsync(m_inventoryData);
            }
        }

        #endregion

        #region 인게임 세션 데이터 (임시)

        /// <summary>
        /// [설명]: 인게임 세션 중에만 사용되는 임시 인벤토리 리스트입니다.
        /// </summary>
        public System.Collections.Generic.List<SkillData> InGameAcquiredSkills { get; private set; } = new System.Collections.Generic.List<SkillData>();

        /// <summary>
        /// [설명]: 인게임 세션 중 획득한 스킬 데이터를 추가합니다.
        /// </summary>
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

        #region 유틸리티

        /// <summary>
        /// 아이템 코드로 아이템 정보를 조회합니다. (Database 위임)
        /// </summary>
        public ItemDataSO GetItemInfo(int itemCode)
        {
            if (m_itemDatabase == null) return null;
            return m_itemDatabase.GetItemData(itemCode);
        }

        #endregion
    }
}
