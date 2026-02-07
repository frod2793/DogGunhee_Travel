using BackEnd;
using Cysharp.Threading.Tasks;

namespace InGame.Services
{
    /// <summary>
    /// 인증(로그인, 회원가입, 토큰 관리)을 담당하는 POCO 서비스입니다.
    /// </summary>
    public class AuthenticationService : BaseService, IAuthenticationService
    {
        #region 프로퍼티

        /// <summary>
        /// 현재 로그인된 사용자의 고유 ID (UUID)입니다.
        /// </summary>
        public string Uuid { get; private set; }

        /// <summary>
        /// 현재 로그인된 사용자의 닉네임입니다.
        /// </summary>
        public string NickName { get; private set; }

        #endregion

        #region 생성자

        public AuthenticationService(UniTaskCompletionSource<bool> backendInitialized) : base(backendInitialized)
        {
        }

        #endregion

        #region 공개 메서드 (로그인/회원가입)

        public async UniTask<(bool success, string error)> LoginAsync(string id, string pw)
        {
            await m_backendInitialized.Task;

            var bro = await BackendCallAsync(callback => Backend.BMember.CustomLogin(id, pw, callback));

            if (bro.IsSuccess())
            {
                Log("로그인 성공");
                OnLoginSuccess();
                return (true, null);
            }

            LogError("Auth", bro);
            return (false, bro.GetMessage());
        }

        public async UniTask<(bool success, string error)> GuestLoginAsync()
        {
            await m_backendInitialized.Task;

            var bro = await BackendCallAsync(callback => Backend.BMember.GuestLogin(callback));

            if (bro.IsSuccess())
            {
                Log("게스트 로그인 성공");
                OnLoginSuccess();
                return (true, null);
            }

            Backend.BMember.DeleteGuestInfo();
            LogError("Auth", bro);
            return (false, bro.GetMessage());
        }

        public async UniTask<(bool success, string error)> TokenLoginAsync()
        {
            await m_backendInitialized.Task;

            var bro = await BackendCallAsync(callback => Backend.BMember.LoginWithTheBackendToken(callback));

            if (bro.IsSuccess())
            {
                Log("토큰 로그인 성공");
                OnLoginSuccess();
                return (true, null);
            }

            LogWarning($"토큰 로그인 실패/만료: {bro.GetMessage()}");
            return (false, bro.GetMessage());
        }

        public async UniTask<(bool success, string error)> SignUpAsync(string id, string pw, string nickname)
        {
            await m_backendInitialized.Task;

            var signUpBro = await BackendCallAsync(callback => Backend.BMember.CustomSignUp(id, pw, callback));
            if (!signUpBro.IsSuccess())
            {
                LogError("Auth", signUpBro);
                return (false, signUpBro.GetMessage());
            }

            Log("회원가입 성공. 닉네임 설정 시도...");

            var updateBro = await BackendCallAsync(callback => Backend.BMember.UpdateNickname(nickname, callback));
            if (!updateBro.IsSuccess())
            {
                LogError("Auth", updateBro);
                return (false, $"가입 성공, 닉네임 설정 실패: {updateBro.GetMessage()}");
            }

            Log("닉네임 설정 성공");
            return (true, null);
        }

        public void DeleteGuestInfo()
        {
            Backend.BMember.DeleteGuestInfo();
        }

        #endregion

        #region 내부 헬퍼

        private void OnLoginSuccess()
        {
            Uuid = Backend.UID;
            NickName = Backend.UserNickName;

            if (string.IsNullOrEmpty(NickName))
            {
                NickName = Uuid;
            }

            RefreshTokenIfAlive();
        }

        private void RefreshTokenIfAlive()
        {
            var bro = Backend.BMember.IsAccessTokenAlive();
            if (bro.IsSuccess())
            {
                Log("액세스 토큰 갱신 시도");
                Backend.BMember.RefreshTheBackendToken();
            }
        }

        #endregion
    }
}
