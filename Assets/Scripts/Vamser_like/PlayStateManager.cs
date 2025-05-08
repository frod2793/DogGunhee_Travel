using System;
using UnityEngine;

namespace DogGuns_Games.vamsir
{
    public class PlayStateManager : MonoBehaviour
    {
        public static Action OnGameStart;
        public static Action OnGamePause;
        public static Action OnGameResume;
        public static Action OnGameOver;

        public static PlayStateManager instance;

        public bool isPlay = false;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
        }

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

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                // 플레이 모드에서만 SetMobState 호출
                SetMobState(PlayState);
            }
        }

        private void Start()
        {
           Invoke(nameof(Gamestart), 1f);
        }
        
        private void Gamestart()
        {
            PlayState = GameState.Play;
        }

        public void SetMobState(GameState state)
        {
            switch (state)
            {
                case GameState.Play:
                    OnGameStart?.Invoke();
                    Debug.Log( "OnGameStart");
                    break;
                case GameState.Pause:
                    OnGamePause?.Invoke();
                    Debug.Log( "OnGamePause");
                    break;
                case GameState.Resume:
                    OnGameResume?.Invoke();
                    Debug.Log( "OnGameResume");
                    break;
                case GameState.GameOver:
                    OnGameOver?.Invoke();
                    Debug.Log( "OnGameOver");
                    break;
            }
        }
    }
}