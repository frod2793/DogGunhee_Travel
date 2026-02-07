using UnityEngine;

namespace InGame.Manager
{
    /// <summary>
    /// 게임의 시스템 설정(프레임 레이트, 사운드 볼륨 등)을 관리하는 매니저입니다.
    /// </summary>
    public class GameSettingsManager : MonoBehaviour
    {
        #region 싱글톤
        private static GameSettingsManager s_instance;
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

        #region 필드
        [SerializeField] private SettingsData m_settingsData;
        #endregion

        #region 프로퍼티
        public SettingsData SettingsData => m_settingsData;
        #endregion

        #region Unity 라이프사이클

        private void Awake()
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
                SetTargetFrameRate(120);
            }
        }

        private void OnEnable()
        {
            SettingsData.OnSettingsChanged += ApplyAllSettings;
        }

        private void OnDisable()
        {
            SettingsData.OnSettingsChanged -= ApplyAllSettings;
        }

        #endregion

        #region 설정 적용 메서드

        /// <summary>
        /// 모든 시스템 설정을 적용합니다.
        /// </summary>
        public void ApplyAllSettings()
        {
            if (m_settingsData == null) return;
            
            SetTargetFrameRate(m_settingsData.TargetFrameRate);
            // 추가 설정 적용 (사운드 등)은 SoundManager에 위임하거나 여기서 직접 처리
        }

        /// <summary>
        /// 게임의 목표 프레임 레이트를 설정합니다.
        /// </summary>
        public void SetTargetFrameRate(int frameRate)
        {
            if (frameRate < 30 && frameRate != -1)
            {
                LogManager.LogWarning($"유효하지 않은 목표 프레임({frameRate})이 요청되어 무시합니다.", LogManager.LogCategory.SettingsManager);
                return;
            }

            if (Application.targetFrameRate == frameRate && QualitySettings.vSyncCount == 0)
            {
                return;
            }

            Application.targetFrameRate = frameRate;
            QualitySettings.vSyncCount = 0;
            LogManager.Log($"목표 프레임 레이트를 {frameRate}으로 설정했습니다.", LogManager.LogCategory.SettingsManager);
        }

        #endregion
    }
}
