using System;
using System.Collections.Generic;
using System.Linq;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;

namespace InGame
{
    public class ServerManager : MonoBehaviour
    {
        #region 필드 및 프로퍼티

        public string Uuid { get; private set; }
        public string NickName { get; private set; }

        public class PostInfo
        {
            public BackEnd.PostType PostType;
            public string PostInDate;
            public string InDate;
            public string Title;
            public string Content;
            public string Sender;
            public Dictionary<string, int> Items = new Dictionary<string, int>();
        }

        // [최적화] 테이블별 inDate 캐싱 (초기 용량 설정으로 재할당 방지)
        private readonly Dictionary<string, string> m_tableInDate = new Dictionary<string, string>(8);

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

        #region 로그인 및 회원 가입

        public async UniTask<(bool success, string error)> LoginAsync(string id, string pw)
        {
            await m_isInitialized.Task;

            var bro = await BackendAsync(callback => Backend.BMember.CustomLogin(id, pw, callback));

            if (bro.IsSuccess())
            {
                LogManager.Log("로그인 성공", LogManager.LogCategory.ServerManager);
                OnLoginSuccess();
                return (true, null);
            }

            ErroDebug(bro);
            return (false, bro.GetMessage());
        }

        public async UniTask<(bool success, string error)> GuestLoginAsync()
        {
            await m_isInitialized.Task;

            var bro = await BackendAsync(callback => Backend.BMember.GuestLogin(callback));

            if (bro.IsSuccess())
            {
                LogManager.Log("게스트 로그인 성공", LogManager.LogCategory.ServerManager);
                OnLoginSuccess();
                return (true, null);
            }

            // 실패 시 로컬 정보 삭제 후 재시도 가능하도록 유도
            Backend.BMember.DeleteGuestInfo();
            ErroDebug(bro);
            return (false, bro.GetMessage());
        }

        public async UniTask<(bool success, string error)> TokenLoginAsync()
        {
            await m_isInitialized.Task;

            var bro = await BackendAsync(callback => Backend.BMember.LoginWithTheBackendToken(callback));

            if (bro.IsSuccess())
            {
                LogManager.Log("토큰 로그인 성공", LogManager.LogCategory.ServerManager);
                OnLoginSuccess();
                return (true, null);
            }

            LogManager.LogWarning($"토큰 로그인 실패/만료: {bro.GetMessage()}", LogManager.LogCategory.ServerManager);
            return (false, bro.GetMessage());
        }

        public async UniTask<(bool success, string error)> SignUpAsync(string id, string pw, string nickname)
        {
            await m_isInitialized.Task;

            var signUpBro = await BackendAsync(callback => Backend.BMember.CustomSignUp(id, pw, callback));
            if (!signUpBro.IsSuccess())
            {
                ErroDebug(signUpBro);
                return (false, signUpBro.GetMessage());
            }

            LogManager.Log("회원가입 성공. 닉네임 설정 시도...", LogManager.LogCategory.ServerManager);

            var updateBro = await BackendAsync(callback => Backend.BMember.UpdateNickname(nickname, callback));
            if (!updateBro.IsSuccess())
            {
                ErroDebug(updateBro);
                return (false, $"가입 성공, 닉네임 설정 실패: {updateBro.GetMessage()}");
            }

            LogManager.Log("닉네임 설정 성공", LogManager.LogCategory.ServerManager);
            return (true, null);
        }

        private void OnLoginSuccess()
        {
            Uuid = Backend.UID;
            NickName = Backend.UserNickName;

            if (string.IsNullOrEmpty(NickName))
            {
                NickName = Uuid;
            }

            RefreshTokenIfAlive();
        }

        private void RefreshTokenIfAlive()
        {
            // 토큰 갱신 (메인 스레드 부하 적음, 필요시 비동기 래핑 가능)
            var bro = Backend.BMember.IsAccessTokenAlive();
            if (bro.IsSuccess())
            {
                LogManager.Log("액세스 토큰 갱신 시도", LogManager.LogCategory.ServerManager);
                Backend.BMember.RefreshTheBackendToken();
            }
        }

        #endregion

        #region 게임 데이터 저장 및 불러오기

        public async UniTask UploadDataAsync(string tableName, Param param)
        {
            await m_isInitialized.Task;

            BackendReturnObject bro;

            // [최적화] TryGetValue로 딕셔너리 조회 성능 향상
            if (m_tableInDate.TryGetValue(tableName, out string inDate))
            {
                LogManager.Log($"{tableName} 수정 요청 (inDate: {inDate})", LogManager.LogCategory.ServerManager);
                bro = await BackendAsync(callback => Backend.GameData.UpdateV2(tableName, inDate, Backend.UserInDate, param, callback));
            }
            else
            {
                LogManager.Log($"{tableName} 신규 삽입 요청", LogManager.LogCategory.ServerManager);
                bro = await BackendAsync(callback => Backend.GameData.Insert(tableName, param, callback));
                if (bro.IsSuccess())
                {
                    m_tableInDate[tableName] = bro.GetInDate();
                }
            }

            if (!bro.IsSuccess())
            {
                ErroDebug(bro);
                throw new Exception($"데이터 업로드 실패 ({tableName}): {bro.GetMessage()}");
            }

            LogManager.Log($"{tableName} 업로드 완료", LogManager.LogCategory.ServerManager);
        }

        public async UniTask<JsonData> DownloadDataAsync(string tableName)
        {
            await m_isInitialized.Task;

            var bro = await BackendAsync(callback => Backend.GameData.GetMyData(tableName, new Where(), callback));

            if (bro.IsSuccess())
            {
                var gameDataJson = bro.FlattenRows();
                if (gameDataJson.Count > 0)
                {
                    var row = gameDataJson[0];
                    m_tableInDate[tableName] = row["inDate"].ToString();
                    LogManager.Log($"{tableName} 다운로드 완료", LogManager.LogCategory.ServerManager);
                    return row;
                }

                LogManager.Log($"{tableName} 데이터 없음", LogManager.LogCategory.ServerManager);
                return null;
            }

            ErroDebug(bro);
            throw new Exception($"데이터 다운로드 실패 ({tableName}): {bro.GetMessage()}");
        }

        #endregion

        #region 메시지 관련

        public async UniTask LoadMessageAsync()
        {
            await m_isInitialized.Task;

            var bro = await BackendAsync(callback => Backend.UPost.GetPostList(PostType.Coupon, 10, callback));
            if (bro.IsSuccess())
            {
                var json = bro.GetReturnValuetoJSON()["postList"];
                for (var i = 0; i < json.Count; i++)
                {
                    LogManager.Log($"제목: {json[i]["title"]}, 날짜: {json[i]["inDate"]}", LogManager.LogCategory.ServerManager);
                }
            }
        }

        public async UniTask<List<PostInfo>> GetPostListAsync(PostType postType)
        {
            await m_isInitialized.Task;

            var bro = await BackendAsync(callback => Backend.UPost.GetPostList(postType, 100, callback));

            if (!bro.IsSuccess())
            {
                ErroDebug(bro);
                return new List<PostInfo>();
            }

            var json = bro.GetReturnValuetoJSON();
            if (!json.ContainsKey("postList"))
            {
                return new List<PostInfo>();
            }

            JsonData postListJson = json["postList"];
            var postList = new List<PostInfo>(postListJson.Count);

            foreach (JsonData postJson in postListJson)
            {
                var postInfo = new PostInfo
                {
                    PostType = postType,
                    PostInDate = postJson["inDate"].ToString(),
                    InDate = ConvertToCustomDateFormat(postJson["inDate"].ToString()),
                    Title = postJson["title"].ToString(),
                    Content = postJson["content"].ToString(),
                    Sender = postJson.ContainsKey("senderNickname") ? postJson["senderNickname"].ToString() : "운영팀",
                };

                // 아이템 파싱
                if (postJson["items"].IsArray)
                {
                    foreach (JsonData itemJson in postJson["items"])
                    {
                        if (itemJson["chartName"].ToString() == "아이템 차트")
                        {
                            string itemName = itemJson["item"]["itemName"].ToString();
                            if (int.TryParse(itemJson["itemCount"].ToString(), out int itemCount))
                            {
                                if (postInfo.Items.ContainsKey(itemName))
                                    postInfo.Items[itemName] += itemCount;
                                else
                                    postInfo.Items.Add(itemName, itemCount);
                            }
                        }
                    }
                }
                else if (postJson["items"].IsObject)
                {
                    foreach (string key in postJson["items"].Keys)
                    {
                        int val = int.TryParse(postJson["items"][key].ToString(), out int v) ? v : 0;
                        postInfo.Items[key] = val;
                    }
                }

                postList.Add(postInfo);
            }

            LogManager.Log($"우편 {postList.Count}개 로드 완료 ({postType})", LogManager.LogCategory.ServerManager);
            return postList;
        }

        public async UniTask<bool> ReceivePostItemAsync(PostType postType, string postInDate)
        {
            await m_isInitialized.Task;

            var bro = await BackendAsync(callback => Backend.UPost.ReceivePostItem(postType, postInDate, callback));

            if (!bro.IsSuccess())
            {
                ErroDebug(bro);
                return false;
            }

            LogManager.Log($"우편 수령 완료 ({postType})", LogManager.LogCategory.ServerManager);
            return true;
        }

        #endregion

        #region 유틸리티 메서드

        /// <summary>
        /// 뒤끝 비동기 콜백 메서드를 UniTask로 변환하는 래퍼
        /// </summary>
        private UniTask<BackendReturnObject> BackendAsync(Action<Backend.BackendCallback> backendCall)
        {
            var tcs = new UniTaskCompletionSource<BackendReturnObject>();
            backendCall(bro => tcs.TrySetResult(bro));
            return tcs.Task;
        }

        private void ErroDebug(BackendReturnObject bro)
        {
            LogManager.LogError($"[Error] {bro.GetStatusCode()} / {bro.GetErrorCode()} / {bro.GetMessage()}", LogManager.LogCategory.ServerManager);
        }

        private string ConvertToCustomDateFormat(string inDate)
        {
            if (DateTime.TryParse(inDate, out DateTime date))
            {
                return date.ToString("yyyy-MM-dd");
            }
            return inDate;
        }

        #endregion
    }
}