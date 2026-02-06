using System;
using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;

namespace InGame.Services
{
    /// <summary>
    /// 우편함(Post) 관련 기능을 담당하는 POCO 서비스입니다.
    /// MonoBehaviour에 의존하지 않으며, UniTask를 사용하여 비동기 처리를 수행합니다.
    /// </summary>
    public class PostService
    {
        #region 내부 클래스

        /// <summary>
        /// 우편 정보를 담는 데이터 클래스입니다.
        /// </summary>
        public class PostInfo
        {
            public PostType PostType;
            public string PostInDate;
            public string InDate;
            public string Title;
            public string Content;
            public string Sender;
            public Dictionary<string, int> Items = new Dictionary<string, int>();
        }

        #endregion

        #region 내부 필드

        private readonly UniTaskCompletionSource<bool> m_backendInitialized;

        #endregion

        #region 생성자

        /// <summary>
        /// PostService 인스턴스를 생성합니다.
        /// </summary>
        /// <param name="backendInitialized">뒤끝 SDK 초기화 완료를 알리는 Task</param>
        public PostService(UniTaskCompletionSource<bool> backendInitialized)
        {
            m_backendInitialized = backendInitialized;
        }

        #endregion

        #region 공개 메서드 (우편 기능)

        /// <summary>
        /// 지정된 타입의 우편 목록을 가져옵니다.
        /// </summary>
        /// <param name="postType">우편 타입 (Admin, Coupon 등)</param>
        /// <returns>우편 정보 리스트</returns>
        public async UniTask<List<PostInfo>> GetPostListAsync(PostType postType)
        {
            await m_backendInitialized.Task;

            var bro = await BackendCallAsync(callback => Backend.UPost.GetPostList(postType, 100, callback));

            if (!bro.IsSuccess())
            {
                LogError(bro);
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
                ParseItems(postJson, postInfo);

                postList.Add(postInfo);
            }

            LogManager.Log($"우편 {postList.Count}개 로드 완료 ({postType})", LogManager.LogCategory.ServerManager);
            return postList;
        }

        /// <summary>
        /// 우편의 첨부 아이템을 수령합니다.
        /// </summary>
        /// <param name="postType">우편 타입</param>
        /// <param name="postInDate">우편의 inDate</param>
        /// <returns>수령 성공 여부</returns>
        public async UniTask<bool> ReceivePostItemAsync(PostType postType, string postInDate)
        {
            await m_backendInitialized.Task;

            var bro = await BackendCallAsync(callback => Backend.UPost.ReceivePostItem(postType, postInDate, callback));

            if (!bro.IsSuccess())
            {
                LogError(bro);
                return false;
            }

            LogManager.Log($"우편 수령 완료 ({postType})", LogManager.LogCategory.ServerManager);
            return true;
        }

        /// <summary>
        /// 쿠폰 타입 메시지 목록을 로드하고 로그에 출력합니다 (디버그 용도).
        /// </summary>
        public async UniTask LoadMessageAsync()
        {
            await m_backendInitialized.Task;

            var bro = await BackendCallAsync(callback => Backend.UPost.GetPostList(PostType.Coupon, 10, callback));
            if (bro.IsSuccess())
            {
                var json = bro.GetReturnValuetoJSON()["postList"];
                for (var i = 0; i < json.Count; i++)
                {
                    LogManager.Log($"제목: {json[i]["title"]}, 날짜: {json[i]["inDate"]}", LogManager.LogCategory.ServerManager);
                }
            }
        }

        #endregion

        #region 내부 헬퍼

        /// <summary>
        /// 우편 JSON에서 아이템 정보를 파싱합니다.
        /// </summary>
        private void ParseItems(JsonData postJson, PostInfo postInfo)
        {
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
        }

        /// <summary>
        /// inDate 문자열을 사용자 친화적인 날짜 형식으로 변환합니다.
        /// </summary>
        private string ConvertToCustomDateFormat(string inDate)
        {
            if (DateTime.TryParse(inDate, out DateTime date))
            {
                return date.ToString("yyyy-MM-dd");
            }
            return inDate;
        }

        /// <summary>
        /// 뒤끝 비동기 콜백 메서드를 UniTask로 변환합니다.
        /// </summary>
        private UniTask<BackendReturnObject> BackendCallAsync(Action<Backend.BackendCallback> backendCall)
        {
            var tcs = new UniTaskCompletionSource<BackendReturnObject>();
            backendCall(bro => tcs.TrySetResult(bro));
            return tcs.Task;
        }

        /// <summary>
        /// 오류 로그를 출력합니다.
        /// </summary>
        private void LogError(BackendReturnObject bro)
        {
            LogManager.LogError($"[Post Error] {bro.GetStatusCode()} / {bro.GetErrorCode()} / {bro.GetMessage()}", LogManager.LogCategory.ServerManager);
        }

        #endregion
    }
}
