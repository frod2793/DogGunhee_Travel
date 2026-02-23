using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Services;

namespace Title
{
    /// <summary>
    /// [설명]: 로그인 화면의 상태와 로직을 관리하는 ViewModel 클래스입니다. (POCO)
    /// </summary>
    public class LoginViewModel
    {
        #region 상태 관련 이벤트

        /// <summary>
        /// [설명]: 작업 중 상태(Busy)가 변경될 때 호출됩니다.
        /// </summary>
        public Action<bool> OnBusyStateChanged;

        /// <summary>
        /// [설명]: 에러 메시지가 발생했을 때 메시지와 함께 호출됩니다.
        /// </summary>
        public Action<string> OnErrorMessage;

        /// <summary>
        /// [설명]: 로그인에 최종 성공했을 때 호출됩니다.
        /// </summary>
        public Action OnLoginSuccess;

        #endregion

        #region 내부 데이터 및 프로퍼티

        private readonly IAuthenticationService m_authService;
        private bool m_isBusy;

        /// <summary>
        /// [설명]: 현재 비즈니스 로직이 실행 중인지 여부를 나타냅니다.
        /// </summary>
        public bool IsBusy
        {
            get => m_isBusy;
            private set
            {
                if (m_isBusy == value) return;
                m_isBusy = value;
                OnBusyStateChanged?.Invoke(m_isBusy);
            }
        }

        #endregion

        #region 생성자

        /// <summary>
        /// [설명]: 인증 서비스를 주입받아 ViewModel을 생성합니다.
        /// </summary>
        /// <param name="authService">인증 서비스 인터페이스</param>
        public LoginViewModel(IAuthenticationService authService)
        {
            m_authService = authService;
        }

        #endregion

        #region 비즈니스 로직

        /// <summary>
        /// [설명]: 아이디/비밀번호를 이용하여 로그인을 시도합니다.
        /// </summary>
        /// <param name="id">사용자 아이디</param>
        /// <param name="pw">사용자 비밀번호</param>
        /// <param name="ct">작업 취소 토큰</param>
        public async UniTask LoginAsync(string id, string pw, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
            {
                OnErrorMessage?.Invoke("아이디와 비밀번호를 입력해주세요.");
                return;
            }

            IsBusy = true;
            try
            {
                var (success, error) = await m_authService.LoginAsync(id, pw);
                if (success)
                {
                    OnLoginSuccess?.Invoke();
                }
                else
                {
                    string msg = error.Contains("StatusCode : 401") 
                        ? "아이디 또는 비밀번호가 일치하지 않습니다." 
                        : $"로그인 실패\n{error}";
                    OnErrorMessage?.Invoke(msg);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// [설명]: 게스트 계정으로 로그인을 시도합니다.
        /// </summary>
        /// <param name="ct">작업 취소 토큰</param>
        public async UniTask GuestLoginAsync(CancellationToken ct)
        {
            IsBusy = true;
            try
            {
                var (success, error) = await m_authService.GuestLoginAsync();
                if (success)
                {
                    OnLoginSuccess?.Invoke();
                }
                else
                {
                    OnErrorMessage?.Invoke($"게스트 로그인 실패\n{error}");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// [설명]: 로컬에 저장된 세션 토큰을 이용하여 자동 로그인을 시도합니다.
        /// </summary>
        /// <returns>로그인 성공 여부</returns>
        public async UniTask<bool> TokenLoginAsync()
        {
            IsBusy = true;
            try
            {
                var (success, error) = await m_authService.TokenLoginAsync();
                if (success)
                {
                    OnLoginSuccess?.Invoke();
                    return true;
                }
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// [설명]: 새로운 계정을 생성(회원가입)하고 성공 시 즉시 로그인을 시도합니다.
        /// </summary>
        /// <param name="nick">사용자 닉네임</param>
        /// <param name="id">사용자 아이디</param>
        /// <param name="pw">사용자 비밀번호</param>
        /// <param name="pwCheck">비밀번호 확인</param>
        /// <param name="ct">작업 취소 토큰</param>
        public async UniTask SignUpAsync(string nick, string id, string pw, string pwCheck, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(nick) || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw) || string.IsNullOrEmpty(pwCheck))
            {
                OnErrorMessage?.Invoke("모든 항목을 입력해주세요.");
                return;
            }

            if (pw != pwCheck)
            {
                OnErrorMessage?.Invoke("비밀번호가 일치하지 않습니다.");
                return;
            }

            IsBusy = true;
            try
            {
                var (signUpSuccess, signUpError) = await m_authService.SignUpAsync(id, pw, nick);
                if (!signUpSuccess)
                {
                    OnErrorMessage?.Invoke($"회원가입 실패\n{signUpError}");
                    return;
                }

                // 가입 성공 후 즉시 로그인 시도
                var (loginSuccess, loginError) = await m_authService.LoginAsync(id, pw);
                if (loginSuccess)
                {
                    OnLoginSuccess?.Invoke();
                }
                else
                {
                    OnErrorMessage?.Invoke("가입에는 성공했으나 로그인에 실패했습니다.\n다시 로그인해주세요.");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// [설명]: 로컬의 게스트 정보를 삭제합니다.
        /// </summary>
        public void DeleteGuestInfo()
        {
            m_authService.DeleteGuestInfo();
        }

        #endregion
    }
}
