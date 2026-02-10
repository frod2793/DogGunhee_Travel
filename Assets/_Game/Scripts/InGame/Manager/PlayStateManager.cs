using System;

namespace InGame.Manager
{
    /// <summary>
    /// 게임의 전역 상태 흐름(준비 -> 시작 -> 일시정지 -> 종료)을 관리하는 순수 C# 클래스입니다.
    /// <br/> GameManager가 소유하며, 상태 변경 시 이벤트를 발행하여 다른 시스템에 알립니다.
    /// </summary>
    public class PlayStateManager
    {
        #region 1. 상태 정의 (Enums)

        /// <summary>
        /// 게임의 라이프사이클 상태 목록입니다.
        /// </summary>
        public enum GameState
        {
            /// <summary>게임 시작 전 초기화 단계</summary>
            Ready,      
            /// <summary>게임 플레이 진행 중</summary>
            Play,       
            /// <summary>일시 정지 (메뉴, 옵션 등)</summary>
            Pause,      
            /// <summary>일시 정지 해제 후 복귀</summary>
            Resume,     
            /// <summary>플레이어 사망 또는 클리어로 인한 종료</summary>
            GameOver    
        }

        #endregion

        #region 2. 이벤트 (Events)

        // 상태 변경 시 외부(UI, Spawner 등)에 알리기 위한 이벤트
        public event Action OnGameStart;
        public event Action OnGamePause;
        public event Action OnGameResume;
        public event Action OnGameOver;

        #endregion

        #region 3. 내부 필드 및 프로퍼티

        // 실제 상태를 저장하는 백킹 필드
        private GameState m_currentState = GameState.Ready;

        /// <summary>
        /// 게임의 현재 상태를 반환합니다. 
        /// <br/> 상태 변경은 제공된 메서드(StartGame, Pause 등)를 통해서만 가능합니다.
        /// </summary>
        public GameState PlayState
        {   
            get => m_currentState;
            private set 
            {
                // 상태가 동일하면 이벤트를 중복 발생시키지 않음
                if (m_currentState == value) return;
                
                m_currentState = value;
                NotifyStateChanged(m_currentState);
            }
        }

        /// <summary>
        /// 현재 게임이 활성 상태(플레이 중 또는 재개 상태)인지 확인합니다.
        /// </summary>
        public bool IsPlaying => PlayState == GameState.Play || PlayState == GameState.Resume;

        #endregion

        #region 4. 상태 제어 메서드 (State Control)

        /// <summary>
        /// 게임을 'Ready' 상태에서 'Play' 상태로 전환하고 시작 이벤트를 호출합니다.
        /// </summary>
        public void StartGame()
        {
            // 이미 게임오버 상태라면 재시작 로직(Reset)이 선행되어야 함 (필요 시 로직 추가)
            PlayState = GameState.Play;
        }

        /// <summary>
        /// 게임을 일시 정지합니다.
        /// </summary>
        public void Pause()
        {
            // 게임 오버 상태에서는 일시정지 불가
            if (PlayState == GameState.GameOver) return;
            
            PlayState = GameState.Pause;
        }

        /// <summary>
        /// 일시 정지된 게임을 다시 재개합니다.
        /// </summary>
        public void Resume()
        {
            if (PlayState == GameState.GameOver) return;

            // Resume 상태로 변경하여 이벤트를 발생시킨 후, 논리적으로는 다시 Play 상태로 간주될 수 있음
            PlayState = GameState.Resume;
        }

        /// <summary>
        /// 게임을 종료 상태로 전환합니다. (플레이어 사망 등)
        /// </summary>
        public void GameOver()
        {
            PlayState = GameState.GameOver;
        }

        #endregion

        #region 5. 내부 로직 (Private Logic)

        /// <summary>
        /// 상태 변경에 따른 이벤트를 호출하고 로그를 기록합니다.
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
                    // Ready 상태는 별도 이벤트 없음
                    break;
            }
        }

        #endregion
    }
}