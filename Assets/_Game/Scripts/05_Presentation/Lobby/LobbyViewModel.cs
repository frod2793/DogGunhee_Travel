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

        private readonly InGame.Data.PlayerDataDTO m_playerData;
        private readonly InGame.Services.PlayerDataService m_playerService;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        #endregion

        #region 생성자

        /// <summary>
        /// [설명]: LobbyViewModel을 생성하고 DTO 데이터를 바인딩합니다.
        /// </summary>
        /// <param name="playerData">플레이어 데이터 DTO</param>
        /// <param name="playerService">데이터 조작을 담당하는 서비스</param>
        public LobbyViewModel(InGame.Data.PlayerDataDTO playerData, InGame.Services.PlayerDataService playerService)
        {
            m_playerData = playerData;
            m_playerService = playerService;

            // 초기 데이터 로드 및 적용
            RefreshFromPlayerData();

            // 데이터 변경 이벤트 구독
            if (m_playerService != null)
            {
                m_playerService.OnDataChanged += RefreshFromPlayerData;
            }
        }

        #endregion

        #region 데이터 동기화 및 갱신

        /// <summary>
        /// [설명]: DTO의 실제 데이터로부터 현재 뷰모델의 상태를 새로고침합니다.
        /// </summary>
        public void RefreshFromPlayerData()
        {
            if (m_playerData == null)
            {
                return;
            }

            // 반응형 프로퍼티 값 갱신 (자동으로 UI에 반영됨)
            Nickname.Value = m_playerData.Nickname ?? string.Empty;
            Level.Value = m_playerData.Level;

            // UI 슬라이더는 0~1 범위를 사용하므로 비율로 변환 (예시로 100 기준)
            Experience.Value = m_playerData.Experience / 100f;

            Gold.Value = m_playerData.Currency1;
            Diamond.Value = m_playerData.Currency2;
        }

        #endregion

        #region 리소스 해제

        /// <summary>
        /// [설명]: 모든 반응형 프로퍼티와 구독 상태를 해제하여 메모리 누수를 방지합니다.
        /// </summary>
        public void Dispose()
        {
            if (m_playerService != null)
            {
                m_playerService.OnDataChanged -= RefreshFromPlayerData;
            }

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
