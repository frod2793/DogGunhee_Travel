using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Services;
using InGame.Data;
using InGame.Managers;
using InGame;
using VContainer;
using VContainer.Unity;
using UnityEngine;

namespace Title
{
    /// <summary>
    /// [설명]: 로그인 화면의 상태와 로직을 관리하는 ViewModel 클래스입니다. (POCO)
    /// VContainer를 통한 의존성 주입을 지원하며, 앱 초기화 및 네비게이션 책임을 가집니다.
    /// </summary>
    public class LoginViewModel : IInitializable
    {
        #region 공개 API

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

        /// <summary>
        /// [설명]: 씬 전환(네비게이션)이 시작될 때 호출됩니다.
        /// </summary>
        public Action OnNavigationStarted;

        #endregion

        #region 내부 변수

        private readonly IAuthenticationService m_authService;
        private readonly ServerManager m_serverManager;
        private readonly ISceneLoader m_sceneLoader;
        private readonly ISoundManager m_soundManager;
        private readonly IAppUpdateService m_appUpdateService;

        private bool m_isBusy;

        #endregion

        #region 프로퍼티

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

        #region 생성자 및 초기화

        /// <summary>
        /// [설명]: 필요한 서비스들을 VContainer로부터 주입받아 ViewModel을 생성합니다.
        /// </summary>
        [Inject]
        public LoginViewModel(
            IAuthenticationService authService,
            ServerManager serverManager,
            ISceneLoader sceneLoader,
            ISoundManager soundManager,
            IAppUpdateService appUpdateService)
        {
            m_authService = authService;
            m_serverManager = serverManager;
            m_sceneLoader = sceneLoader;
            m_soundManager = soundManager;
            m_appUpdateService = appUpdateService;
        }

        /// <summary>
        /// [설명]: VContainer에 의해 호출되는 초기화 진입점입니다.
        /// </summary>
        public void Initialize()
        {
            InitializeInternalAsync().Forget();
        }

        /// <summary>
        /// [설명]: 앱 강제 업데이트 확인 및 배경음 재생 등 초기화 로직을 수행합니다.
        /// </summary>
        private async UniTask InitializeInternalAsync()
        {
            if (m_appUpdateService != null)
            {
                await m_appUpdateService.CheckForUpdateAsync();
            }

            if (m_soundManager != null)
            {
                m_soundManager.LoadSoundSetting();
                // Title 씬 진입 시 Intro 사운드 재생
                m_soundManager.Play(SoundKeys.Intro.ToString(), Sound.BGM, 1.0f, true);
            }
        }

        #endregion

        #region 비즈니스 로직

        /// <summary>
        /// [설명]: 아이디/비밀번호를 이용하여 로그인을 시도합니다.
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
                    await ProcessLoginSuccessAsync();
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
        public async UniTask GuestLoginAsync(CancellationToken ct)
        {
            IsBusy = true;
            try
            {
                var (success, error) = await m_authService.GuestLoginAsync();
                if (success)
                {
                    await ProcessLoginSuccessAsync();
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
        public async UniTask<bool> TokenLoginAsync()
        {
            IsBusy = true;
            try
            {
                var (success, error) = await m_authService.TokenLoginAsync();
                if (success)
                {
                    await ProcessLoginSuccessAsync();
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

                var (loginSuccess, loginError) = await m_authService.LoginAsync(id, pw);
                if (loginSuccess)
                {
                    await ProcessLoginSuccessAsync();
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
        /// [설명]: 로그인 성공 후 데이터 로드 및 로비 씬 전환을 처리합니다.
        /// </summary>
        private async UniTask ProcessLoginSuccessAsync()
        {
            OnLoginSuccess?.Invoke();
            OnNavigationStarted?.Invoke();
            
            // D토 조립
            var playerDto = new PlayerDataDTO();
            ServerSessionDTO sessionDto = null;
            
            if (m_serverManager != null && m_authService != null)
            {
                playerDto.Initialize(m_authService.NickName, m_authService.Uuid);
                sessionDto = m_serverManager.GetSession();
            }

            var payload = new ScenePayloadDTO(playerDto, sessionDto, m_soundManager)
            {
                IsFirstLogin = true
            };

            // 네비게이션 실행
            if (m_sceneLoader != null)
            {
                await m_sceneLoader.LoadSceneAsync(SceneNames.Lobby, payload);
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

