using VContainer;
using VContainer.Unity;
using InGame;
using InGame.Managers;
using InGame.Services;
using UnityEngine;

namespace Title
{
    /// <summary>
    /// [설명]: Title 씬의 의존성 주입을 관리하는 LifetimeScope 클래스입니다.
    /// </summary>
    public class TitleLifetimeScope : LifetimeScope
    {
        #region 에디터 설정

        [Header("매니저 참조 (씬 내 오브젝트)")]
        [SerializeField, Tooltip("서버 통신 매니저")]
        private ServerManager m_serverManager;

        [SerializeField, Tooltip("씬 로더")]
        private SceneLoader m_sceneLoader;

        [SerializeField, Tooltip("사운드 매니저")]
        private SoundManager m_soundManager;

        [SerializeField, Tooltip("앱 업데이트 매니저")]
        private AppUpdateManager m_appUpdateManager;

        [Header("뷰 참조")]
        [SerializeField, Tooltip("로그인 뷰")]
        private LoginViewMVVM m_loginView;

        #endregion

        #region 의존성 설정

        /// <summary>
        /// [설명]: 서비스 및 뷰모델을 컨테이너에 등록합니다.
        /// </summary>
        /// <param name="builder">VContainer 빌더</param>
        protected override void Configure(IContainerBuilder builder)
        {
            // 1. 매니저 컴포넌트 등록
            if (m_serverManager != null)
            {
                builder.RegisterComponent(m_serverManager);
                // 인증 서비스 등록 (ServerManager에서 추출)
                builder.Register(container => m_serverManager.Auth, Lifetime.Scoped);
            }

            if (m_sceneLoader != null)
                builder.RegisterComponent(m_sceneLoader).AsImplementedInterfaces().AsSelf();

            if (m_soundManager != null)
                builder.RegisterComponent(m_soundManager).AsImplementedInterfaces().AsSelf();

            if (m_appUpdateManager != null)
                builder.RegisterComponent(m_appUpdateManager).AsImplementedInterfaces().AsSelf();

            // 2. 뷰모델 등록 (IInitializable을 구현하여 엔트리 포인트 역할 수행)
            builder.Register<LoginViewModel>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

            // 3. 뷰 등록 및 멤버 주입 설정
            if (m_loginView != null)
                builder.RegisterComponent(m_loginView);
        }

        #endregion
    }
}
