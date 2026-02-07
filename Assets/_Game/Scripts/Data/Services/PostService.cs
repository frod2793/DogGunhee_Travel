using System;
using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;

namespace InGame.Services
{
    /// <summary>
    /// 우편함(Post) 관련 기능을 담당하는 POCO 서비스입니다.
    /// </summary>
    public class PostService : BaseService, IPostService
    {
        #region 내부 클래스
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

        #region 생성자
        public PostService(UniTaskCompletionSource<bool> backendInitialized) : base(backendInitialized)
        {
        }
        #endregion

        #region 공개 메서드 (우편 기능)

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

        #region 내부 헬퍼

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
