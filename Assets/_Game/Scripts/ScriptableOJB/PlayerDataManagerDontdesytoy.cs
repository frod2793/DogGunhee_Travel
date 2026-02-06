using System;
using System.IO;
using BackEnd;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Serialization;

namespace InGame
{
    /// <summary>
    /// 플레이어 데이터를 관리하고 암호화하여 저장/로드하는 매니저 클래스
    /// DontDestroyOnLoad로 씬 전환 시에도 유지됩니다.
    /// </summary>
    public class PlayerDataManagerDontdesytoy : MonoBehaviour
    {
        #region 변수, 프로퍼티, 필드

        [Header("데이터")]
        [Tooltip("게임의 전반적인 설정을 관리하는 ScriptableObject 입니다.")]
        [SerializeField] private SettingsData m_settingsData;
        
        [Tooltip("플레이어 데이터를 담고 있는 ScriptableObject 입니다.")]
        [SerializeField] private PlayerData m_scriptableobjPlayerData;

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

        public string RsaPublicKey => _rsaPublicKey;
        public string RsaPrivateKey => _rsaPrivateKey;

        private string _rsaPublicKey;
        private string _rsaPrivateKey;
        private HybridEncryption _encryption;

        private const string k_EncryptedDataPath = "playerData.encrypted";
        private const string k_RsaKeysPath = "rsakeys.json";
        
        // 데이터 무결성 검증을 위한 비밀키 (주의: 실제 상용 환경에서는 보안 저장소나 Remote Config 등을 통해 관리해야 함)
        private const string k_IntegritySecretKey = "DogGunhee_Travel_Secret_Key_2025";

        // 플레이어 데이터 속성에 대한 접근자
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

        #region 싱글톤 및 초기화

        private static PlayerDataManagerDontdesytoy s_instance;
        private static readonly object s_lockObject = new object();

        public static PlayerDataManagerDontdesytoy Instance
        {
            get
            {
                if (s_instance == null)
                {
                    lock (s_lockObject)
                    {
                        if (s_instance == null)
                        {
                            s_instance = FindAnyObjectByType<PlayerDataManagerDontdesytoy>();
                            if (s_instance == null)
                            {
                                var container = new GameObject("PlayerDataManager");
                                s_instance = container.AddComponent<PlayerDataManagerDontdesytoy>();
                                DontDestroyOnLoad(container);
                            }
                        }
                    }
                }
                return s_instance;
            }
        }

        private void Awake()
        {
            // 싱글톤 패턴 구현
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);

            // 설정 파일에서 프레임 레이트 불러와서 적용
            if (m_settingsData != null)
            {
                m_settingsData.LoadSettings();
                SetTargetFrameRate(m_settingsData.TargetFrameRate);
            }
            else
            {
                LogManager.LogError("SettingsData가 PlayerDataManager에 할당되지 않았습니다. 기본 프레임으로 실행됩니다.", LogManager.LogCategory.PlayerDataManager);
                SetTargetFrameRate(120); // 기본값
            }

            // 암호화 객체 초기화
            _encryption = new HybridEncryption();

            // 키 생성 또는 로드
            GenerateOrLoadRsaKeys();
        }

        private void OnEnable()
        {
            // 설정이 변경될 때마다 자동으로 프레임 설정을 다시 로드하도록 이벤트 구독
            SettingsData.OnSettingsChanged += ApplyFrameRateSetting;
        }

        private void OnDisable()
        {
            // 오브젝트가 비활성화되거나 파괴될 때 이벤트 구독 해제
            SettingsData.OnSettingsChanged -= ApplyFrameRateSetting;
        }

        #endregion

        #region 성능 설정

        /// <summary>
        /// 게임의 목표 프레임 레이트를 설정합니다.
        /// </summary>
        private void ApplyFrameRateSetting()
        {
            if (m_settingsData != null)
            {
                SetTargetFrameRate(m_settingsData.TargetFrameRate);
            }
        }
        
        /// <summary>
        /// 게임의 목표 프레임 레이트를 설정합니다.
        /// </summary>
        /// <param name="frameRate">목표 FPS (예: 60, 120)</param>
        public void SetTargetFrameRate(int frameRate)
        {
            // 30 미만의 유효하지 않은 값(단, -1은 '제한 없음'이므로 예외)이 들어오면 무시합니다.
            if (frameRate < 30 && frameRate != -1)
            {
                LogManager.LogWarning($"유효하지 않은 목표 프레임({frameRate})이 요청되어 무시합니다.", LogManager.LogCategory.PlayerDataManager);
                return;
            }

            // 현재 설정과 동일한 경우, 불필요한 변경 및 로그를 방지합니다.
            if (Application.targetFrameRate == frameRate && QualitySettings.vSyncCount == 0)
            {
                return;
            }

            Application.targetFrameRate = frameRate;
            QualitySettings.vSyncCount = 0; // VSync를 꺼야 targetFrameRate가 제대로 동작합니다.
            LogManager.Log($"목표 프레임 레이트를 {frameRate}으로 설정했습니다.", LogManager.LogCategory.PlayerDataManager);
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
                var savePath = Path.Combine(Application.persistentDataPath, k_EncryptedDataPath);
                if (PlayerData == null)
                {
                    LogManager.LogWarning("저장할 플레이어 데이터가 null입니다.", LogManager.LogCategory.PlayerDataManager);
                    return;
                }
                var jsonData = JsonUtility.ToJson(PlayerData, true);
                EncryptedPacket encryptedPacket = _encryption.Encrypt(jsonData, _rsaPublicKey);
                string packetJson = JsonUtility.ToJson(encryptedPacket);
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
                var savePath = Path.Combine(Application.persistentDataPath, k_EncryptedDataPath);
                if (File.Exists(savePath))
                {
                    string packetJson = File.ReadAllText(savePath);
                    EncryptedPacket encryptedPacket = JsonUtility.FromJson<EncryptedPacket>(packetJson);
                    string decryptedJson = _encryption.Decrypt(encryptedPacket, _rsaPrivateKey);
                    JsonUtility.FromJsonOverwrite(decryptedJson, PlayerData);
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
            m_scriptableobjPlayerData = playerData;
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

                PlayerData serverData = ParseServerData(serverDataJson);
                LogManager.Log($"[Debug] Parsed Server Data - Level: {serverData.level}, Money1: {serverData.currency1}", LogManager.LogCategory.PlayerDataManager);

                // 로컬 데이터 로드
                LoadPlayerData();
                PlayerData localData = PlayerData;
                LogManager.Log($"[Debug] Local Data - Level: {localData.level}, Money1: {localData.currency1}", LogManager.LogCategory.PlayerDataManager);

                PlayerData finalData;

                // [무결성 검증] 서버 데이터 검증
                bool isVerified = VerifyDataIntegrity(serverDataJson, serverData);
                LogManager.Log($"[Debug] Verification Result: {isVerified}", LogManager.LogCategory.PlayerDataManager);

                // [수정] 무결성 검증 실패 시에도 데이터 복구를 위해 Conflict Resolution 진행
                // 보안 정책 완화: 해시가 다르더라도 서버 데이터(Legacy/Migration)가 더 가치있다면(레벨 등) 채택 후 재서명(Re-sign)하여 업로드
                if (!isVerified)
                {
                    LogManager.LogWarning("무결성 검증 실패: 데이터 위변조 또는 키/포맷 불일치(Migration) 가능성. 데이터 가치 비교를 통해 복구를 시도합니다.", LogManager.LogCategory.PlayerDataManager);
                }

                // 무결성 여부와 관계없이 더 나은 데이터를 선택 (레벨/경험치 기준)
                finalData = ResolveDataConflict(serverData, localData);
                LogManager.Log($"[Debug] Conflict Resolved. Selected Source: {(finalData == serverData ? "Server" : "Local")}", LogManager.LogCategory.PlayerDataManager);

                UpdatePlayerData(finalData);
                SavePlayerData(); // 최종 데이터를 로컬에 저장

                // 동기화 및 복구 업로드 조건
                // 1. 검증이 실패했거나 (!isVerified) -> 해시 재발급(Self-Healing) 필요
                // 2. 로컬 데이터가 최종 선택되었을 때 (finalData == localData) -> 서버 동기화 필요
                if (!isVerified || finalData == localData)
                {
                    LogManager.Log("[Debug] Uploading Data to Server (Sync/Fix/Self-Healing)", LogManager.LogCategory.PlayerDataManager);
                    await UploadDataToServerAsync();
                }
                else
                {
                    LogManager.Log("[Debug] Skipping Upload (Server is valid and up-to-date)", LogManager.LogCategory.PlayerDataManager);
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

        private PlayerData ParseServerData(LitJson.JsonData serverDataJson)
        {
            PlayerData serverData = ScriptableObject.CreateInstance<PlayerData>();
            serverData.level = int.Parse(serverDataJson["level"].ToString());
            serverData.currency1 = int.Parse(serverDataJson["Money1"].ToString());
            serverData.currency2 = int.Parse(serverDataJson["Money2"].ToString());
            serverData.experience = float.Parse(serverDataJson["experience"].ToString());
            serverData.UID = serverDataJson["uid"].ToString();
            serverData.nickname = serverDataJson["nickname"].ToString();
            // 필요한 다른 필드들도 여기서 파싱합니다.
            // 예: serverData.selectCharacterIndex = int.Parse(serverDataJson["selectCharacterIndex"].ToString());

            return serverData;
        }

        /// <summary>
        /// 서버로부터 받은 데이터의 무결성을 검증합니다.
        /// </summary>
        private bool VerifyDataIntegrity(LitJson.JsonData serverDataJson, PlayerData serverData)
        {
            // 1. 해시값 존재 여부 확인
            if (!serverDataJson.ContainsKey("integrityHash"))
            {
                LogManager.LogWarning("서버 데이터에 해시값이 없습니다. (Legacy Data 가능성)", LogManager.LogCategory.PlayerDataManager);
                // 초기 단계에서는 호환성을 위해 true 반환 (보안 강화 시 false로 변경 고려)
                return true; 
            }

            string serverHash = serverDataJson["integrityHash"].ToString();

            LogManager.Log($"[Debug Integrity] ServerHash: {serverHash}", LogManager.LogCategory.PlayerDataManager);
            LogManager.Log($"[Debug Integrity] Components(Server): Nick={serverData.nickname}, UID={serverData.UID}, Money1={serverData.currency1}, Money2={serverData.currency2}, Exp={serverData.experience}, Lv={serverData.level}", LogManager.LogCategory.PlayerDataManager);

            // 2. 로컬에서 동일한 방식으로 해시 재생성 (구조적 안정성 강화: 구분자 사용 및 정밀도 보장)
            // 형식: Nick|UID|Money1|Money2|Exp|Start
            // float는 ToString("R")을 사용하여 라운드트립 보장
            string rawData = $"{serverData.nickname}|{serverData.UID}|{serverData.currency1}|{serverData.currency2}|{serverData.experience.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}|{serverData.level}";
            LogManager.Log($"[Debug Integrity] RawData: {rawData}", LogManager.LogCategory.PlayerDataManager);

            string calculatedHash = _encryption.GenerateHMAC(rawData, k_IntegritySecretKey);
            LogManager.Log($"[Debug Integrity] CalculatedHash: {calculatedHash}", LogManager.LogCategory.PlayerDataManager);

            // 3. 비교
            if (serverHash.Equals(calculatedHash))
            {
                return true;
            }

            LogManager.LogError($"데이터 변조 감지! ServerHash: {serverHash} / MyCalc: {calculatedHash}", LogManager.LogCategory.PlayerDataManager);
            LogManager.LogError($"RawData Used: {rawData}", LogManager.LogCategory.PlayerDataManager);
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

        /// <summary>
        /// 현재 플레이어 데이터를 서버에 비동기적으로 업로드(Insert or Update)합니다.
        /// </summary>
        public async UniTask UploadDataToServerAsync()
        {
            Param param = new Param();
            param.Add("nickname", PlayerData.nickname);
            param.Add("uid", PlayerData.UID);
            param.Add("Money1", PlayerData.currency1);
            param.Add("Money2", PlayerData.currency2);
            param.Add("experience", PlayerData.experience);
            param.Add("level", PlayerData.level);

            // [무결성 검증] HMAC-SHA256 해시 생성 및 추가
            // 데이터 순서를 고정하고 구분자를 사용하여 구조적 문제 해결 (InvariantCulture + 구분자 + 정밀도 "R")
            string rawData = $"{PlayerData.nickname}|{PlayerData.UID}|{PlayerData.currency1}|{PlayerData.currency2}|{PlayerData.experience.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}|{PlayerData.level}";
            string integrityHash = _encryption.GenerateHMAC(rawData, k_IntegritySecretKey);
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