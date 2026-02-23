using BackEnd;
using Cysharp.Threading.Tasks;

namespace InGame.Services
{
    /// <summary>
    /// [설명]: 인증(로그인, 회원가입, 토큰 관리)을 담당하는 POCO 서비스입니다.
    /// </summary>
    public class AuthenticationService : BaseService, IAuthenticationService
    {
        #region 외부 프로퍼티 

        /// <summary>
        /// [설명]: 현재 로그인된 사용자의 고유 ID (UUID)입니다.
        /// </summary>
        public string Uuid { get; private set; }

        /// <summary>
        /// [설명]: 현재 로그인된 사용자의 닉네임입니다.
        /// </summary>
        public string NickName { get; private set; }

        #endregion

        #region 초기화 

        /// <summary>
        /// [설명]: 백엔드 초기화 완료를 대기할 수 있는 초기화 메서드입니다.
        /// </summary>
        public AuthenticationService(UniTaskCompletionSource<bool> backendInitialized) : base(backendInitialized)
        {
        }

        #endregion

        #region 공개 메서드 

        /// <summary>
        /// [설명]: 사용자 ID와 비밀번호로 로그인을 시도합니다.
        /// </summary>
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

        /// <summary>
        /// [설명]: 게스트 계정으로 로그인을 시도합니다.
        /// </summary>
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

        /// <summary>
        /// [설명]: 저장된 토큰을 사용해 자동 로그인을 시도합니다.
        /// </summary>
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

        /// <summary>
        /// [설명]: 새 사용자로 회원가입 후 닉네임을 설정합니다.
        /// </summary>
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

        /// <summary>
        /// [설명]: 게스트 정보를 시스템에서 삭제합니다.
        /// </summary>
        public void DeleteGuestInfo()
        {
            Backend.BMember.DeleteGuestInfo();
        }

        #endregion

        #region 내부 메서드 

        /// <summary>
        /// [설명]: 로그인 성공 시 상태를 초기화하고 사용자 정보를 갱신합니다.
        /// </summary>
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

        /// <summary>
        /// [설명]: 현재 액세스 토큰이 유효한 경우 갱신을 시도합니다.
        /// </summary>
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