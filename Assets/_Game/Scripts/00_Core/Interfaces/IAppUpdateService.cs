using Cysharp.Threading.Tasks;

namespace InGame.Managers
{
    /// <summary>
    /// [설명]: 앱 업데이트 확인을 담당하는 서비스 인터페이스입니다.
    /// 플랫폼별 업데이트 로직을 추상화하여 테스트 가능한 구조로 제공합니다.
    /// </summary>
    public interface IAppUpdateService
    {
        /// <summary>
        /// [설명]: 앱 업데이트가 필요한지 비동기로 확인합니다.
        /// </summary>
        UniTask CheckForUpdateAsync();
    }
}
