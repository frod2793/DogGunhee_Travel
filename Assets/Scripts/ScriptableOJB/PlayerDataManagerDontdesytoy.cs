using System;
using System.IO;
using BackEnd;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace DogGuns_Games
{
    /// <summary>
    /// 플레이어 데이터를 관리하고 암호화하여 저장/로드하는 매니저 클래스
    /// DontDestroyOnLoad로 씬 전환 시에도 유지됩니다.
    /// </summary>
    public class PlayerDataManagerDontdesytoy : MonoBehaviour
    {
        #region 변수, 프로퍼티, 필드

        public PlayerData scritpableobjPlayerData;
        
        public string RsaPublicKey => _rsaPublicKey;
        public string RsaPrivateKey => _rsaPrivateKey;
        
        private string _rsaPublicKey;
        private string _rsaPrivateKey;
        private HybridEncryption _encryption;

        // 플레이어 데이터 속성에 대한 접근자
        public int SelectWeaponIndex
        {
            get => scritpableobjPlayerData.selelcWeaponIndex;
            set => scritpableobjPlayerData.selelcWeaponIndex = value;
        }

        public int SelectCharacterIndex
        {
            get => scritpableobjPlayerData.selectCharacterIndex;
            set => scritpableobjPlayerData.selectCharacterIndex = value;
        }
        
        #endregion

        #region 싱글톤 및 초기화

        private static PlayerDataManagerDontdesytoy instance;
        private static readonly object LockObject = new object();

        public static PlayerDataManagerDontdesytoy Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (LockObject)
                    {
                        if (instance == null)
                        {
                            instance = FindAnyObjectByType<PlayerDataManagerDontdesytoy>();
                            if (instance == null)
                            {
                                var container = new GameObject("PlayerDataManager");
                                instance = container.AddComponent<PlayerDataManagerDontdesytoy>();
                                DontDestroyOnLoad(container);
                            }
                        }
                    }
                }
                return instance;
            }
        }

        private void Awake()
        {
            // 싱글톤 패턴 구현
            if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            // PlayerData 초기화
            if (scritpableobjPlayerData == null)
                scritpableobjPlayerData = ScriptableObject.CreateInstance<PlayerData>();
        
            // 암호화 객체 초기화
            _encryption = new HybridEncryption();
    
            // 키 생성 또는 로드
            GenerateOrLoadRsaKeys();
        }
        
        #endregion

        #region 로컬 데이터 관리

        /// <summary>
        /// RSA 키 쌍을 생성하거나 기존 키를 로드합니다.
        /// </summary>
        private void GenerateOrLoadRsaKeys()
        {
            try
            {
                var keyPath = Path.Combine(Application.persistentDataPath, "rsakeys.json");
                if (File.Exists(keyPath))
                {
                    string keyJson = File.ReadAllText(keyPath);
                    var keyContainer = JsonUtility.FromJson<KeyContainer>(keyJson);
                    _rsaPublicKey = keyContainer.publicKey;
                    _rsaPrivateKey = keyContainer.privateKey;
                    LogManager.Log("로컬 파일에서 RSA 키 쌍을 로드했습니다.", LogManager.LogCategory.PlayerDataManager);
                }
                else
                {
                    _encryption.GenerateRsaKeys(out _rsaPublicKey, out _rsaPrivateKey);
                    var keyContainer = new KeyContainer { publicKey = _rsaPublicKey, privateKey = _rsaPrivateKey };
                    string keyJson = JsonUtility.ToJson(keyContainer);
                    File.WriteAllText(keyPath, keyJson);
                    LogManager.Log("새로운 RSA 키 쌍을 생성하고 로컬 파일에 저장했습니다.", LogManager.LogCategory.PlayerDataManager);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"RSA 키 관리 중 오류 발생: {ex.Message}", LogManager.LogCategory.PlayerDataManager);
                _encryption.GenerateRsaKeys(out _rsaPublicKey, out _rsaPrivateKey);
                LogManager.Log("RSA 키 오류로 인해 새로운 키 쌍을 생성했습니다.", LogManager.LogCategory.PlayerDataManager);
            }
        }

        /// <summary>
        /// 플레이어 데이터를 암호화하여 로컬에 저장합니다.
        /// </summary>
        public void SavePlayerData()
        {
            try
            {
                var savePath = Path.Combine(Application.persistentDataPath, "playerData.encrypted");
                if (scritpableobjPlayerData == null)
                {
                    LogManager.LogWarning("저장할 플레이어 데이터가 null입니다.", LogManager.LogCategory.PlayerDataManager);
                    return;
                }
                var jsonData = JsonUtility.ToJson(scritpableobjPlayerData, true);
                EncryptedPacket encryptedPacket = _encryption.Encrypt(jsonData, _rsaPublicKey);
                SerializableEncryptedPacket serializablePacket = new SerializableEncryptedPacket
                {
                    EncryptedSessionKeyBase64 = Convert.ToBase64String(encryptedPacket.EncryptedSessionKey),
                    EncryptedDataBase64 = Convert.ToBase64String(encryptedPacket.EncryptedData)
                };
                string packetJson = JsonUtility.ToJson(serializablePacket);
                File.WriteAllText(savePath, packetJson);
                LogManager.Log($"플레이어 데이터가 암호화되어 {savePath}에 저장되었습니다.", LogManager.LogCategory.PlayerDataManager);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"플레이어 데이터 저장 중 오류 발생: {ex.Message}", LogManager.LogCategory.PlayerDataManager);
            }
        }

        /// <summary>
        /// 로컬에서 암호화된 플레이어 데이터를 로드하고 복호화합니다.
        /// </summary>
        public void LoadPlayerData()
        {
            try
            {
                var savePath = Path.Combine(Application.persistentDataPath, "playerData.encrypted");
                if (File.Exists(savePath))
                {
                    string packetJson = File.ReadAllText(savePath);
                    SerializableEncryptedPacket serializablePacket = JsonUtility.FromJson<SerializableEncryptedPacket>(packetJson);
                    EncryptedPacket encryptedPacket = new EncryptedPacket
                    {
                        EncryptedSessionKey = Convert.FromBase64String(serializablePacket.EncryptedSessionKeyBase64),
                        EncryptedData = Convert.FromBase64String(serializablePacket.EncryptedDataBase64)
                    };
                    string decryptedJson = _encryption.Decrypt(encryptedPacket, _rsaPrivateKey);
                    JsonUtility.FromJsonOverwrite(decryptedJson, scritpableobjPlayerData);
                    LogManager.Log("로컬에서 플레이어 데이터를 성공적으로 로드했습니다.", LogManager.LogCategory.PlayerDataManager);
                }
                else
                {
                    LogManager.LogWarning("저장된 플레이어 데이터 파일이 없습니다. 새 데이터를 생성합니다.", LogManager.LogCategory.PlayerDataManager);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"플레이어 데이터 로드 중 오류 발생: {ex.Message}", LogManager.LogCategory.PlayerDataManager);
            }
        }
        
        /// <summary>
        /// 플레이어 데이터를 업데이트합니다.
        /// </summary>
        /// <param name="playerData">새로운 플레이어 데이터</param>
        public void UpdatePlayerData(PlayerData playerData)
        {
            scritpableobjPlayerData = playerData;
            LogManager.Log("플레이어 데이터가 업데이트되었습니다.", LogManager.LogCategory.PlayerDataManager);
        }
        
        #endregion

        #region 서버 데이터 처리

        /// <summary>
        /// 서버에서 플레이어 데이터를 비동기적으로 가져와 로컬 데이터와 병합합니다.
        /// </summary>
        /// <returns>서버에 데이터가 존재하면 true, 그렇지 않으면 false를 반환합니다.</returns>
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

                // 서버 데이터 파싱
                PlayerData serverData = ScriptableObject.CreateInstance<PlayerData>();
                serverData.level = int.Parse(serverDataJson["level"].ToString());
                serverData.currency1 = int.Parse(serverDataJson["Money1"].ToString());
                serverData.currency2 = int.Parse(serverDataJson["Money2"].ToString());
                serverData.experience = float.Parse(serverDataJson["experience"].ToString());
                serverData.UID = serverDataJson["uid"].ToString();
                serverData.nickname = serverDataJson["nickname"].ToString();

                // 로컬 데이터 로드
                LoadPlayerData();
                PlayerData localData = scritpableobjPlayerData;

                // 데이터 비교 및 최종 데이터 결정
                PlayerData finalData = ResolveDataConflict(serverData, localData);

                UpdatePlayerData(finalData);
                SavePlayerData(); // 최종 데이터를 로컬에 저장

                // 필요한 경우 서버 데이터 업데이트
                if (finalData == localData)
                {
                    await UploadDataToServerAsync();
                }

                return true;
            }
            catch (Exception e)
            {
                LogManager.LogError($"서버 데이터 로드 중 오류 발생: {e.Message}", LogManager.LogCategory.PlayerDataManager);
                // 404 Not Found (데이터 없음) 에러는 LoginManager에서 처리하므로 여기서는 false만 반환
                return false;
            }
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

        /// <summary>
        /// 현재 플레이어 데이터를 서버에 비동기적으로 업로드(Insert or Update)합니다.
        /// </summary>
        public async UniTask UploadDataToServerAsync()
        {
            Param param = new Param();
            param.Add("nickname", scritpableobjPlayerData.nickname);
            param.Add("uid", scritpableobjPlayerData.UID);
            param.Add("Money1", scritpableobjPlayerData.currency1);
            param.Add("Money2", scritpableobjPlayerData.currency2);
            param.Add("experience", scritpableobjPlayerData.experience);
            param.Add("level", scritpableobjPlayerData.level);

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

        #region 유틸리티


        /// <summary>
        /// RSA 키 저장을 위한 헬퍼 클래스
        /// </summary>
        [System.Serializable]
        private class KeyContainer
        {
            public string publicKey;
            public string privateKey;
        }
        
        #endregion
    }
}