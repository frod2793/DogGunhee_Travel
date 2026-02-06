using System;
using System.IO;
using BackEnd;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace InGame.Lobby
{
    /// <summary>
    /// 인벤토리 데이터의 저장, 로드, 암호화를 담당하는 POCO 클래스입니다.
    /// </summary>
    public class InventoryPersistence
    {
        #region 설정

        private readonly string m_localSavePath;
        private const string k_EncryptedFileName = "inventoryData.encrypted";
        
        private HybridEncryption m_encryption;

        #endregion

        #region 생성자

        public InventoryPersistence()
        {
            m_localSavePath = Path.Combine(Application.persistentDataPath, k_EncryptedFileName);
            m_encryption = new HybridEncryption();
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
                string rsaPublicKey = PlayerDataManagerDontdesytoy.Instance?.RsaPublicKey;

                if (string.IsNullOrEmpty(rsaPublicKey))
                {
                    // 공개키 없음 -> 평문 저장 (백업)
                    Debug.LogWarning("[InventoryPersistence] RSA 키 없음. 평문으로 저장합니다.");
                    File.WriteAllText(m_localSavePath.Replace(".encrypted", ".json"), jsonData);
                    return;
                }

                if (m_encryption == null) m_encryption = new HybridEncryption();

                // 암호화 수행
                EncryptedPacket encryptedPacket = m_encryption.Encrypt(jsonData, rsaPublicKey);
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

                await ServerManager.Instance.UploadDataAsync("Inventory_Data", param);
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

                string rsaPrivateKey = PlayerDataManagerDontdesytoy.Instance?.RsaPrivateKey;
                if (string.IsNullOrEmpty(rsaPrivateKey))
                {
                    LogManager.LogError("[InventoryPersistence] RSA 개인키가 없어 복호화 불가", LogManager.LogCategory.InventoryManager);
                    return false;
                }

                if (m_encryption == null) m_encryption = new HybridEncryption();

                string decryptedJson = m_encryption.Decrypt(encryptedPacket, rsaPrivateKey);
                
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
                var serverDataJson = await ServerManager.Instance.DownloadDataAsync("Inventory_Data");
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
