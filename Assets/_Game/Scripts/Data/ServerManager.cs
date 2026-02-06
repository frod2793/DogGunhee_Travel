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
    /// 기존 API 호환성을 유지하면서 내부적으로 서비스를 호출합니다.
    /// </summary>
    public class ServerManager : MonoBehaviour
    {
        #region 서비스 프로퍼티

        /// <summary>
        /// 인증(로그인, 회원가입) 관련 서비스입니다.
        /// </summary>
        public AuthenticationService Auth { get; private set; }

        /// <summary>
        /// 게임 데이터 저장/로드 관련 서비스입니다.
        /// </summary>
        public GameDataService GameData { get; private set; }

        /// <summary>
        /// 우편함 관련 서비스입니다.
        /// </summary>
        public PostService Post { get; private set; }

        #endregion

        #region 기존 API 호환용 프로퍼티

        /// <summary>
        /// 현재 로그인된 사용자의 UUID (호환성 유지)
        /// </summary>
        public string Uuid => Auth?.Uuid;

        /// <summary>
        /// 현재 로그인된 사용자의 닉네임 (호환성 유지)
        /// </summary>
        public string NickName => Auth?.NickName;

        #endregion

        #region 내부 필드

        // 초기화 상태 추적 (스레드 안전성 확보)
        private readonly UniTaskCompletionSource<bool> m_isInitialized = new UniTaskCompletionSource<bool>();

        // 타임아웃 상수
        private const int k_InitTimeoutSec = 10;

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

            // [Fix] 인스턴스가 프로퍼티에 의해 먼저 설정된 경우에도 서비스 초기화 보장
            if (Auth == null)
            {
                Auth = new AuthenticationService(m_isInitialized);
                GameData = new GameDataService(m_isInitialized);
                Post = new PostService(m_isInitialized);
            }
        }

        private void Start()
        {
            // 초기화 시작 (Fire-and-forget)
            InitializeAsync().Forget();
        }

        #endregion

        #region 초기화 메서드

        private async UniTask InitializeAsync()
        {
            bool isSuccess = false;
            try
            {
                // [최적화] Timeout 확장 메서드로 타임아웃 로직 간소화
                var bro = await InitializeBackendAsync()
                                .Timeout(TimeSpan.FromSeconds(k_InitTimeoutSec));

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
                LogManager.LogError($"뒤끝 SDK 초기화 시간 초과({k_InitTimeoutSec}초). 네트워크를 확인해주세요.", LogManager.LogCategory.ServerManager);
            }
            catch (Exception e)
            {
                LogManager.LogError($"뒤끝 SDK 초기화 중 치명적 오류: {e.Message}", LogManager.LogCategory.ServerManager);
            }
            finally
            {
                // 결과 설정 (성공 여부와 관계없이 대기 중인 작업들의 Lock 해제)
                m_isInitialized.TrySetResult(isSuccess);
            }
        }

        private UniTask<BackendReturnObject> InitializeBackendAsync()
        {
            var bro = Backend.Initialize();

            // 동기 결과를 UniTask로 감싸서 반환 (오버헤드 최소화)
            return UniTask.FromResult(bro);
        }

        #endregion

        #region 기존 API 호환 레이어 (Facade 패턴)

        // 아래 메서드들은 기존 코드와의 호환성을 위해 유지됩니다.
        // 내부적으로 각 서비스의 메서드를 호출합니다.

        /// <summary>
        /// 커스텀 로그인 (기존 API 호환)
        /// </summary>
        public UniTask<(bool success, string error)> LoginAsync(string id, string pw)
            => Auth.LoginAsync(id, pw);

        /// <summary>
        /// 게스트 로그인 (기존 API 호환)
        /// </summary>
        public UniTask<(bool success, string error)> GuestLoginAsync()
            => Auth.GuestLoginAsync();

        /// <summary>
        /// 토큰 로그인 (기존 API 호환)
        /// </summary>
        public UniTask<(bool success, string error)> TokenLoginAsync()
            => Auth.TokenLoginAsync();

        /// <summary>
        /// 회원가입 (기존 API 호환)
        /// </summary>
        public UniTask<(bool success, string error)> SignUpAsync(string id, string pw, string nickname)
            => Auth.SignUpAsync(id, pw, nickname);

        /// <summary>
        /// 데이터 업로드 (기존 API 호환)
        /// </summary>
        public UniTask UploadDataAsync(string tableName, Param param)
            => GameData.UploadDataAsync(tableName, param);

        /// <summary>
        /// 데이터 다운로드 (기존 API 호환)
        /// </summary>
        public UniTask<JsonData> DownloadDataAsync(string tableName)
            => GameData.DownloadDataAsync(tableName);

        /// <summary>
        /// 우편 메시지 로드 (기존 API 호환)
        /// </summary>
        public UniTask LoadMessageAsync()
            => Post.LoadMessageAsync();

        /// <summary>
        /// 우편 리스트 조회 (기존 API 호환)
        /// </summary>
        public UniTask<List<PostService.PostInfo>> GetPostListAsync(PostType postType)
            => Post.GetPostListAsync(postType);

        /// <summary>
        /// 우편 아이템 수령 (기존 API 호환)
        /// </summary>
        public UniTask<bool> ReceivePostItemAsync(PostType postType, string postInDate)
            => Post.ReceivePostItemAsync(postType, postInDate);

        #endregion
    }
}