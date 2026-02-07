using R3;
using System;
using InGame;

namespace Lobby
{
    /// <summary>
    /// 로비 UI의 데이터 상태를 관리하는 ViewModel 클래스입니다.
    /// R3의 ReactiveProperty를 사용하여 View가 데이터 변경을 구독할 수 있도록 합니다.
    /// </summary>
    public class LobbyViewModel : IDisposable
    {
        #region 반응형 프로퍼티

        public ReactiveProperty<string> Nickname { get; } = new ReactiveProperty<string>(string.Empty);
        public ReactiveProperty<int> Level { get; } = new ReactiveProperty<int>(1);
        public ReactiveProperty<float> Experience { get; } = new ReactiveProperty<float>(0f);
        public ReactiveProperty<int> Gold { get; } = new ReactiveProperty<int>(0);
        public ReactiveProperty<int> Diamond { get; } = new ReactiveProperty<int>(0);

        #endregion

        #region 내부 필드

        private readonly PlayerDataManager m_playerDataManager;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        #endregion

        #region 생성자

        public LobbyViewModel(PlayerDataManager playerDataManager)
        {
            m_playerDataManager = playerDataManager;

            // 초기 데이터 로드
            RefreshFromPlayerData();
        }

        #endregion

        #region 데이터 동기화

        /// <summary>
        /// PlayerData에서 최신 데이터를 읽어와 ViewModel 프로퍼티를 갱신합니다.
        /// </summary>
        public void RefreshFromPlayerData()
        {
            if (m_playerDataManager?.PlayerData == null) return;

            var data = m_playerDataManager.PlayerData;

            Nickname.Value = data.nickname ?? string.Empty;
            Level.Value = data.level;
            Experience.Value = data.experience / 100f; // Slider 비율로 변환
            Gold.Value = data.currency1;
            Diamond.Value = data.currency2;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            m_disposables.Dispose();

            Nickname.Dispose();
            Level.Dispose();
            Experience.Dispose();
            Gold.Dispose();
            Diamond.Dispose();
        }

        #endregion
    }
}
