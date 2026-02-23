using System;
using System.IO;
using InGame.Services;
using UnityEngine;

namespace InGame.Data
{
    /// <summary>
    /// [설명]: 플레이어 데이터의 로컬 저장/로드를 담당하는 Repository 클래스입니다.
    /// </summary>
    public class LocalPlayerDataRepository
    {
        #region 상수 

        private const string k_EncryptedDataPath = "playerData.encrypted";

        #endregion

        #region 내부 필드 

        private readonly EncryptionService m_encryptionService;
        private readonly string m_savePath;

        #endregion

        #region 초기화 

        public LocalPlayerDataRepository(EncryptionService encryptionService)
        {
            m_encryptionService = encryptionService;
            m_savePath = Path.Combine(Application.persistentDataPath, k_EncryptedDataPath);
        }

        #endregion

        #region 저장/로드 메서드 

        /// <summary>
        /// [설명]: 플레이어 데이터를 암호화하여 로컬에 저장합니다.
        /// </summary>
        public void Save(PlayerDataDTO playerData)
        {
            if (playerData == null)
            {
                LogManager.LogWarning("저장할 플레이어 데이터가 null입니다.", LogManager.LogCategory.PlayerDataService);
                return;
            }

            try
            {
                string jsonData = JsonUtility.ToJson(playerData, true);
                EncryptedPacket encryptedPacket = m_encryptionService.Encrypt(jsonData);
                string packetJson = JsonUtility.ToJson(encryptedPacket);
                File.WriteAllText(m_savePath, packetJson);
                LogManager.Log($"플레이어 데이터가 암호화되어 {m_savePath}에 저장되었습니다.", LogManager.LogCategory.PlayerDataService);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"플레이어 데이터 저장 중 오류 발생: {ex.Message}", LogManager.LogCategory.PlayerDataService);
            }
        }

        /// <summary>
        /// [설명]: 로컬에서 암호화된 플레이어 데이터를 로드하고 복호화합니다.
        /// </summary>
        public void Load(PlayerDataDTO targetPlayerData)
        {
            if (targetPlayerData == null)
            {
                LogManager.LogWarning("로드 대상 PlayerDataDTO가 null입니다.", LogManager.LogCategory.PlayerDataService);
                return;
            }

            try
            {
                if (File.Exists(m_savePath))
                {
                    string packetJson = File.ReadAllText(m_savePath);
                    EncryptedPacket encryptedPacket = JsonUtility.FromJson<EncryptedPacket>(packetJson);
                    string decryptedJson = m_encryptionService.Decrypt(encryptedPacket);
                    JsonUtility.FromJsonOverwrite(decryptedJson, targetPlayerData);
                    LogManager.Log("로컬에서 플레이어 데이터를 성공적으로 로드했습니다.", LogManager.LogCategory.PlayerDataService);
                }
                else
                {
                    LogManager.LogWarning("저장된 플레이어 데이터 파일이 없습니다. 새 데이터를 생성합니다.", LogManager.LogCategory.PlayerDataService);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"플레이어 데이터 로드 중 오류 발생: {ex.Message}", LogManager.LogCategory.PlayerDataService);
            }
        }

        /// <summary>
        /// [설명]: 로컬에 저장된 데이터가 존재하는지 확인합니다.
        /// </summary>
        public bool Exists()
        {
            return File.Exists(m_savePath);
        }

        #endregion
    }
}