using Cysharp.Threading.Tasks;

namespace InGame.Services
{
    /// <summary>
    /// 인증(로그인, 회원가입, 토큰 관리)을 담당하는 서비스 인터페이스입니다.
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// 현재 로그인된 사용자의 고유 ID (UUID)입니다.
        /// </summary>
        string Uuid { get; }

        /// <summary>
        /// 현재 로그인된 사용자의 닉네임입니다.
        /// </summary>
        string NickName { get; }

        /// <summary>
        /// 커스텀 ID/PW를 사용하여 로그인합니다.
        /// </summary>
        UniTask<(bool success, string error)> LoginAsync(string id, string pw);

        /// <summary>
        /// 게스트 계정으로 로그인합니다.
        /// </summary>
        /// <returns>성공 여부와 오류 메시지 튜플</returns>
        UniTask<(bool success, string error)> GuestLoginAsync();

        /// <summary>
        /// 저장된 토큰을 사용하여 자동 로그인을 시도합니다.
        /// </summary>
        UniTask<(bool success, string error)> TokenLoginAsync();

        /// <summary>
        /// 커스텀 ID/PW를 사용하여 회원가입 후 닉네임을 설정합니다.
        /// </summary>
        UniTask<(bool success, string error)> SignUpAsync(string id, string pw, string nickname);

        /// <summary>
        /// 게스트 계정 정보를 삭제합니다.
        /// </summary>
        void DeleteGuestInfo();
    }
}
