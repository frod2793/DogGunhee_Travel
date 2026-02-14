using System;
using BackEnd;
using Cysharp.Threading.Tasks;
using InGame.Data;
using InGame.Services;
using LitJson;
using UnityEngine;

namespace InGame
{
    /// <summary>
    /// [설명]: 플레이어 데이터를 관리하고 서버와의 동기화를 담당하는 매니저 클래스입니다.
    /// </summary>
    public class PlayerDataManager : MonoBehaviour
    {
        #region 내부 필드 

        [Header("데이터")]
        [SerializeField] private PlayerData m_scriptableobjPlayerData;

        #endregion

        #region 프로퍼티 

        public PlayerData PlayerData
        {
            get
            {
                if (m_scriptableobjPlayerData == null)
                {
                    m_scriptableobjPlayerData = ScriptableObject.CreateInstance<PlayerData>();
                    LogManager.LogWarning("PlayerData ScriptableObject가 없어 새로 생성합니다.", LogManager.LogCategory.PlayerDataManager);
                }
                return m_scriptableobjPlayerData;
            }
        }

        public int SelectWeaponIndex
        {
            get => PlayerData.selelcWeaponIndex;
            set => PlayerData.selelcWeaponIndex = value;
        }

        public int SelectCharacterIndex
        {
            get => PlayerData.selectCharacterIndex;
            set => PlayerData.selectCharacterIndex = value;
        }

        #endregion

        #region 싱글톤 

        private static PlayerDataManager s_instance;
        private static readonly object s_lockObject = new object();

        public static PlayerDataManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    lock (s_lockObject)
                    {
                        if (s_instance == null)
                        {
                            s_instance = FindAnyObjectByType<PlayerDataManager>();
                            if (s_instance == null)
                            {
                                var container = new GameObject(nameof(PlayerDataManager));
                                s_instance = container.AddComponent<PlayerDataManager>();
                                DontDestroyOnLoad(container);
                            }
                        }
                    }
                }
                return s_instance;
            }
        }

        #endregion

        #region 초기화 

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);

            // 서비스 초기화
            m_encryptionService = new EncryptionService();
            m_localRepository = new LocalPlayerDataRepository(m_encryptionService);
        }

        #endregion

        #region 로컬 데이터 관리 

        public void SavePlayerData()
        {
            m_localRepository.Save(PlayerData);
        }

        public void LoadPlayerData()
        {
            m_localRepository.Load(PlayerData);
        }

        public void UpdatePlayerData(PlayerData playerData)
        {
            m_scriptableobjPlayerData = playerData;
            LogManager.Log("플레이어 데이터가 업데이트되었습니다.", LogManager.LogCategory.PlayerDataManager);
        }

        #endregion

        #region 서버 데이터 처리 

        public async UniTask<bool> LoadDataFromServerAsync()
        {
            try
            {
                var serverDataJson = await ServerManager.Instance.DownloadDataAsync("User_Data");

                if (serverDataJson == null)
                {
                    LogManager.LogWarning("서버에 데이터가 존재하지 않습니다.", LogManager.LogCategory.PlayerDataManager);
                    return false;
                }

                LogManager.Log("서버에서 게임 정보를 성공적으로 조회했습니다.", LogManager.LogCategory.PlayerDataManager);

                PlayerData serverData = ParseServerData(serverDataJson);
                LoadPlayerData();
                PlayerData localData = PlayerData;

                bool isVerified = VerifyDataIntegrity(serverDataJson, serverData);

                if (!isVerified)
                {
                    LogManager.LogWarning("무결성 검증 실패: 데이터 가치 비교를 통해 복구를 시도합니다.", LogManager.LogCategory.PlayerDataManager);
                }

                PlayerData finalData = ResolveDataConflict(serverData, localData);
                UpdatePlayerData(finalData);
                SavePlayerData();

                if (!isVerified || finalData == localData)
                {
                    await UploadDataToServerAsync();
                }

                return true;
            }
            catch (Exception e)
            {
                LogManager.LogError($"서버 데이터 로드 중 오류 발생: {e.Message}", LogManager.LogCategory.PlayerDataManager);
                return false;
            }
        }

        public async UniTask UploadDataToServerAsync()
        {
            Param param = new Param();
            param.Add("nickname", PlayerData.nickname);
            param.Add("uid", PlayerData.UID);
            param.Add("Money1", PlayerData.currency1);
            param.Add("Money2", PlayerData.currency2);
            param.Add("experience", PlayerData.experience);
            param.Add("level", PlayerData.level);

            string rawData = $"{PlayerData.nickname}|{PlayerData.UID}|{PlayerData.currency1}|{PlayerData.currency2}|{PlayerData.experience.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}|{PlayerData.level}";
            string integrityHash = m_encryptionService.GenerateHMAC(rawData);
            param.Add("integrityHash", integrityHash);

            try
            {
                await ServerManager.Instance.UploadDataAsync("User_Data", param);
                LogManager.Log("플레이어 데이터를 서버에 성공적으로 업로드했습니다.", LogManager.LogCategory.PlayerDataManager);
            }
            catch (Exception e)
            {
                LogManager.LogError($"플레이어 데이터 업로드 실패: {e.Message}", LogManager.LogCategory.PlayerDataManager);
            }
        }

        #endregion

        #region 보조 로직 

        private PlayerData ParseServerData(JsonData serverDataJson)
        {
            PlayerData serverData = ScriptableObject.CreateInstance<PlayerData>();
            serverData.level = int.Parse(serverDataJson["level"].ToString());
            serverData.currency1 = int.Parse(serverDataJson["Money1"].ToString());
            serverData.currency2 = int.Parse(serverDataJson["Money2"].ToString());
            serverData.experience = float.Parse(serverDataJson["experience"].ToString());
            serverData.UID = serverDataJson["uid"].ToString();
            serverData.nickname = serverDataJson["nickname"].ToString();
            return serverData;
        }

        private bool VerifyDataIntegrity(JsonData serverDataJson, PlayerData serverData)
        {
            if (!serverDataJson.ContainsKey("integrityHash"))
            {
                LogManager.LogWarning("서버 데이터에 해시값이 없습니다. (Legacy Data 가능성)", LogManager.LogCategory.PlayerDataManager);
                return true;
            }

            string serverHash = serverDataJson["integrityHash"].ToString();
            string rawData = $"{serverData.nickname}|{serverData.UID}|{serverData.currency1}|{serverData.currency2}|{serverData.experience.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}|{serverData.level}";
            string calculatedHash = m_encryptionService.GenerateHMAC(rawData);

            if (serverHash.Equals(calculatedHash))
            {
                return true;
            }

            LogManager.LogError($"데이터 변조 감지! ServerHash: {serverHash} / MyCalc: {calculatedHash}", LogManager.LogCategory.PlayerDataManager);
            return false;
        }

        private PlayerData ResolveDataConflict(PlayerData serverData, PlayerData localData)
        {
            if (localData.level > serverData.level ||
                (localData.level == serverData.level && localData.experience > serverData.experience))
            {
                LogManager.Log("로컬 데이터가 더 최신이므로 사용합니다.", LogManager.LogCategory.PlayerDataManager);
                return localData;
            }
            LogManager.Log("서버 데이터가 더 최신이므로 사용합니다.", LogManager.LogCategory.PlayerDataManager);
            return serverData;
        }

        #endregion

        #region 내부 필드 (DI) 

        private EncryptionService m_encryptionService;
        private LocalPlayerDataRepository m_localRepository;

        #endregion
    }
}