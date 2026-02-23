using System;
using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;

namespace InGame.Services
{
    /// <summary>
    /// [설명]: 우편함(Post) 관련 기능을 담당하는 POCO 서비스입니다.
    /// </summary>
    public class PostService : BaseService, IPostService
    {
        #region 내부 클래스 

        /// <summary>
        /// [설명]: 우편 정보를 담는 내부 클래스입니다.
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

        #region 초기화 

        public PostService(UniTaskCompletionSource<bool> backendInitialized) : base(backendInitialized)
        {
        }

        #endregion

        #region 공개 메서드 

        /// <summary>
        /// [설명]: 지정된 타입의 우편 목록을 가져옵니다.
        /// </summary>
        public async UniTask<List<PostInfo>> GetPostListAsync(PostType postType)
        {
            await m_backendInitialized.Task;

            var bro = await BackendCallAsync(callback => Backend.UPost.GetPostList(postType, 100, callback));

            if (!bro.IsSuccess())
            {
                LogError("Post", bro);
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

                ParseItems(postJson, postInfo);
                postList.Add(postInfo);
            }

            Log($"우편 {postList.Count}개 로드 완료 ({postType})");
            return postList;
        }

        /// <summary>
        /// [설명]: 우편의 첨부 아이템을 수령합니다.
        /// </summary>
        public async UniTask<bool> ReceivePostItemAsync(PostType postType, string postInDate)
        {
            await m_backendInitialized.Task;

            var bro = await BackendCallAsync(callback => Backend.UPost.ReceivePostItem(postType, postInDate, callback));

            if (!bro.IsSuccess())
            {
                LogError("Post", bro);
                return false;
            }

            Log($"우편 수령 완료 ({postType})");
            return true;
        }

        /// <summary>
        /// [설명]: 쿠폰 타입 메시지 목록을 로드합니다.
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
                    Log($"제목: {json[i]["title"]}, 날짜: {json[i]["inDate"]}");
                }
            }
        }

        #endregion

        #region 보조 로직 

        /// <summary>
        /// [설명]: 우편의 첨부 아이템 JSON을 파싱하여 PostInfo에 추가합니다.
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
        /// [설명]: Backend에서 제공하는 inDate 형식을 'yyyy-MM-dd' 형식으로 변환합니다.
        /// </summary>
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