using System;
using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using InGame.Services;
using LitJson;
using UnityEngine;

namespace InGame
{
    /// <summary>
    /// 서버 통신을 관리하는 Facade 클래스입니다.
    /// 실제 비즈니스 로직은 각 서비스(Auth, GameData, Post)에 위임합니다.
    /// </summary>
    public class ServerManager : MonoBehaviour
    {
        #region 서비스 프로퍼티

        private IAuthenticationService m_auth;
        private IGameDataService m_gameData;
        private IPostService m_post;

        public IAuthenticationService Auth 
        { 
            get => m_auth ??= new AuthenticationService(m_isInitialized);
            private set => m_auth = value;
        }
        
        public IGameDataService GameData 
        { 
            get => m_gameData ??= new GameDataService(m_isInitialized);
            private set => m_gameData = value;
        }
        
        public IPostService Post 
        { 
            get => m_post ??= new PostService(m_isInitialized);
            private set => m_post = value;
        }

        #endregion

        #region 싱글톤 패턴

        private static ServerManager m_instance;
        public static ServerManager Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = FindFirstObjectByType<ServerManager>();
                    if (m_instance == null)
                    {
                        var container = new GameObject("ServerManager");
                        m_instance = container.AddComponent<ServerManager>();
                    }
                }
                return m_instance;
            }
        }

        #endregion

        #region 내부 필드
        private readonly UniTaskCompletionSource<bool> m_isInitialized = new UniTaskCompletionSource<bool>();
        private const int k_InitTimeoutSec = 10;
        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            if (m_instance == null)
            {
                m_instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (m_instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            InitializeAsync().Forget();
        }

        #endregion

        #region 초기화 메서드

        private async UniTask InitializeAsync()
        {
            bool isSuccess = false;
            try
            {
                var bro = await InitializeBackendAsync().Timeout(TimeSpan.FromSeconds(k_InitTimeoutSec));

                if (bro.IsSuccess())
                {
                    LogManager.Log("뒤끝 SDK 초기화 성공", LogManager.LogCategory.ServerManager);
                    isSuccess = true;
                }
                else
                {
                    LogManager.LogError($"뒤끝 SDK 초기화 실패: {bro}", LogManager.LogCategory.ServerManager);
                }
            }
            catch (TimeoutException)
            {
                LogManager.LogError($"뒤끝 SDK 초기화 시간 초과({k_InitTimeoutSec}초)", LogManager.LogCategory.ServerManager);
            }
            catch (Exception e)
            {
                LogManager.LogError($"뒤끝 SDK 초기화 중 오류: {e.Message}", LogManager.LogCategory.ServerManager);
            }
            finally
            {
                m_isInitialized.TrySetResult(isSuccess);
            }
        }

        private UniTask<BackendReturnObject> InitializeBackendAsync()
        {
            return UniTask.FromResult(Backend.Initialize());
        }

        #endregion

        #region 기존 API 호환 레이어 (Facade)

        public UniTask<(bool success, string error)> LoginAsync(string id, string pw) => Auth.LoginAsync(id, pw);
        public UniTask<(bool success, string error)> GuestLoginAsync() => Auth.GuestLoginAsync();
        public UniTask<(bool success, string error)> TokenLoginAsync() => Auth.TokenLoginAsync();
        public UniTask<(bool success, string error)> SignUpAsync(string id, string pw, string nickname) => Auth.SignUpAsync(id, pw, nickname);

        public UniTask UploadDataAsync(string tableName, Param param) => GameData.UploadDataAsync(tableName, param);
        public UniTask<JsonData> DownloadDataAsync(string tableName) => GameData.DownloadDataAsync(tableName);

        public UniTask LoadMessageAsync() => Post.LoadMessageAsync();
        public UniTask<List<PostService.PostInfo>> GetPostListAsync(PostType postType) => Post.GetPostListAsync(postType);
        public UniTask<bool> ReceivePostItemAsync(PostType postType, string postInDate) => Post.ReceivePostItemAsync(postType, postInDate);

        public string Uuid => Auth?.Uuid;
        public string NickName => Auth?.NickName;

        #endregion
    }
}