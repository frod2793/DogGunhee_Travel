using System;
using R3;
using UnityEngine;

namespace Lobby
{
    /// <summary>
    /// 옵션 팝업의 비즈니스 로직과 설정 데이터를 관리하는 ViewModel 클래스입니다.
    /// <br/>사운드 볼륨 및 타겟 프레임 레이트 상태를 반응형으로 보유합니다.
    /// </summary>
    public class OptionPopupViewModel : IDisposable
    {
        #region 1. 필드 및 프로퍼티

        private readonly SettingsData m_settingsData;
        private readonly SoundManager m_soundManager;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        /// <summary> 효과음(SFX) 볼륨 (0.0 ~ 1.0) </summary>
        public ReactiveProperty<float> EffectSoundVolume { get; } = new ReactiveProperty<float>();

        /// <summary> 배경음(BGM) 볼륨 (0.0 ~ 1.0) </summary>
        public ReactiveProperty<float> BgmSoundVolume { get; } = new ReactiveProperty<float>();

        /// <summary> 목표 프레임 레이트 (FPS) </summary>
        public ReactiveProperty<int> TargetFrameRate { get; } = new ReactiveProperty<int>();

        /// <summary> 지원하는 프레임 레이트 목록 </summary>
        public int[] FrameRateOptions { get; } = { 30, 60, 120 };

        #endregion

        #region 2. 초기화 및 생성자

        /// <summary>
        /// OptionPopupViewModel을 생성하고 초기 설정을 수행합니다.
        /// </summary>
        /// <param name="settingsData">저장소 데이터</param>
        /// <param name="soundManager">사운드 제어 매니저</param>
        public OptionPopupViewModel(SettingsData settingsData, SoundManager soundManager)
        {
            m_settingsData = settingsData;
            m_soundManager = soundManager;

            Initialize();
        }

        /// <summary>
        /// 설정 데이터를 불러오고 상태 변화에 따른 시스템 반영(사운드/프레임) 로직을 구동합니다.
        /// </summary>
        private void Initialize()
        {
            if (m_settingsData == null)
            {
                LogManager.LogError("[OptionPopupViewModel] SettingsData가 할당되지 않았습니다!", LogManager.LogCategory.System);
                return;
            }

            // 1. 기존 설정 로드
            m_settingsData.LoadSettings();

            // 2. 상태 변경 이벤트 구독 (ReactiveProperty -> System)
            var soundManager = (m_soundManager != null) ? m_soundManager : SoundManager.Instance;

            EffectSoundVolume.Subscribe(v =>
            {
                m_settingsData.EffectSoundVolume = v;
                if (soundManager != null)
                {
                    soundManager.SetVolume(Sound.SFX, v);
                }
            }).AddTo(m_disposables);

            BgmSoundVolume.Subscribe(v =>
            {
                m_settingsData.BackgroundSoundVolume = v;
                if (soundManager != null)
                {
                    soundManager.SetVolume(Sound.BGM, v);
                }
            }).AddTo(m_disposables);

            TargetFrameRate.Subscribe(v =>
            {
                m_settingsData.TargetFrameRate = v;
                ApplyFrameRate(v);
            }).AddTo(m_disposables);

            // 3. 로드된 데이터로 초기 상태 설정
            EffectSoundVolume.Value = m_settingsData.EffectSoundVolume;
            BgmSoundVolume.Value = m_settingsData.BackgroundSoundVolume;
            TargetFrameRate.Value = m_settingsData.TargetFrameRate;
        }

        #endregion

        #region 3. 비즈니스 로직

        /// <summary>
        /// 현재 모든 설정값을 영구 저장소에 저장합니다.
        /// </summary>
        public void SaveSettings()
        {
            if (m_settingsData != null)
            {
                m_settingsData.SaveSettings();
            }
            else
            {
                LogManager.LogWarning("옵션 데이터가 로드되지 않았습니다.", LogManager.LogCategory.System);
            }
        }

        /// <summary>
        /// 유니티 엔진 설정에 목표 프레임 레이트를 반영합니다.
        /// </summary>
        private void ApplyFrameRate(int frameRate)
        {
            Application.targetFrameRate = frameRate;
            QualitySettings.vSyncCount = 0; // 프레임 고정을 위해 수직 동기화 종료
        }

        /// <summary>
        /// 현재 FPS 수치를 기반으로 옵션 배열의 인덱스를 반환합니다.
        /// </summary>
        public int GetFrameRateIndex()
        {
            int index = Array.IndexOf(FrameRateOptions, TargetFrameRate.Value);
            return index < 0 ? 2 : index; // 찾지 못할 경우 기본값(120fps) 인덱스 반환
        }

        #endregion

        #region 4. 리소스 해제

        /// <summary>
        /// 뷰모델 파생 시 모든 구독을 정리합니다.
        /// </summary>
        public void Dispose()
        {
            m_disposables.Dispose();

            EffectSoundVolume.Dispose();
            BgmSoundVolume.Dispose();
            TargetFrameRate.Dispose();
        }

        #endregion
    }
}
