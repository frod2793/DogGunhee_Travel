using UnityEngine;
using System;

namespace InGame.Managers
{
    /// <summary>
    /// [설명]: 게임의 시스템 설정(프레임 레이트, 사운드 볼륨 등)을 관리하는 매니저입니다.
    /// </summary>
    public class GameSettingsManager : MonoBehaviour
    {
        #region 내부 필드
        

        [SerializeField]
        private SettingsData m_settingsData;

        #endregion

        #region 프로퍼티
        

        /// <summary>
        /// [설명]: 현재 설정 데이터를 반환합니다.
        /// </summary>
        public SettingsData SettingsData => m_settingsData;

        #endregion

        #region 유니티 생명주기

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (m_settingsData != null)
            {
                SettingsData.OnSettingsChanged += ApplyAllSettings;
            }
        }

        private void OnDisable()
        {
            if (m_settingsData != null)
            {
                SettingsData.OnSettingsChanged -= ApplyAllSettings;
            }
        }
        

        #endregion

        #region 초기화 및 설정 제어

        /// <summary>
        /// [설명]: 매니저를 초기화합니다.
        /// </summary>
        private void Initialize()
        {
            DontDestroyOnLoad(gameObject);

            if (m_settingsData != null)
            {
                m_settingsData.LoadSettings();
                ApplyAllSettings();
            }
            else
            {
                LogManager.LogError("SettingsData가 할당되지 않았습니다. 기본 프레임으로 실행됩니다.", LogManager.LogCategory.SettingsManager);
                ApplyDefaultSettings();
            }
        }

        /// <summary>
        /// [설명]: 기본 설정을 적용합니다.
        /// </summary>
        private void ApplyDefaultSettings()
        {
            // TODO: 기본 설정 적용 로직 구현
        }

        /// <summary>
        /// [설명]: 모든 설정을 적용합니다.
        /// </summary>
        private void ApplyAllSettings()
        {
            // TODO: 저장된 설정 데이터에 기반한 설정 적용 로직 구현
        }

        #endregion

        #region 이벤트 핸들러

        /// <summary>
        /// [설명]: 설정 변경 시 호출되는 이벤트 핸들러입니다.
        /// </summary>
        private void OnSettingsChanged()
        {
            // 설정 변경 처리 로직
        }

        /// <summary>
        /// [설명]: 게임 일시정지 시 호출되는 이벤트 핸들러입니다.
        /// </summary>
        private void OnGamePause()
        {
            // 일시정지 처리 로직
        }

        /// <summary>
        /// [설명]: 게임 재개 시 호출되는 이벤트 핸들러입니다.
        /// </summary>
        private void OnGameResume()
        {
            // 재개 처리 로직
        }

        /// <summary>
        /// [설명]: 게임 오버 시 호출되는 이벤트 핸들러입니다.
        /// </summary>
        private void OnGameOver()
        {
            // 게임 오버 처리 로직
        }

        #endregion
    }
}