using UnityEngine;
using System;

namespace InGame.Manager
{
    /// <summary>
    /// 게임의 시스템 설정(프레임 레이트, 사운드 볼륨 등)을 관리하는 매니저입니다.
    /// </summary>
    public class GameSettingsManager : MonoBehaviour
    {
        #region 1. 싱글톤 패턴
        private static GameSettingsManager s_instance;
        
        /// <summary>
        /// GameSettingsManager의 싱글톤 인스턴스를 반환합니다.
        /// </summary>
        public static GameSettingsManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindFirstObjectByType<GameSettingsManager>();
                    if (s_instance == null)
                    {
                        var container = new GameObject(nameof(GameSettingsManager));
                        s_instance = container.AddComponent<GameSettingsManager>();
                        DontDestroyOnLoad(container);
                    }
                }
                return s_instance;
            }
        }
        #endregion

        #region 2. 에디터 설정 (Inspector)
        [SerializeField]
        private SettingsData m_settingsData;
        #endregion

        #region 3. 프로퍼티
        /// <summary>
        /// 현재 설정 데이터를 반환합니다.
        /// </summary>
        public SettingsData SettingsData => m_settingsData;
        #endregion

        #region 4. Unity 생명주기
        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (m_settingsData != null)
            {
                // 이벤트 핸들러를 정적 멤버로 접근하도록 수정
                SettingsData.OnSettingsChanged += ApplyAllSettings;
            }
        }

        private void OnDisable()
        {
            if (m_settingsData != null)
            {
                // 이벤트 핸들러를 정적 멤버로 접근하도록 수정
                SettingsData.OnSettingsChanged -= ApplyAllSettings;
            }
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }
        #endregion

        #region 5. 초기화 및 설정 로직
        /// <summary>
        /// 매니저를 초기화합니다.
        /// </summary>
        private void Initialize()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            s_instance = this;
            DontDestroyOnLoad(gameObject);
            
            if (m_settingsData != null)
            {
                m_settingsData.LoadSettings();
                ApplyAllSettings();
            }
            else
            {
                LogManager.LogError("SettingsData가 할당되지 않았습니다. 기본 프레임으로 실행됩니다.", LogManager.LogCategory.SettingsManager);
                // 기본 설정 적용
                ApplyDefaultSettings();
            }
        }

        /// <summary>
        /// 기본 설정을 적용합니다.
        /// </summary>
        private void ApplyDefaultSettings()
        {
            // 기본 설정 로직
        }

        /// <summary>
        /// 모든 설정을 적용합니다.
        /// </summary>
        private void ApplyAllSettings()
        {
            // 설정 적용 로직
        }
        #endregion

        #region 6. 공개 메서드
        /// <summary>
        /// 프레임 레이트를 설정합니다.
        /// </summary>
        /// <param name="targetFrameRate">설정할 프레임 레이트</param>
        public void SetTargetFrameRate(int targetFrameRate)
        {
            Application.targetFrameRate = targetFrameRate;
        }

        /// <summary>
        /// 설정 변경 이벤트를 발생시킵니다.
        /// </summary>
        public void NotifySettingsChanged()
        {
            // 설정 변경 로직
        }
        #endregion

        #region 7. 내부 로직 및 이벤트 핸들러
        /// <summary>
        /// 설정 변경 시 호출되는 이벤트 핸들러
        /// </summary>
        private void OnSettingsChanged()
        {
            // 설정 변경 처리 로직
        }

        /// <summary>
        /// 게임 일시정지 시 호출되는 이벤트 핸들러
        /// </summary>
        private void OnGamePause()
        {
            // 일시정지 처리 로직
        }

        /// <summary>
        /// 게임 재개 시 호출되는 이벤트 핸들러
        /// </summary>
        private void OnGameResume()
        {
            // 재개 처리 로직
        }

        /// <summary>
        /// 게임 오버 시 호출되는 이벤트 핸들러
        /// </summary>
        private void OnGameOver()
        {
            // 게임 오버 처리 로직
        }
        #endregion
    }
}