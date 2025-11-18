using System;
using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using System.Linq;
using UnityEngine;

namespace DogGuns_Games
{
    public class ServerManager : MonoBehaviour
    {
        #region 필드 및 프로퍼티

        public string Uuid { get; private set; }
        public string NickName { get; private set; }

        /// <summary>
        /// 서버에서 받아온 우편 정보를 담는 구조체입니다.
        /// </summary>
        public struct PostInfo
        {
            public BackEnd.PostType postType;
            public string postInDate;
            public string inDate;
            public string title;
            public string content;
            public string sender;
            // key: itemChart, value: itemValue
            public Dictionary<string, int> items;
        }

        private readonly Dictionary<string, string> m_tableInDate = new Dictionary<string, string>();

        #endregion

        #region 싱글톤 패턴 (DontDestroyOnLoad)

        private static ServerManager instance;
        private static readonly object padlock = new object();

        public static ServerManager Instance
        {
            get
            {
                lock (padlock)
                {
                    if (instance == null)
                    {
                        instance = FindAnyObjectByType<ServerManager>();
                        if (instance == null)
                        {
                            var container = new GameObject("ServerManager");
                            instance = container.AddComponent<ServerManager>();
                        }
                    }

                    return instance;
                }
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
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
            Init ();
        }

        #endregion

        #region 초기화 메서드

        /// <summary>
        ///     뒤끝 서버 초기화
        /// </summary>
        private void Init()
        {
            var bro = Backend.Initialize();
            if (bro.IsSuccess())
            {
                LogManager.Log("뒤끝 초기화 성공", LogManager.LogCategory.ServerManager);
            }
            else
            {
                LogManager.LogError("뒤끝 초기화 실패: " + bro, LogManager.LogCategory.ServerManager);
            }
        }

        #endregion

        #region 로그인 및 회원 가입 (비동기)

        /// <summary>
        ///     로그인
        /// </summary>
        public async UniTask<(string nickname, string uuid)> LoginAsync(string id, string pw)
        {
            var bro = await BackendAsync(callback => Backend.BMember.CustomLogin(id, pw, callback));

            if (bro.IsSuccess())
            {
                LogManager.Log("로그인 성공", LogManager.LogCategory.ServerManager);
                OnLoginSuccess();
                return (NickName, Uuid);
            }
            else
            {
                ErroDebug(bro);
                throw new Exception($"로그인 실패: {bro.GetMessage()}");
            }
        }

        /// <summary>
        ///     게스트 로그인
        /// </summary>
        public async UniTask<(string nickname, string uuid)> GuestLoginAsync()
        {
            var bro = await BackendAsync(callback => Backend.BMember.GuestLogin(callback));

            if (bro.IsSuccess())
            {
                LogManager.Log("게스트 로그인 성공", LogManager.LogCategory.ServerManager);
                OnLoginSuccess();
                return (NickName, Uuid);
            }
            else
            {
                Backend.BMember.DeleteGuestInfo();
                ErroDebug(bro);
                throw new Exception($"게스트 로그인 실패: {bro.GetMessage()}");
            }
        }

        /// <summary>
        ///     토큰 로그인
        /// </summary>
        public async UniTask<(bool success, string nickname, string uuid)> TokenLoginAsync()
        {
            var bro = await BackendAsync(callback => Backend.BMember.LoginWithTheBackendToken(callback));

            if (bro.IsSuccess())
            {
                LogManager.Log("토큰 로그인 성공", LogManager.LogCategory.ServerManager);
                OnLoginSuccess();
                return (true, NickName, Uuid);
            }
            else
            {
                LogManager.LogWarning($"토큰 로그인 실패: {bro.GetMessage()}", LogManager.LogCategory.ServerManager);
                return (false, null, null);
            }
        }

        /// <summary>
        ///     회원 가입
        /// </summary>
        public async UniTask SignUpAsync(string id, string pw, string nickname)
        {
            var signUpBro = await BackendAsync(callback => Backend.BMember.CustomSignUp(id, pw, callback));
            if (!signUpBro.IsSuccess())
            {
                ErroDebug(signUpBro);
                throw new Exception($"회원가입 실패: {signUpBro.GetMessage()}");
            }
            LogManager.Log("회원가입 성공", LogManager.LogCategory.ServerManager);

            var updateBro = await BackendAsync(callback => Backend.BMember.UpdateNickname(nickname, callback));
            if (!updateBro.IsSuccess())
            {
                ErroDebug(updateBro);
                throw new Exception($"닉네임 설정 실패: {updateBro.GetMessage()}");
            }
            LogManager.Log("닉네임 설정 성공", LogManager.LogCategory.ServerManager);
        }

        /// <summary>
        /// 로그인 성공 시 공통으로 처리할 로직입니다.
        /// </summary>
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

        #region 게임 데이터 저장 및 불러오기 (비동기)

        /// <summary>
        /// 지정된 테이블의 데이터를 서버에 업로드합니다. (Insert or Update)
        /// </summary>
        public async UniTask UploadDataAsync(string tableName, Param param)
        {
            BackendReturnObject bro;
            if (m_tableInDate.ContainsKey(tableName))
            {
                string inDate = m_tableInDate[tableName];
                LogManager.Log($"{tableName} 테이블의 데이터 수정을 요청합니다. (inDate: {inDate})", LogManager.LogCategory.ServerManager);
                bro = await BackendAsync(callback => Backend.GameData.UpdateV2(tableName, inDate, Backend.UserInDate, param, callback));
            }
            else
            {
                LogManager.Log($"{tableName} 테이블에 새 데이터 삽입을 요청합니다.");
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

        /// <summary>
        /// 지정된 테이블에서 내 데이터를 다운로드합니다.
        /// </summary>
        public async UniTask<JsonData> DownloadDataAsync(string tableName)
        {
            var bro = await BackendAsync(callback => Backend.GameData.GetMyData(tableName, new Where(), callback));
            if (bro.IsSuccess())
            {
                var gameDataJson = bro.FlattenRows();
                if (gameDataJson.Count > 0)
                {
                    m_tableInDate[tableName] = gameDataJson[0]["inDate"].ToString();
                    LogManager.Log($"{tableName} 테이블 데이터 다운로드 성공", LogManager.LogCategory.ServerManager);
                    return gameDataJson[0];
                }
                else
                {
                    LogManager.Log($"{tableName} 테이블에 데이터가 없습니다.", LogManager.LogCategory.ServerManager);
                    return null;
                }
            }
            else
            {
                ErroDebug(bro);
                throw new Exception($"데이터 다운로드 실패 ({tableName}): {bro.GetMessage()}");
            }
        }

        #endregion

        #region 메시지 관련

        /// <summary>
        ///     우편함 불러오기 (동기)
        /// </summary>
        public void LoadMessage()
        {
            var bro = Backend.UPost.GetPostList(PostType.Coupon, 10);
            var json = bro.GetReturnValuetoJSON()["postList"];

            for (var i = 0; i < json.Count; i++)
            {
                LogManager.Log("제목 : " + json[i]["title"], LogManager.LogCategory.ServerManager);
                LogManager.Log("inDate : " + json[i]["inDate"], LogManager.LogCategory.ServerManager);
            }
        }

        /// <summary>
        ///     우편함 불러오기 (비동기)
        /// </summary>
        public async UniTask LoadMessageAsync()
        {
            var bro = await BackendAsync(callback => Backend.UPost.GetPostList(PostType.Coupon, 10, callback));
            if (bro.IsSuccess())
            {
                var json = bro.GetReturnValuetoJSON()["postList"];
                for (var i = 0; i < json.Count; i++)
                {
                    LogManager.Log("제목 : " + json[i]["title"], LogManager.LogCategory.ServerManager);
                    LogManager.Log("inDate : " + json[i]["inDate"], LogManager.LogCategory.ServerManager);
                }
            }
        }

        /// <summary>
        ///     우편 하나 수령하기
        /// </summary>
        public void GetReward()
        {
            var type = PostType.Admin;

            //우편 리스트 불러오기
            var bro = Backend.UPost.GetPostList(type, 100);
            var json = bro.GetReturnValuetoJSON()["postItems"];

            //우편 리스트중 0번째 우편의 inDate 가져오기
            var recentPostIndate = json[0]["inDate"].ToString();

            // 동일한 PostType의 우편 수령하기
            Backend.UPost.ReceivePostItem(type, recentPostIndate);
        }

        public void GetRewardAll()
        {
            var receiveBro = Backend.UPost.ReceivePostItemAll(PostType.Admin);
            if (receiveBro.IsSuccess() == false)
            {
                LogManager.LogError($"우편 모두 수령하기 중 에러가 발생하였습니다. : {receiveBro}", LogManager.LogCategory.ServerManager);
                return;
            }

            foreach (JsonData postItemJson in receiveBro.GetReturnValuetoJSON()["postItems"])
                for (var j = 0; j < postItemJson.Count; j++)
                    if (!postItemJson[j].ContainsKey("item"))
                    {
                    }
        }

        /// <summary>
        /// 지정된 타입의 우편 목록을 비동기적으로 불러옵니다.
        /// </summary>
        /// <param name="postType">불러올 우편 타입</param>
        /// <returns>우편 정보 리스트</returns>
        public async UniTask<List<PostInfo>> GetPostListAsync(PostType postType)
        {
            var bro = await BackendAsync(callback => Backend.UPost.GetPostList(postType, 100, callback));

            if (!bro.IsSuccess())
            {
                ErroDebug(bro);
                throw new Exception($"우편 목록({postType}) 불러오기 실패: {bro.GetMessage()}");
            }

            var postList = new List<PostInfo>();
            var json = bro.GetReturnValuetoJSON();

            if (!json.ContainsKey("postList"))
            {
                LogManager.LogWarning($"'{postType}' 타입의 우편이 없습니다.", LogManager.LogCategory.ServerManager);
                return postList;
            }

            var postListJson = json["postList"];

            foreach (JsonData postJson in postListJson)
            {
                var postInfo = new PostInfo
                {
                    postType = postType,
                    postInDate = postJson["inDate"].ToString(),
                    inDate = ConvertToCustomDateFormat(postJson["inDate"].ToString()),
                    title = postJson["title"].ToString(),
                    content = postJson["content"].ToString(),
                    sender = postJson.Keys.Contains("senderNickname") ? postJson["senderNickname"].ToString() : "운영팀",
                    items = new Dictionary<string, int>()
                };

                if (postJson["items"].IsArray)
                {
                    foreach (JsonData itemJson in postJson["items"])
                    {
                        // 제공해주신 코드에 따라 "아이템 차트"만 허용합니다.
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
                    postInfo.items = postJson["items"].Keys.ToDictionary(
                        key => key,
                        key => int.TryParse(postJson["items"][key].ToString(), out int val) ? val : 0);
                }
                
                postList.Add(postInfo);
            }
            LogManager.Log($"총 {postList.Count}개의 우편({postType})을 불러왔습니다.", LogManager.LogCategory.ServerManager);
            return postList;
        }

        /// <summary>
        /// 특정 우편 아이템을 비동기적으로 수령합니다.
        /// </summary>
        /// <param name="postType">수령할 우편의 타입</param>
        /// <param name="postInDate">수령할 우편의 inDate</param>
        /// <returns>수령 성공 여부</returns>
        public async UniTask<bool> ReceivePostItemAsync(PostType postType, string postInDate)
        {
            var bro = await BackendAsync(callback => Backend.UPost.ReceivePostItem(postType, postInDate, callback));

            if (!bro.IsSuccess())
            {
                ErroDebug(bro);
                LogManager.LogError($"우편 수령 실패 ({postType}, {postInDate}): {bro.GetMessage()}", LogManager.LogCategory.ServerManager);
                return false;
            }

            LogManager.Log($"우편 수령 성공 ({postType}, {postInDate})", LogManager.LogCategory.ServerManager);
            return true;
        }

        #endregion

        #region 유틸리티 메서드

        /// <summary>
        /// 뒤끝 SDK의 콜백 기반 비동기 메서드를 UniTask로 변환하는 헬퍼 메서드입니다.
        /// </summary>
        private UniTask<BackendReturnObject> BackendAsync(Action<Backend.BackendCallback> backendCall)
        {
            var tcs = new UniTaskCompletionSource<BackendReturnObject>();
            // 뒤끝 SDK 메서드를 실행하고, 콜백이 호출되면 UniTask를 완료시킵니다.
            backendCall(bro => tcs.TrySetResult(bro));
            return tcs.Task;
        }

        /// <summary>
        ///     오류 디버그
        /// </summary>
        /// <param name="bro"></param>
        private void ErroDebug(BackendReturnObject bro)
        {
            LogManager.LogError($"StatusCode: {bro.GetStatusCode()}", LogManager.LogCategory.ServerManager);
            LogManager.LogError($"ErrorCode: {bro.GetErrorCode()}", LogManager.LogCategory.ServerManager);
            LogManager.LogError($"Message: {bro.GetMessage()}", LogManager.LogCategory.ServerManager);
        }

        /// <summary>
        /// 뒤끝 inDate 형식(yyyy-MM-ddTHH:mm:ss.fffZ)을 "yyyy-MM-dd" 형식으로 변환합니다.
        /// </summary>
        private string ConvertToCustomDateFormat(string inDate)
        {
            if (DateTime.TryParse(inDate, out DateTime date))
            {
                return date.ToString("yyyy-MM-dd");
            }
            // 파싱 실패 시 원본 문자열 반환
            return inDate;
        }
        
        #endregion
    }
}
