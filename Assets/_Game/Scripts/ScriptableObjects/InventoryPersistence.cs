using System;
using System.IO;
using BackEnd;
using Cysharp.Threading.Tasks;
using InGame.Services;
using UnityEngine;

namespace InGame.Lobby
{
    /// <summary>
    /// 인벤토리 데이터의 저장, 로드, 암호화를 담당하는 POCO 클래스입니다.
    /// EncryptionService를 통해 암호화를 수행합니다.
    /// </summary>
    public class InventoryPersistence
    {
        #region 설정

        private readonly string m_localSavePath;
        private readonly EncryptionService m_encryptionService;
        private readonly IGameDataService m_gameDataService;
        private const string k_EncryptedFileName = "inventoryData.encrypted";

        #endregion

        #region 생성자

        public InventoryPersistence(EncryptionService encryptionService, IGameDataService gameDataService)
        {
            m_localSavePath = Path.Combine(Application.persistentDataPath, k_EncryptedFileName);
            m_encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            m_gameDataService = gameDataService;
        }

        #endregion

        #region 저장 (Save)

        /// <summary>
        /// 데이터를 암호화하여 로컬 파일에 저장합니다.
        /// </summary>
        public void SaveLocal(InventoryDataSO data)
        {
            if (data == null) return;

            try
            {
                string jsonData = JsonUtility.ToJson(data, true);
                EncryptedPacket encryptedPacket = m_encryptionService.Encrypt(jsonData);
                string packetJson = JsonUtility.ToJson(encryptedPacket);
                
                File.WriteAllText(m_localSavePath, packetJson);
                LogManager.Log($"[InventoryPersistence] 암호화 저장 완료: {m_localSavePath}", LogManager.LogCategory.InventoryManager);
            }
            catch (Exception e)
            {
                LogManager.LogError($"[InventoryPersistence] 저장 실패: {e.Message}", LogManager.LogCategory.InventoryManager);
            }
        }

        /// <summary>
        /// 서버에 데이터를 업로드합니다.
        /// </summary>
        public async UniTask UploadToServerAsync(InventoryDataSO data)
        {
            if (data == null) return;

            try
            {
                string jsonData = JsonUtility.ToJson(data, true);
                
                Param param = new Param();
                param.Add("Inventory", jsonData);

                if (m_gameDataService != null)
                {
                    await m_gameDataService.UploadDataAsync("Inventory_Data", param);
                }
                LogManager.Log("[InventoryPersistence] 서버 업로드 완료", LogManager.LogCategory.InventoryManager);
            }
            catch (Exception e)
            {
                LogManager.LogError($"[InventoryPersistence] 서버 업로드 실패: {e.Message}", LogManager.LogCategory.InventoryManager);
            }
        }

        #endregion

        #region 로드 (Load)

        /// <summary>
        /// 로컬 파일에서 데이터를 로드하고 복호화합니다.
        /// </summary>
        public bool LoadLocal(InventoryDataSO targetData)
        {
            if (!File.Exists(m_localSavePath)) return false;

            try
            {
                string packetJson = File.ReadAllText(m_localSavePath);
                EncryptedPacket encryptedPacket = JsonUtility.FromJson<EncryptedPacket>(packetJson);
                string decryptedJson = m_encryptionService.Decrypt(encryptedPacket);
                
                // 데이터 덮어쓰기
                JsonUtility.FromJsonOverwrite(decryptedJson, targetData);
                
                LogManager.Log("[InventoryPersistence] 로컬 로드 완료", LogManager.LogCategory.InventoryManager);
                return true;
            }
            catch (Exception e)
            {
                LogManager.LogError($"[InventoryPersistence] 로컬 로드 실패: {e.Message}", LogManager.LogCategory.InventoryManager);
                return false;
            }
        }

        /// <summary>
        /// 서버에서 데이터를 다운로드합니다.
        /// </summary>
        public async UniTask<bool> DownloadFromServerAsync(InventoryDataSO targetData)
        {
            try
            {
                if (m_gameDataService == null) return false;
                var serverDataJson = await m_gameDataService.DownloadDataAsync("Inventory_Data");
                if (serverDataJson == null || !serverDataJson.ContainsKey("Inventory")) return false;

                string inventoryJsonString = serverDataJson["Inventory"].ToString();
                JsonUtility.FromJsonOverwrite(inventoryJsonString, targetData);

                LogManager.Log("[InventoryPersistence] 서버 다운로드 완료", LogManager.LogCategory.InventoryManager);
                return true;
            }
            catch (Exception e)
            {
                LogManager.LogError($"[InventoryPersistence] 서버 다운로드 실패: {e.Message}", LogManager.LogCategory.InventoryManager);
                return false;
            }
        }

        #endregion
    }
}
