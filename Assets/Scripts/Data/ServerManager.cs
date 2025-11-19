using System;
using System.Collections.Generic;
using System.Linq;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;

namespace DogGuns_Games
{
    public class ServerManager : MonoBehaviour
    {
        #region 필드 및 프로퍼티

        public string Uuid { get; private set; }
        public string NickName { get; private set; }

        public class PostInfo
        {
            public BackEnd.PostType postType;
            public string postInDate;
            public string inDate;
            public string title;
            public string content;
            public string sender;
            public Dictionary<string, int> items = new Dictionary<string, int>();
        }

        private readonly Dictionary<string, string> m_tableInDate = new Dictionary<string, string>(4);
        private readonly UniTaskCompletionSource<bool> m_isInitialized = new UniTaskCompletionSource<bool>();
        private const int InitTimeoutSec = 10;

        #endregion

        #region 싱글톤 패턴

        private static ServerManager instance;

        public static ServerManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<ServerManager>();
                    if (instance == null)
                    {
                        var container = new GameObject("ServerManager");
                        instance = container.AddComponent<ServerManager>();
                    }
                }
                return instance;
            }
        }

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeAsync().Forget();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region 초기화 메서드

        private async UniTask InitializeAsync()
        {
            bool isSuccess = false;
            try
            {
                var bro = await InitializeBackendAsync()
                                .Timeout(TimeSpan.FromSeconds(InitTimeoutSec));

                if (bro.IsSuccess())
                {
                    LogManager.Log("뒤끝 SDK 비동기 초기화 성공", LogManager.LogCategory.ServerManager);
                    isSuccess = true;
                }
                else
                {
                    LogManager.LogError($"뒤끝 SDK 비동기 초기화 실패: {bro}", LogManager.LogCategory.ServerManager);
                }
            }
            catch (TimeoutException)
            {
                LogManager.LogError($"뒤끝 SDK 초기화 시간 초과({InitTimeoutSec}초). 네트워크 연결을 확인해주세요.", LogManager.LogCategory.ServerManager);
            }
            catch (Exception e)
            {
                LogManager.LogError($"뒤끝 SDK 초기화 중 예외 발생: {e.Message}", LogManager.LogCategory.ServerManager);
            }
            finally
            {
                m_isInitialized.TrySetResult(isSuccess);
            }
        }

        private UniTask<BackendReturnObject> InitializeBackendAsync()
        {
            var setting = new BackendCustomSetting();
            return UniTask.Create(() => UniTask.FromResult(Backend.Initialize(setting)));
        }

        #endregion

        #region 로그인 및 회원 가입

        public async UniTask<(bool success, string error)> LoginAsync(string id, string pw)
        {
            await m_isInitialized.Task;

            // callback의 타입이 BackendCallback으로 정확히 전달됩니다.
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

            LogManager.LogWarning($"토큰 로그인 실패: {bro.GetMessage()}", LogManager.LogCategory.ServerManager);
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

            LogManager.Log("회원가입 성공, 닉네임 설정 시도", LogManager.LogCategory.ServerManager);

            var updateBro = await BackendAsync(callback => Backend.BMember.UpdateNickname(nickname, callback));
            if (!updateBro.IsSuccess())
            {
                ErroDebug(updateBro);
                return (false, $"가입은 성공했으나 닉네임 설정 실패: {updateBro.GetMessage()}");
            }

            LogManager.Log("닉네임 설정 성공", LogManager.LogCategory.ServerManager);
            return (true, null);
        }

        private void OnLoginSuccess()
        {
            Uuid = Backend.UID;
            NickName = Backend.UserNickName;
            RefreshTokenIfAlive();
        }

        private void RefreshTokenIfAlive()
        {
            var bro = Backend.BMember.IsAccessTokenAlive();
            if (bro.IsSuccess())
            {
                LogManager.Log("액세스 토큰 유효, 갱신을 시도합니다.", LogManager.LogCategory.ServerManager);
                Backend.BMember.RefreshTheBackendToken();
            }
        }

        #endregion

        #region 게임 데이터 저장 및 불러오기

        public async UniTask UploadDataAsync(string tableName, Param param)
        {
            await m_isInitialized.Task;

            BackendReturnObject bro;
            if (m_tableInDate.TryGetValue(tableName, out string inDate))
            {
                LogManager.Log($"{tableName} 데이터 수정 요청 (inDate: {inDate})", LogManager.LogCategory.ServerManager);
                bro = await BackendAsync(callback => Backend.GameData.UpdateV2(tableName, inDate, Backend.UserInDate, param, callback));
            }
            else
            {
                LogManager.Log($"{tableName} 새 데이터 삽입 요청", LogManager.LogCategory.ServerManager);
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
            
            LogManager.Log($"{tableName} 테이블 데이터 업로드 성공", LogManager.LogCategory.ServerManager);
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
                    LogManager.Log($"{tableName} 데이터 다운로드 성공", LogManager.LogCategory.ServerManager);
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
                    LogManager.Log($"제목 : {json[i]["title"]}", LogManager.LogCategory.ServerManager);
                    LogManager.Log($"inDate : {json[i]["inDate"]}", LogManager.LogCategory.ServerManager);
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
                LogManager.LogWarning($"'{postType}' 타입의 우편이 없습니다.", LogManager.LogCategory.ServerManager);
                return new List<PostInfo>();
            }

            JsonData postListJson = json["postList"];
            var postList = new List<PostInfo>(postListJson.Count);

            foreach (JsonData postJson in postListJson)
            {
                var postInfo = new PostInfo
                {
                    postType = postType,
                    postInDate = postJson["inDate"].ToString(),
                    inDate = ConvertToCustomDateFormat(postJson["inDate"].ToString()),
                    title = postJson["title"].ToString(),
                    content = postJson["content"].ToString(),
                    sender = postJson.ContainsKey("senderNickname") ? postJson["senderNickname"].ToString() : "운영팀",
                };

                if (postJson["items"].IsArray)
                {
                    foreach (JsonData itemJson in postJson["items"])
                    {
                        if (itemJson["chartName"].ToString() == "아이템 차트")
                        {
                            string itemName = itemJson["item"]["itemName"].ToString();
                            if (int.TryParse(itemJson["itemCount"].ToString(), out int itemCount))
                            {
                                if (postInfo.items.ContainsKey(itemName))
                                    postInfo.items[itemName] += itemCount;
                                else
                                    postInfo.items.Add(itemName, itemCount);
                            }
                        }
                    }
                }
                else if (postJson["items"].IsObject)
                {
                    foreach (string key in postJson["items"].Keys)
                    {
                        int val = int.TryParse(postJson["items"][key].ToString(), out int v) ? v : 0;
                        postInfo.items[key] = val;
                    }
                }
                
                postList.Add(postInfo);
            }

            LogManager.Log($"총 {postList.Count}개의 우편({postType})을 불러왔습니다.", LogManager.LogCategory.ServerManager);
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

            LogManager.Log($"우편 수령 성공 ({postType}, {postInDate})", LogManager.LogCategory.ServerManager);
            return true;
        }

        #endregion

        #region 유틸리티 메서드

        /// <summary>
        /// [수정됨] Action<BackendReturnObject> -> Backend.BackendCallback 으로 타입 변경
        /// </summary>
        private UniTask<BackendReturnObject> BackendAsync(Action<Backend.BackendCallback> backendCall)
        {
            var tcs = new UniTaskCompletionSource<BackendReturnObject>();
            // C# 컴파일러가 여기서 람다식을 BackendCallback 델리게이트로 자동 변환합니다.
            backendCall(bro => tcs.TrySetResult(bro));
            return tcs.Task;
        }

        private void ErroDebug(BackendReturnObject bro)
        {
            LogManager.LogError($"StatusCode: {bro.GetStatusCode()}", LogManager.LogCategory.ServerManager);
            LogManager.LogError($"ErrorCode: {bro.GetErrorCode()}", LogManager.LogCategory.ServerManager);
            LogManager.LogError($"Message: {bro.GetMessage()}", LogManager.LogCategory.ServerManager);
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