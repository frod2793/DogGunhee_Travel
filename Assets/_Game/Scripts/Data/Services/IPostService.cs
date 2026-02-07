using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using BackEnd;

namespace InGame.Services
{
    /// <summary>
    /// 우편함(Post) 관련 기능을 담당하는 서비스 인터페이스입니다.
    /// </summary>
    public interface IPostService
    {
        /// <summary>
        /// 지정된 타입의 우편 목록을 가져옵니다.
        /// </summary>
        UniTask<List<PostService.PostInfo>> GetPostListAsync(PostType postType);

        /// <summary>
        /// 우편의 첨부 아이템을 수령합니다.
        /// </summary>
        UniTask<bool> ReceivePostItemAsync(PostType postType, string postInDate);

        /// <summary>
        /// 쿠폰 타입 메시지 목록을 로드합니다.
        /// </summary>
        UniTask LoadMessageAsync();
    }
}
