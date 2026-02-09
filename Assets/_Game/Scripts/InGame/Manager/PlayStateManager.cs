using System;

namespace InGame.Manager
{
    /// <summary>
    /// 게임의 전역 상태(시작, 일시정지, 재개, 종료)를 관리하는 순수 C# 로직 클래스입니다.
    /// GameManager에 의해 소유 및 관리됩니다.
    /// </summary>
    public class PlayStateManager
    {
        #region 상태 정의

        /// <summary>
        /// 게임의 현재 상태를 나타내는 열거형입니다.
        /// </summary>
        public enum GameState
        {
            Ready,      // 초기 대기 상태
            Play,       // 플레이 중
            Pause,      // 일시 정지
            Resume,     // 일시 정지 후 재개
            GameOver    // 게임 종료
        }

        #endregion

        #region 이벤트

        /// <summary>게임이 시작될 때 호출되는 이벤트입니다.</summary>
        public event Action OnGameStart;
        /// <summary>게임이 일시정지될 때 호출되는 이벤트입니다.</summary>
        public event Action OnGamePause;
        /// <summary>게임이 재개될 때 호출되는 이벤트입니다.</summary>
        public event Action OnGameResume;
        /// <summary>게임이 종료될 때 호출되는 이벤트입니다.</summary>
        public event Action OnGameOver;

        #endregion

        #region 내부 필드 및 프로퍼티

        private GameState m_playState = GameState.Ready;

        /// <summary>
        /// 게임의 현재 상태를 가져오거나 설정합니다.
        /// </summary>
        public GameState PlayState
        {   
            get => m_playState;
            set
            {
                if (m_playState == value) return;
                m_playState = value;
                HandleStateChange(m_playState);
            }
        }

        /// <summary>
        /// 현재 게임이 진행 중인 상태(Play 또는 Resume)인지 여부를 반환합니다.
        /// </summary>
        public bool IsPlaying => PlayState == GameState.Play || PlayState == GameState.Resume;

        #endregion

        #region 상태 제어 메서드

        /// <summary>
        /// 게임을 시작 상태로 전환합니다.
        /// </summary>
        public void StartGame()
        {
            PlayState = GameState.Play;
        }

        /// <summary>
        /// 게임을 일시 정지 상태로 전환합니다.
        /// </summary>
        public void Pause()
        {
            if (PlayState == GameState.GameOver) return;
            PlayState = GameState.Pause;
        }

        /// <summary>
        /// 게임을 재개 상태로 전환합니다.
        /// </summary>
        public void Resume()
        {
            if (PlayState == GameState.GameOver) return;
            PlayState = GameState.Resume;
        }

        /// <summary>
        /// 게임을 종료 상태로 전환합니다.
        /// </summary>
        public void GameOver()
        {
            PlayState = GameState.GameOver;
        }

        #endregion

        #region 내부 로직

        /// <summary>
        /// 상태 변화에 따른 이벤트를 호출하고 로그를 기록합니다.
        /// </summary>
        private void HandleStateChange(GameState newState)
        {
            switch (newState)
            {
                case GameState.Play:
                    OnGameStart?.Invoke();
                    LogManager.Log("게임 시작 (OnGameStart)", LogManager.LogCategory.PlayStateManager);
                    break;

                case GameState.Pause:
                    OnGamePause?.Invoke();
                    LogManager.Log("게임 일시정지 (OnGamePause)", LogManager.LogCategory.PlayStateManager);
                    break;

                case GameState.Resume:
                    OnGameResume?.Invoke();
                    LogManager.Log("게임 재개 (OnGameResume)", LogManager.LogCategory.PlayStateManager);
                    break;

                case GameState.GameOver:
                    OnGameOver?.Invoke();
                    LogManager.Log("게임 오버 (OnGameOver)", LogManager.LogCategory.PlayStateManager);
                    break;
            }
        }

        #endregion
    }
}