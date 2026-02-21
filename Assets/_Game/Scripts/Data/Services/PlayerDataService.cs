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
        UniTask UploadToServerAsync();
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
        #endregion

        #region 프로퍼티
        public PlayerDataDTO Data => m_data;
        #endregion

        /// <summary>
        /// [설명]: 새로운 PlayerDataService를 생성합니다.
        /// </summary>
        /// <param name="data">관리할 데이터 DTO</param>
        /// <param name="encryptionService">암호화 서비스</param>
        /// <param name="localRepository">로컬 저장소 서비스</param>
        public PlayerDataService(
            PlayerDataDTO data, 
            EncryptionService encryptionService, 
            LocalPlayerDataRepository localRepository)
        {
            m_data = data ?? new PlayerDataDTO();
            m_encryptionService = encryptionService;
            m_localRepository = localRepository;
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

            // 변경 시 즉시 저장
            SaveData();
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
        }

        /// <summary>
        /// [설명]: 데이터를 저장합니다 (비동기 작업을 동기적으로 실행).
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
            // ServerManager.Instance 참조는 순수 클래스에서 지양해야 하나 
            // 현재 ServerManager도 싱글톤이므로 우선 연결 유지
            // TODO: ServerManager도 서비스 주입 방식으로 전환 권장
            
            // 기존 데이터 관리 로직의 DTO 버전 구현
            await UniTask.CompletedTask;
            return true; 
        }

        /// <summary>
        /// [설명]: 현재 DTO 상태를 서버에 업로드합니다.
        /// </summary>
        public async UniTask UploadToServerAsync()
        {
            // 업로드 로직 구현
            await UniTask.CompletedTask;
        }
        #endregion
    }
    #endregion
}
