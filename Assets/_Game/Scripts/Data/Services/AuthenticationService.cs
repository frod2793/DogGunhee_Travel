using System;
using BackEnd;
using Cysharp.Threading.Tasks;

namespace InGame.Services
{
    /// <summary>
    /// 인증(로그인, 회원가입, 토큰 관리)을 담당하는 POCO 서비스입니다.
    /// MonoBehaviour에 의존하지 않으며, UniTask를 사용하여 비동기 처리를 수행합니다.
    /// </summary>
    public class AuthenticationService
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

        #region 내부 필드

        private readonly UniTaskCompletionSource<bool> m_backendInitialized;

        #endregion

        #region 생성자

        /// <summary>
        /// AuthenticationService 인스턴스를 생성합니다.
        /// </summary>
        /// <param name="backendInitialized">뒤끝 SDK 초기화 완료를 알리는 Task</param>
        public AuthenticationService(UniTaskCompletionSource<bool> backendInitialized)
        {
            m_backendInitialized = backendInitialized;
        }

        #endregion

        #region 공개 메서드 (로그인/회원가입)

        /// <summary>
        /// 커스텀 ID/PW를 사용하여 로그인합니다.
        /// </summary>
        /// <param name="id">사용자 아이디</param>
        /// <param name="pw">사용자 비밀번호</param>
        /// <returns>성공 여부와 오류 메시지 튜플</returns>
        public async UniTask<(bool success, string error)> LoginAsync(string id, string pw)
        {
            await m_backendInitialized.Task;

            var bro = await BackendCallAsync(callback => Backend.BMember.CustomLogin(id, pw, callback));

            if (bro.IsSuccess())
            {
                LogManager.Log("로그인 성공", LogManager.LogCategory.ServerManager);
                OnLoginSuccess();
                return (true, null);
            }

            LogError(bro);
            return (false, bro.GetMessage());
        }

        /// <summary>
        /// 게스트 계정으로 로그인합니다.
        /// </summary>
        /// <returns>성공 여부와 오류 메시지 튜플</returns>
        public async UniTask<(bool success, string error)> GuestLoginAsync()
        {
            await m_backendInitialized.Task;

            var bro = await BackendCallAsync(callback => Backend.BMember.GuestLogin(callback));

            if (bro.IsSuccess())
            {
                LogManager.Log("게스트 로그인 성공", LogManager.LogCategory.ServerManager);
                OnLoginSuccess();
                return (true, null);
            }

            // 실패 시 로컬 게스트 정보 삭제 (재시도 시 새 계정 생성 유도)
            Backend.BMember.DeleteGuestInfo();
            LogError(bro);
            return (false, bro.GetMessage());
        }

        /// <summary>
        /// 저장된 토큰을 사용하여 자동 로그인을 시도합니다.
        /// </summary>
        /// <returns>성공 여부와 오류 메시지 튜플</returns>
        public async UniTask<(bool success, string error)> TokenLoginAsync()
        {
            await m_backendInitialized.Task;

            var bro = await BackendCallAsync(callback => Backend.BMember.LoginWithTheBackendToken(callback));

            if (bro.IsSuccess())
            {
                LogManager.Log("토큰 로그인 성공", LogManager.LogCategory.ServerManager);
                OnLoginSuccess();
                return (true, null);
            }

            LogManager.LogWarning($"토큰 로그인 실패/만료: {bro.GetMessage()}", LogManager.LogCategory.ServerManager);
            return (false, bro.GetMessage());
        }

        /// <summary>
        /// 커스텀 ID/PW를 사용하여 회원가입 후 닉네임을 설정합니다.
        /// </summary>
        /// <param name="id">사용자 아이디</param>
        /// <param name="pw">사용자 비밀번호</param>
        /// <param name="nickname">사용자 닉네임</param>
        /// <returns>성공 여부와 오류 메시지 튜플</returns>
        public async UniTask<(bool success, string error)> SignUpAsync(string id, string pw, string nickname)
        {
            await m_backendInitialized.Task;

            // 1. 회원가입
            var signUpBro = await BackendCallAsync(callback => Backend.BMember.CustomSignUp(id, pw, callback));
            if (!signUpBro.IsSuccess())
            {
                LogError(signUpBro);
                return (false, signUpBro.GetMessage());
            }

            LogManager.Log("회원가입 성공. 닉네임 설정 시도...", LogManager.LogCategory.ServerManager);

            // 2. 닉네임 설정
            var updateBro = await BackendCallAsync(callback => Backend.BMember.UpdateNickname(nickname, callback));
            if (!updateBro.IsSuccess())
            {
                LogError(updateBro);
                return (false, $"가입 성공, 닉네임 설정 실패: {updateBro.GetMessage()}");
            }

            LogManager.Log("닉네임 설정 성공", LogManager.LogCategory.ServerManager);
            return (true, null);
        }

        /// <summary>
        /// 게스트 계정 정보를 삭제하고 재시도할 수 있도록 합니다.
        /// </summary>
        public void DeleteGuestInfo()
        {
            Backend.BMember.DeleteGuestInfo();
        }

        #endregion

        #region 내부 헬퍼

        /// <summary>
        /// 로그인 성공 시 공통 처리 (UUID, 닉네임 설정, 토큰 갱신)
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
        /// 액세스 토큰이 유효하면 갱신을 시도합니다.
        /// </summary>
        private void RefreshTokenIfAlive()
        {
            var bro = Backend.BMember.IsAccessTokenAlive();
            if (bro.IsSuccess())
            {
                LogManager.Log("액세스 토큰 갱신 시도", LogManager.LogCategory.ServerManager);
                Backend.BMember.RefreshTheBackendToken();
            }
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
            LogManager.LogError($"[Auth Error] {bro.GetStatusCode()} / {bro.GetErrorCode()} / {bro.GetMessage()}", LogManager.LogCategory.ServerManager);
        }

        #endregion
    }
}
