using System;
using System.IO;
using UnityEngine;

namespace InGame.Services
{
    /// <summary>
    /// [설명]: RSA 키 쌍을 관리하고 암호화/복호화 기능을 제공하는 서비스입니다.
    /// </summary>
    public class EncryptionService
    {
        #region 상수 

        private const string k_RsaKeysFileName = "rsakeys.json";
        private const string k_IntegritySecretKey = "DogGunhee_Travel_Secret_Key_2025";

        #endregion

        #region 내부 필드 

        private readonly HybridEncryption m_encryption;
        private string m_rsaPublicKey;
        private string m_rsaPrivateKey;

        #endregion

        #region 프로퍼티 

        public string RsaPublicKey => m_rsaPublicKey;
        public string RsaPrivateKey => m_rsaPrivateKey;

        #endregion

        #region 생성자 

        public EncryptionService()
        {
            m_encryption = new HybridEncryption();
            GenerateOrLoadRsaKeys();
        }

        #endregion

        #region RSA 키 관리 

        /// <summary>
        /// [설명]: RSA 키 쌍을 생성하거나 기존 키를 로드합니다.
        /// </summary>
        private void GenerateOrLoadRsaKeys()
        {
            try
            {
                string keyPath = Path.Combine(Application.persistentDataPath, k_RsaKeysFileName);
                if (File.Exists(keyPath))
                {
                    string keyJson = File.ReadAllText(keyPath);
                    var keyContainer = JsonUtility.FromJson<KeyContainer>(keyJson);
                    m_rsaPublicKey = keyContainer.publicKey;
                    m_rsaPrivateKey = keyContainer.privateKey;
                    LogManager.Log("로컬 파일에서 RSA 키 쌍을 로드했습니다.", LogManager.LogCategory.PlayerDataManager);
                }
                else
                {
                    m_encryption.GenerateRsaKeys(out m_rsaPublicKey, out m_rsaPrivateKey);
                    var keyContainer = new KeyContainer { publicKey = m_rsaPublicKey, privateKey = m_rsaPrivateKey };
                    string keyJson = JsonUtility.ToJson(keyContainer);
                    File.WriteAllText(keyPath, keyJson);
                    LogManager.Log("새로운 RSA 키 쌍을 생성하고 로컬 파일에 저장했습니다.", LogManager.LogCategory.PlayerDataManager);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"RSA 키 관리 중 오류 발생: {ex.Message}", LogManager.LogCategory.PlayerDataManager);
                m_encryption.GenerateRsaKeys(out m_rsaPublicKey, out m_rsaPrivateKey);
                LogManager.Log("RSA 키 오류로 인해 새로운 키 쌍을 생성했습니다.", LogManager.LogCategory.PlayerDataManager);
            }
        }

        #endregion

        #region 암호화/복호화 메서드 

        /// <summary>
        /// [설명]: 주어진 JSON 문자열을 암호화합니다.
        /// </summary>
        public EncryptedPacket Encrypt(string plainJson)
        {
            return m_encryption.Encrypt(plainJson, m_rsaPublicKey);
        }

        /// <summary>
        /// [설명]: 암호화된 패킷을 복호화합니다.
        /// </summary>
        public string Decrypt(EncryptedPacket packet)
        {
            return m_encryption.Decrypt(packet, m_rsaPrivateKey);
        }

        /// <summary>
        /// [설명]: HMAC-SHA256 해시를 생성합니다.
        /// </summary>
        public string GenerateHMAC(string data)
        {
            return m_encryption.GenerateHMAC(data, k_IntegritySecretKey);
        }

        #endregion

        #region 내부 클래스 

        [System.Serializable]
        private class KeyContainer
        {
            public string publicKey;
            public string privateKey;
        }

        #endregion
    }
}