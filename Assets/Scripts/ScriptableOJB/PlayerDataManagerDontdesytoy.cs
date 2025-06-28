using System;
using System.IO;
using BackEnd;
using LitJson;
using UnityEngine;
using Object = UnityEngine.Object;

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
                if (IsNullOrEmpty(instance))
                {
                    lock (LockObject) // 스레드 안전성 보장
                    {
                        if (IsNullOrEmpty(instance))
                        {
                            instance = FindAnyObjectByType<PlayerDataManagerDontdesytoy>();
                            if (IsNullOrEmpty(instance))
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
                // 로컬에 저장된 키 파일 경로
                var keyPath = Path.Combine(Application.persistentDataPath, "rsakeys.json");

                if (File.Exists(keyPath))
                {
                    // 파일에서 키 로드
                    string keyJson = File.ReadAllText(keyPath);
                    var keyContainer = JsonUtility.FromJson<KeyContainer>(keyJson);
                    _rsaPublicKey = keyContainer.publicKey;
                    _rsaPrivateKey = keyContainer.privateKey;
                    Debug.Log("로컬 파일에서 RSA 키 쌍을 로드했습니다.");
                }
                else
                {
                    // 새 키 생성
                    _encryption.GenerateRsaKeys(out _rsaPublicKey, out _rsaPrivateKey);
            
                    // 파일에 키 저장
                    var keyContainer = new KeyContainer { publicKey = _rsaPublicKey, privateKey = _rsaPrivateKey };
                    string keyJson = JsonUtility.ToJson(keyContainer);
                    File.WriteAllText(keyPath, keyJson);

                    Debug.Log("새로운 RSA 키 쌍을 생성하고 로컬 파일에 저장했습니다.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"RSA 키 관리 중 오류 발생: {ex.Message}");
        
                // 키 생성 및 복구
                _encryption.GenerateRsaKeys(out _rsaPublicKey, out _rsaPrivateKey);
                Debug.Log("RSA 키 오류로 인해 새로운 키 쌍을 생성했습니다.");
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
                    Debug.LogWarning("저장할 플레이어 데이터가 null입니다.");
                    return;
                }
                
                var jsonData = JsonUtility.ToJson(scritpableobjPlayerData, true);
    
                // 데이터 암호화
                EncryptedPacket encryptedPacket = _encryption.Encrypt(jsonData, _rsaPublicKey);
    
                // byte[] 배열을 Base64 문자열로 변환
                SerializableEncryptedPacket serializablePacket = new SerializableEncryptedPacket
                {
                    EncryptedSessionKeyBase64 = Convert.ToBase64String(encryptedPacket.EncryptedSessionKey),
                    EncryptedDataBase64 = Convert.ToBase64String(encryptedPacket.EncryptedData)
                };
    
                // 암호화된 데이터 저장
                string packetJson = JsonUtility.ToJson(serializablePacket);
                File.WriteAllText(savePath, packetJson);
                Debug.Log($"플레이어 데이터가 암호화되어 {savePath}에 저장되었습니다.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"플레이어 데이터 저장 중 오류 발생: {ex.Message}");
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
                    // 암호화된 데이터 로드
                    string packetJson = File.ReadAllText(savePath);
                    SerializableEncryptedPacket serializablePacket = JsonUtility.FromJson<SerializableEncryptedPacket>(packetJson);
            
                    // Base64 문자열을 byte[] 배열로 변환
                    EncryptedPacket encryptedPacket = new EncryptedPacket
                    {
                        EncryptedSessionKey = Convert.FromBase64String(serializablePacket.EncryptedSessionKeyBase64),
                        EncryptedData = Convert.FromBase64String(serializablePacket.EncryptedDataBase64)
                    };
            
                    // 데이터 복호화
                    string decryptedJson = _encryption.Decrypt(encryptedPacket, _rsaPrivateKey);
            
                    // 복호화된 JSON을 PlayerData 객체로 변환
                    JsonUtility.FromJsonOverwrite(decryptedJson, scritpableobjPlayerData);
                    Debug.Log("로컬에서 플레이어 데이터를 성공적으로 로드했습니다.");
                }
                else
                {
                    Debug.LogWarning("저장된 플레이어 데이터 파일이 없습니다. 새 데이터를 생성합니다.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"플레이어 데이터 로드 중 오류 발생: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 플레이어 데이터를 업데이트합니다.
        /// </summary>
        /// <param name="playerData">새로운 플레이어 데이터</param>
        public void UpdatePlayerData(PlayerData playerData)
        {
            scritpableobjPlayerData = playerData;
            Debug.Log("플레이어 데이터가 업데이트되었습니다.");
        }
        
        #endregion

        #region 서버 데이터 처리

        /// <summary>
        /// 서버에서 플레이어 데이터를 가져옵니다.
        /// </summary>
        /// <param name="onDataNotExist">서버에 데이터가 없을 때 실행할 콜백</param>
        public void LoadDataFromServer(Action onDataNotExist)
        {
            ServerManager.Instance.DownloadData("User_Data", (bro) =>
            {
                OnServerDataReceived(bro, onDataNotExist);
            });
        }

        private void OnServerDataReceived(BackendReturnObject bro, Action onDataNotExist)
        {
            if (!bro.IsSuccess())
            {
                Debug.LogError("게임 정보 조회에 실패했습니다. : " + bro);
                if (bro.GetStatusCode() == "404") // 데이터가 없는 경우
                {
                    onDataNotExist?.Invoke();
                }
                return;
            }

            var gameDataJson = bro.FlattenRows();
            if (gameDataJson.Count <= 0)
            {
                Debug.LogWarning("서버에 데이터가 존재하지 않습니다.");
                onDataNotExist?.Invoke();
                return;
            }

            Debug.Log("서버에서 게임 정보를 성공적으로 조회했습니다.");
            var serverDataJson = gameDataJson[0];
            
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
                UploadDataToServer();
            }
        }
        
        private PlayerData ResolveDataConflict(PlayerData serverData, PlayerData localData)
        {
            // 간단한 비교 로직: 레벨과 경험치가 높은 쪽을 최신 데이터로 간주
            if (localData.level > serverData.level || 
                (localData.level == serverData.level && localData.experience > serverData.experience))
            {
                Debug.Log("로컬 데이터가 더 최신이므로 사용합니다.");
                return localData;
            }
            
            Debug.Log("서버 데이터가 더 최신이므로 사용합니다.");
            return serverData;
        }

        /// <summary>
        /// 현재 플레이어 데이터를 서버에 업로드(Insert or Update)합니다.
        /// </summary>
        public void UploadDataToServer()
        {
            Param param = new Param();
            param.Add("nickname", scritpableobjPlayerData.nickname);
            param.Add("uid", scritpableobjPlayerData.UID);
            param.Add("Money1", scritpableobjPlayerData.currency1);
            param.Add("Money2", scritpableobjPlayerData.currency2);
            param.Add("experience", scritpableobjPlayerData.experience);
            param.Add("level", scritpableobjPlayerData.level);

            ServerManager.Instance.UploadData("User_Data", param, (bro) =>
            {
                if (bro.IsSuccess())
                {
                    Debug.Log("플레이어 데이터를 서버에 성공적으로 업로드했습니다.");
                }
                else
                {
                    Debug.LogError($"플레이어 데이터 업로드 실패: {bro}");
                }
            });
        }

        #endregion

        #region 유틸리티

        /// <summary>
        /// Unity Object가 null인지 확인합니다. (== 연산자 오버로딩 대응)
        /// </summary>
        private static bool IsNullOrEmpty(Object value)
        {
            return ReferenceEquals(value, null);
        }
        
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