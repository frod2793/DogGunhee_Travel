using System;
using UnityEngine;

namespace Vamser_like
{
    public class PlayStateManager : MonoBehaviour
    {
        public static Action OnGameStart;
        public static Action OnGamePause;
        public static Action OnGameResume;
        public static Action OnGameOver;
        
        public static PlayStateManager instance;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
        }
        
        /// <summary>
        /// 현재 게임이 플레이 상태인지 여부를 반환합니다.
        /// </summary>
        public bool IsPlaying => PlayState == GameState.Play;

        public enum GameState
        {
            Play,
            Pause,
            Resume,
            GameOver
        }

        private GameState _playState;

        public GameState PlayState
        {   
            get => _playState;
            set
            {
                _playState = value;
                SetMobState(_playState);
            }
        }

        private void Start()
        {
           // 게임 시작은 VamserLikeGameManager에서 명시적으로 호출하도록 변경
           // 이 클래스는 더 이상 스스로 게임 시작을 호출하지 않습니다.
        }
        public void StartGame()
        {
            PlayState = GameState.Play;
        }

        public void Pause()
        {
            if (PlayState == GameState.GameOver) return;
            PlayState = GameState.Pause;
        }

        public void Resume()
        {
            if (PlayState == GameState.GameOver) return;
            PlayState = GameState.Resume;
        }

        public void GameOver()
        {
            PlayState = GameState.GameOver;
        }

        private void SetMobState(GameState newState)
        {
            switch (newState)
            {
                case GameState.Play:
                    OnGameStart?.Invoke();
                    LogManager.Log("OnGameStart", LogManager.LogCategory.PlayStateManager);
                    break;
                case GameState.Pause:
                case GameState.Resume:
                    if (newState == GameState.Pause) OnGamePause?.Invoke();
                    else OnGameResume?.Invoke();
                    LogManager.Log(newState == GameState.Pause ? "OnGamePause" : "OnGameResume", LogManager.LogCategory.PlayStateManager);
                    break;
                case GameState.GameOver:
                    OnGameOver?.Invoke();
                    LogManager.Log("OnGameOver", LogManager.LogCategory.PlayStateManager);
                    break;
            }
        }
    }
}