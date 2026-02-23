using System;
using Cysharp.Threading.Tasks;
using InGame.Data;
using InGame.Services;
using LitJson;
using UnityEngine;
using BackEnd;

namespace InGame.Services
{
    #region 서비스 인터페이스
    /// <summary>
    /// [설명]: 플레이어 데이터 관리 서비스의 인터페이스입니다.
    /// </summary>
    public interface IPlayerDataService
    {
        PlayerDataDTO Data { get; }
        void AddCurrency(string type, int amount);
        void SubtractCurrency(string type, int amount);
        void SaveData(); // 동기식 래퍼 (기존 호환용)
        UniTask SaveLocalAsync();
        UniTask<bool> LoadFromServerAsync();
        UniTask UploadToServerAsync(bool includeCurrency = true);
        event Action OnDataChanged;
    }
    #endregion

    #region 서비스 구현
    /// <summary>
    /// [설명]: 플레이어 데이터를 관리하고 서버/로컬 동기화를 담당하는 서비스 클래스입니다.
    /// MonoBehaviour를 상속받지 않는 Pure C# 클래스입니다.
    /// </summary>
    public class PlayerDataService : IPlayerDataService
    {
        #region 내부 필드
        private readonly PlayerDataDTO m_data;
        private readonly EncryptionService m_encryptionService;
        private readonly LocalPlayerDataRepository m_localRepository;
        private readonly IGameDataService m_gameDataService;
        #endregion

        #region 프로퍼티
        public PlayerDataDTO Data => m_data;
        public event Action OnDataChanged;
        #endregion

        /// <summary>
        /// [설명]: 새로운 PlayerDataService를 생성합니다.
        /// </summary>
        /// <param name="data">관리할 데이터 DTO</param>
        /// <param name="encryptionService">암호화 서비스</param>
        /// <param name="localRepository">로컬 저장소 서비스</param>
        /// <param name="gameDataService">서버 데이터 서비스 (뒤끝)</param>
        public PlayerDataService(
            PlayerDataDTO data, 
            EncryptionService encryptionService, 
            LocalPlayerDataRepository localRepository,
            IGameDataService gameDataService = null)
        {
            m_data = data ?? new PlayerDataDTO();
            m_encryptionService = encryptionService;
            m_localRepository = localRepository;
            m_gameDataService = gameDataService;
        }

        #region 공개 메서드
        /// <summary>
        /// [설명]: 재화를 추가하고 로컬에 즉시 저장합니다.
        /// </summary>
        public void AddCurrency(string type, int amount)
        {
            if (amount <= 0) return;

            switch (type)
            {
                case "currency1":
                case "Currency1":
                    m_data.Currency1 += amount;
                    break;
                case "currency2":
                case "Currency2":
                    m_data.Currency2 += amount;
                    break;
                case "ingameCoin":
                case "IngameCoin":
                    m_data.IngameCoin += amount;
                    break;
            }

            // 변경 시 즉시 저장 및 알림
            SaveData();
            OnDataChanged?.Invoke();
        }

        /// <summary>
        /// [설명]: 재화를 소모하고 로컬에 즉시 저장합니다.
        /// </summary>
        public void SubtractCurrency(string type, int amount)
        {
            if (amount <= 0) return;

            switch (type)
            {
                case "currency1":
                case "Currency1":
                    m_data.Currency1 = Mathf.Max(0, m_data.Currency1 - amount);
                    break;
                case "currency2":
                case "Currency2":
                    m_data.Currency2 = Mathf.Max(0, m_data.Currency2 - amount);
                    break;
                case "ingameCoin":
                case "IngameCoin":
                    m_data.IngameCoin = Mathf.Max(0, m_data.IngameCoin - amount);
                    break;
            }

            SaveData();
            OnDataChanged?.Invoke();
        }

        /// <summary>
        /// [설명]: 데이터를 저장합니다 (로컬 및 서버 동기화).
        /// </summary>
        public void SaveData()
        {
            SaveLocalAsync().Forget();
        }

        /// <summary>
        /// [설명]: 실시간 데이터를 로컬 저장소에 저장합니다.
        /// </summary>
        public async UniTask SaveLocalAsync()
        {
            if (m_localRepository != null)
            {
                m_localRepository.Save(m_data);
                await UniTask.CompletedTask;
            }
        }

        /// <summary>
        /// [설명]: 서버에서 데이터를 가져와 DTO를 업데이트합니다.
        /// </summary>
        public async UniTask<bool> LoadFromServerAsync()
        {
            if (m_gameDataService == null)
            {
                return false;
            }

            try
            {
                // 뒤끝 "User_Data" 테이블에서 내 데이터 다운로드
                var row = await m_gameDataService.DownloadDataAsync("User_Data");
                
                if (row != null)
                {
                    LogManager.Log($"[PlayerDataService] 서버 원본 데이터 수신: {row.ToJson()}", LogManager.LogCategory.PlayerDataService);
                    
                    // [수정]: 데이터 형식이 다르거나 비어있을 경우를 대비해 안전한 파싱(TryParse)을 사용합니다.
                    int s_gold = ParseIntSafe(row, "Money1", m_data.Currency1);
                    int s_dia = ParseIntSafe(row, "Money2", m_data.Currency2);
                    int s_lv = ParseIntSafe(row, "Level", m_data.Level);
                    float s_exp = ParseFloatSafe(row, "Exp", m_data.Experience);

                    m_data.Currency1 = s_gold;
                    m_data.Currency2 = s_dia;
                    m_data.Level = s_lv;
                    m_data.Experience = s_exp;

                    LogManager.Log($"[PlayerDataService] 서버 데이터 로드 완료 - Gold: {s_gold}, Dia: {s_dia}, Lv: {s_lv}, Exp: {s_exp}", LogManager.LogCategory.PlayerDataService);
                    
                    // 로드 직후 로컬에도 최신화 저장
                    SaveLocalAsync().Forget();
                    OnDataChanged?.Invoke();
                    
                    return true;
                }
                else
                {
                    LogManager.Log("[PlayerDataService] 기존 서버 데이터가 없습니다. 신규 유저로 판단하여 서버에 빈 데이터를 초기 생성합니다.", LogManager.LogCategory.PlayerDataService);
                    // 빈 값이더라도 서버에 row를 만들어주어야 이후 AddCalculation 등이 정상 작동합니다.
                    UploadToServerAsync().Forget();
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[PlayerDataService] 서버 데이터 로딩 실패: {ex.Message}", LogManager.LogCategory.PlayerDataService);
            }

            return false; 
        }

        /// <summary>
        /// [설명]: 현재 DTO 상태를 서버에 업로드합니다.
        /// </summary>
        /// <param name="includeCurrency">재화(Gold, Diamond) 정보를 포함할지 여부</param>
        public async UniTask UploadToServerAsync(bool includeCurrency = true)
        {
            if (m_gameDataService == null)
            {
                return;
            }

            try
            {
                // 재화 및 경험치 등의 핵심 데이터 파라미터 생성
                var param = new Param();
                
                if (includeCurrency)
                {
                    param.Add("Money1", m_data.Currency1);     // Gold
                    param.Add("Money2", m_data.Currency2);     // Diamond
                }

                param.Add("Level", m_data.Level);
                param.Add("Exp", m_data.Experience);

                // 뒤끝 테이블 "User_Data"에 업로드 요청
                await m_gameDataService.UploadDataAsync("User_Data", param);
                
                LogManager.Log($"[PlayerDataService] 서버 데이터 동기화 완료 (User_Data, includeCurrency: {includeCurrency})", LogManager.LogCategory.PlayerDataService);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[PlayerDataService] 서버 데이터 업로드 실패: {ex.Message}", LogManager.LogCategory.PlayerDataService);
            }
        }

        #region 데이터 파싱 헬퍼

        /// <summary>
        /// [설명]: JsonData에서 특정 키의 값을 정수로 안전하게 변환합니다.
        /// </summary>
        private int ParseIntSafe(LitJson.JsonData data, string key, int defaultValue)
        {
            if (data == null || !data.Keys.Contains(key) || data[key] == null)
            {
                LogManager.Log($"[PlayerDataService] {key} 필드가 없거나 null입니다. 기본값 {defaultValue} 반환.", LogManager.LogCategory.PlayerDataService);
                return defaultValue;
            }

            LitJson.JsonData targetData = data[key];

            // [추가]: 뒤끝 AddCalculation 사용 시 데이터가 {"number": value, "operator": "+"} 오브젝트로 올 수 있음
            // 하위 호환성을 위해 number 필드가 있다면 추출합니다.
            if (targetData.IsObject && targetData.Keys.Contains("number"))
            {
                targetData = targetData["number"];
            }

            string valStr = targetData.ToString();
            if (int.TryParse(valStr, out int result))
            {
                return result;
            }

            // [보강]: double 포맷(예: 103.0)일 경우 int.TryParse가 실패할 수 있으므로 double로 먼저 파싱 시도
            if (double.TryParse(valStr, out double doubleResult))
            {
                return (int)doubleResult;
            }

            LogManager.LogWarning($"[PlayerDataService] {key} 파싱 실패 (값: {valStr}, 타입: {targetData.GetJsonType()}). 기본값 {defaultValue}을 유지합니다.", LogManager.LogCategory.PlayerDataService);
            return defaultValue;
        }

        /// <summary>
        /// [설명]: JsonData에서 특정 키의 값을 부동소수점으로 안전하게 변환합니다.
        /// </summary>
        private float ParseFloatSafe(LitJson.JsonData data, string key, float defaultValue)
        {
            if (data == null || !data.Keys.Contains(key) || data[key] == null)
            {
                return defaultValue;
            }

            LitJson.JsonData targetData = data[key];

            // [추가]: 뒤끝 AddCalculation 사용 시 데이터가 {"number": value, "operator": "+"} 오브젝트로 올 수 있음
            if (targetData.IsObject && targetData.Keys.Contains("number"))
            {
                targetData = targetData["number"];
            }

            string valStr = targetData.ToString();
            if (float.TryParse(valStr, out float result))
            {
                return result;
            }

            LogManager.LogWarning($"[PlayerDataService] {key} 파싱 실패 (값: {valStr}, 타입: {targetData.GetJsonType()}). 기본값 {defaultValue}을 유지합니다.", LogManager.LogCategory.PlayerDataService);
            return defaultValue;
        }

        #endregion
    }
    #endregion
    #endregion
}
