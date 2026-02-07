using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Services;

namespace Title
{
    /// <summary>
    /// 로그인 화면의 상태와 로직을 관리하는 ViewModel 클래스입니다. (POCO)
    /// </summary>
    public class LoginViewModel
    {
        #region 상태 관련 이벤트
        public Action<bool> OnBusyStateChanged;
        public Action<string> OnErrorMessage;
        public Action OnLoginSuccess;
        #endregion

        #region 내부 상탸
        private readonly IAuthenticationService m_authService;
        private bool m_isBusy;

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
        public LoginViewModel(IAuthenticationService authService)
        {
            m_authService = authService;
        }
        #endregion

        #region 커맨드 (비즈니스 로직)

        /// <summary>
        /// 아이디/비밀번호 로그인을 시도합니다.
        /// </summary>
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
        /// 게스트 로그인을 시도합니다.
        /// </summary>
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
        /// 토큰 로그인을 시도합니다.
        /// </summary>
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
        /// 회원가입을 시도합니다.
        /// </summary>
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

        public void DeleteGuestInfo()
        {
            m_authService.DeleteGuestInfo();
        }

        #endregion
    }
}
