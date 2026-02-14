using R3;
using System;
using InGame;

namespace Lobby
{
    /// <summary>
    /// [설명]: 로비 UI의 데이터 상태와 비즈니스 로직을 관리하는 ViewModel 클래스입니다.
    /// R3의 ReactiveProperty를 활용하여 View와 데이터 바인딩을 수행합니다.
    /// </summary>
    public class LobbyViewModel : IDisposable
    {
        #region 반응형 프로퍼티

        /// <summary> [설명]: 플레이어 닉네임 </summary>
        public ReactiveProperty<string> Nickname { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary> [설명]: 플레이어 레벨 </summary>
        public ReactiveProperty<int> Level { get; } = new ReactiveProperty<int>(1);

        /// <summary> [설명]: 현재 경험치 진행도 (0.0 ~ 1.0) </summary>
        public ReactiveProperty<float> Experience { get; } = new ReactiveProperty<float>(0f);

        /// <summary> [설명]: 보유 골드 수량 </summary>
        public ReactiveProperty<int> Gold { get; } = new ReactiveProperty<int>(0);

        /// <summary> [설명]: 보유 다이아 수량 </summary>
        public ReactiveProperty<int> Diamond { get; } = new ReactiveProperty<int>(0);

        #endregion

        #region 내부 필드

        private readonly PlayerDataManager m_playerDataManager;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        #endregion

        #region 생성자

        /// <summary>
        /// [설명]: LobbyViewModel을 생성하고 초기 데이터를 동기화합니다.
        /// </summary>
        /// <param name="playerDataManager">데이터를 제공할 플레이어 데이터 관리자</param>
        public LobbyViewModel(PlayerDataManager playerDataManager)
        {
            m_playerDataManager = playerDataManager;

            // 초기 데이터 로드 및 적용
            RefreshFromPlayerData();
        }

        #endregion

        #region 데이터 동기화 및 갱신

        /// <summary>
        /// [설명]: PlayerDataManager의 실제 데이터로부터 현재 뷰모델의 상태를 새로고침합니다.
        /// </summary>
        public void RefreshFromPlayerData()
        {
            if (m_playerDataManager == null || m_playerDataManager.PlayerData == null)
            {
                return;
            }

            var data = m_playerDataManager.PlayerData;

            // 반응형 프로퍼티 값 갱신 (자동으로 UI에 반영됨)
            Nickname.Value = data.nickname ?? string.Empty;
            Level.Value = data.level;

            // UI 슬라이더는 0~1 범위를 사용하므로 비율로 변환 (예시로 100 기준)
            Experience.Value = data.experience / 100f;

            Gold.Value = data.currency1;
            Diamond.Value = data.currency2;
        }

        #endregion

        #region 리소스 해제

        /// <summary>
        /// [설명]: 모든 반응형 프로퍼티와 구독 상태를 해제하여 메모리 누수를 방지합니다.
        /// </summary>
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
