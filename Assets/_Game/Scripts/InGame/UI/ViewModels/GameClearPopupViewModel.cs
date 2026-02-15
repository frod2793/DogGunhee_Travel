using System;
using R3;

namespace InGame.UI.ViewModels
{
    /// <summary>
    /// [설명]: 게임 클리어 팝업의 데이터와 비즈니스 로직을 관리하는 ViewModel입니다.
    /// 별점(Star Rating) 및 획득 재화, 처치 수 등의 데이터를 보관하며 View에 바인딩됩니다.
    /// </summary>
    public class GameClearPopupViewModel : IDisposable
    {
        #region 내부 필드
        /// <summary> [설명]: 팝업 표시 여부 </summary>
        private readonly ReactiveProperty<bool> m_isVisible = new(false);
        
        /// <summary> [설명]: 획득한 코인 수 </summary>
        private readonly ReactiveProperty<int> m_coinCount = new(0);
        
        /// <summary> [설명]: 도달한 웨이브 번호 </summary>
        private readonly ReactiveProperty<int> m_waveCount = new(0);
        
        /// <summary> [설명]: 총 처치 수 </summary>
        private readonly ReactiveProperty<int> m_killCount = new(0);
        
        /// <summary> [설명]: 획득한 별 개수 (0~3) </summary>
        private readonly ReactiveProperty<int> m_starCount = new(0);

        /// <summary> [설명]: 재시작 버튼 클릭 시 수행할 액션 </summary>
        private Action m_onRestartAction;
        
        /// <summary> [설명]: 로비 버튼 클릭 시 수행할 액션 </summary>
        private Action m_onExitAction;
        #endregion

        #region 프로퍼티 (View 바인딩용)
        public ReadOnlyReactiveProperty<bool> IsVisible => m_isVisible;
        public ReadOnlyReactiveProperty<int> CoinCount => m_coinCount;
        public ReadOnlyReactiveProperty<int> WaveCount => m_waveCount;
        public ReadOnlyReactiveProperty<int> KillCount => m_killCount;
        public ReadOnlyReactiveProperty<int> StarCount => m_starCount;
        #endregion

        #region 공개 메서드
        /// <summary>
        /// [설명]: 클리어 결과 데이터를 기반으로 팝업을 활성화합니다.
        /// </summary>
        /// <param name="coins">획득 코인</param>
        /// <param name="wave">도달 웨이브</param>
        /// <param name="kills">처치 수</param>
        /// <param name="stars">별점 (0~3)</param>
        /// <param name="onRestart">재시작 콜백</param>
        /// <param name="onExit">로비 이동 콜백</param>
        public void Show(int coins, int wave, int kills, int stars, Action onRestart, Action onExit)
        {
            m_coinCount.Value = coins;
            m_waveCount.Value = wave;
            m_killCount.Value = kills;
            m_starCount.Value = Math.Clamp(stars, 0, 3);
            
            m_onRestartAction = onRestart;
            m_onExitAction = onExit;
            
            m_isVisible.Value = true;
        }

        /// <summary>
        /// [설명]: 게임을 재시작합니다. View의 버튼 이벤트와 연결됩니다.
        /// </summary>
        public void Restart()
        {
            m_onRestartAction?.Invoke();
            Hide();
        }

        /// <summary>
        /// [설명]: 로비로 이동합니다. View의 버튼 이벤트와 연결됩니다.
        /// </summary>
        public void ExitToLobby()
        {
            m_onExitAction?.Invoke();
            Hide();
        }

        /// <summary>
        /// [설명]: 팝업을 닫습니다.
        /// </summary>
        public void Hide()
        {
            m_isVisible.Value = false;
        }
        #endregion

        #region IDisposable 구현
        public void Dispose()
        {
            m_isVisible.Dispose();
            m_coinCount.Dispose();
            m_waveCount.Dispose();
            m_killCount.Dispose();
            m_starCount.Dispose();
            
            m_onRestartAction = null;
            m_onExitAction = null;
        }
        #endregion
    }
}
