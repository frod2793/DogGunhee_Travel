using Cysharp.Threading.Tasks;

namespace InGame
{
    /// <summary>
    /// [설명]: 씬 전환 및 로딩 처리를 담당하는 서비스 인터페이스입니다.
    /// </summary>
    public interface ISceneLoader
    {
        UniTask LoadLobbySceneAsync(object payload = null);
        UniTask LoadGameSceneAsync(object payload = null);
        UniTask LoadVamSerLikeSceneAsync(object payload = null);
        UniTask LoadIntroSceneAsync(object payload = null);
        UniTask LoadSceneAsync(string sceneName, object payload = null);
        void LoadLobbyScene(object payload = null);
        void LoadGameScene(object payload = null);
        void LoadVamSerLikeScene(object payload = null);
        void LoadIntroScene(object payload = null);
        void LoadScene(string sceneName, object payload = null);
        UniTask WaitUntilFadedOutAsync();
    }
}
