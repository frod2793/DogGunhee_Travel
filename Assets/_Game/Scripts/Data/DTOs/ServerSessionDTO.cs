using System;
using InGame.Services;

namespace InGame.Data
{
    /// <summary>
    /// [설명]: 서버 세션 정보를 담는 데이터 전송 객체입니다.
    /// SDK 초기화 후 생성되며, 씬 간 전달되어 의존성 주입에 사용됩니다.
    /// </summary>
    [Serializable]
    public class ServerSessionDTO
    {
        #region 프로퍼티 (Interfaces)

        public IAuthenticationService Auth { get; private set; }
        public IGameDataService GameData { get; private set; }
        public IPostService Post { get; private set; }

        #endregion

        #region 생성자

        public ServerSessionDTO(IAuthenticationService auth, IGameDataService gameData, IPostService post)
        {
            Auth = auth;
            GameData = gameData;
            Post = post;
        }

        #endregion
    }
}
