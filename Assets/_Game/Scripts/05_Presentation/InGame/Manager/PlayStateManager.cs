using System;

namespace InGame.Managers
{
    /// <summary>
    /// [설명]: 게임의 전역 상태 흐름(준비 -> 시작 -> 일시정지 -> 종료)을 관리하는 순수 C# 클래스입니다.
    /// GameManager가 소유하며, 상태 변경 시 이벤트를 발행하여 다른 시스템에 알립니다.
    /// </summary>
    public class PlayStateManager
    {
        #region 상태 정의

        /// <summary>
        /// [설명]: 게임의 라이프사이클 상태 목록입니다.
        /// </summary>
        public enum GameState
        {
            /// <summary> 게임 시작 전 초기화 단계 </summary>
            Ready,
            /// <summary> 게임 플레이 진행 중 </summary>
            Play,
            /// <summary> 일시 정지 (메뉴, 옵션 등) </summary>
            Pause,
            /// <summary> 일시 정지 해제 후 복귀 </summary>
            Resume,
            /// <summary> 플레이어 사망 또는 클리어로 인한 종료 </summary>
            GameOver
        }

        #endregion

        #region 이벤트

        /// <summary> 게임 시작 시 발생하는 이벤트 </summary>
        public event Action OnGameStart;

        /// <summary> 게임 일시 정지 시 발생하는 이벤트 </summary>
        public event Action OnGamePause;

        /// <summary> 게임 재개 시 발생하는 이벤트 </summary>
        public event Action OnGameResume;

        /// <summary> 게임 종료 시 발생하는 이벤트 </summary>
        public event Action OnGameOver;

        #endregion

        #region 내부 필드 및 프로퍼티

        private GameState m_currentState = GameState.Ready;

        /// <summary>
        /// [설명]: 게임의 현재 상태를 반환합니다.
        /// 상태 변경은 제공된 메서드를 통해서만 가능합니다.
        /// </summary>
        public GameState PlayState
        {
            get => m_currentState;
            private set
            {
                if (m_currentState == value)
                {
                    return;
                }

                m_currentState = value;
                NotifyStateChanged(m_currentState);
            }
        }

        /// <summary> [설명]: 현재 게임이 활성 상태(플레이 중 또는 재개 상태)인지 확인합니다. </summary>
        public bool IsPlaying => PlayState == GameState.Play || PlayState == GameState.Resume;

        #endregion

        #region 상태 제어 메서드

        /// <summary>
        /// [설명]: 게임을 'Ready' 상태에서 'Play' 상태로 전환합니다.
        /// </summary>
        public void StartGame()
        {
            PlayState = GameState.Play;
        }

        /// <summary>
        /// [설명]: 게임을 일시 정지합니다.
        /// </summary>
        public void Pause()
        {
            if (PlayState == GameState.GameOver)
            {
                return;
            }

            PlayState = GameState.Pause;
        }

        /// <summary>
        /// [설명]: 일시 정지된 게임을 다시 재개합니다.
        /// </summary>
        public void Resume()
        {
            if (PlayState == GameState.GameOver)
            {
                return;
            }

            PlayState = GameState.Resume;
        }

        /// <summary>
        /// [설명]: 게임을 종료 상태로 전환합니다.
        /// </summary>
        public void GameOver()
        {
            PlayState = GameState.GameOver;
        }

        #endregion

        #region 내부 로직

        /// <summary>
        /// [설명]: 상태 변경에 따른 이벤트를 호출하고 로그를 기록합니다.
        /// </summary>
        private void NotifyStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.Play:
                    LogManager.Log("[PlayStateManager] 게임 시작", LogManager.LogCategory.PlayStateManager);
                    OnGameStart?.Invoke();
                    break;

                case GameState.Pause:
                    LogManager.Log("[PlayStateManager] 일시 정지", LogManager.LogCategory.PlayStateManager);
                    OnGamePause?.Invoke();
                    break;

                case GameState.Resume:
                    LogManager.Log("[PlayStateManager] 게임 재개", LogManager.LogCategory.PlayStateManager);
                    OnGameResume?.Invoke();
                    break;

                case GameState.GameOver:
                    LogManager.Log("[PlayStateManager] 게임 오버", LogManager.LogCategory.PlayStateManager);
                    OnGameOver?.Invoke();
                    break;

                case GameState.Ready:
                    break;
            }
        }

        #endregion
    }
}