using System;
using R3;
using UnityEngine;

namespace Lobby
{
    /// <summary>
    /// 옵션 팝업의 비즈니스 로직 및 상태를 관리하는 ViewModel 클래스입니다.
    /// </summary>
    public class OptionPopupViewModel : IDisposable
    {
        #region 필드 및 프로퍼티

        private readonly SettingsData m_settingsData;
        private readonly SoundManager m_soundManager;
        private readonly CompositeDisposable m_disposables = new();

        /// <summary>
        /// 효과음 볼륨 (0.0 ~ 1.0)
        /// </summary>
        public ReactiveProperty<float> EffectSoundVolume { get; } = new();

        /// <summary>
        /// 배경음 볼륨 (0.0 ~ 1.0)
        /// </summary>
        public ReactiveProperty<float> BgmSoundVolume { get; } = new();

        /// <summary>
        /// 목표 프레임 레이트 (FPS)
        /// </summary>
        public ReactiveProperty<int> TargetFrameRate { get; } = new();

        /// <summary>
        /// 프레임 레이트 선택 옵션들
        /// </summary>
        public int[] FrameRateOptions { get; } = { 30, 60, 120 };

        #endregion

        #region 초기화

        public OptionPopupViewModel(SettingsData settingsData, SoundManager soundManager)
        {
            m_settingsData = settingsData;
            m_soundManager = soundManager;

            Initialize();
        }

        private void Initialize()
        {
            if (m_settingsData == null)
            {
                Debug.LogError("[OptionPopupViewModel] SettingsData가 할당되지 않았습니다!");
                return;
            }

            // 1. 데이터 로드
            m_settingsData.LoadSettings();
            
            // 싱글톤 인스턴스 확인 (전달받은 객체가 null인 경우 대비)
            var soundManager = m_soundManager ?? SoundManager.Instance;
            Debug.Log($"[OptionPopupViewModel] 초기화 시작. SoundManager: {(soundManager != null ? "연결됨" : "NULL")}");

            // 2. 상태 변경 구독 (시스템 반영)
            EffectSoundVolume.Subscribe(v =>
            {
                Debug.Log($"[OptionPopupViewModel] SFX 볼륨 적용 시도: {v}");
                m_settingsData.EffectSoundVolume = v;
                soundManager?.SetVolume(Sound.SFX, v);
            }).AddTo(m_disposables);

            BgmSoundVolume.Subscribe(v =>
            {
                Debug.Log($"[OptionPopupViewModel] BGM 볼륨 적용 시도: {v}");
                m_settingsData.BackgroundSoundVolume = v;
                soundManager?.SetVolume(Sound.BGM, v);
            }).AddTo(m_disposables);

            TargetFrameRate.Subscribe(v =>
            {
                Debug.Log($"[OptionPopupViewModel] 프레임 레이트 적용: {v}");
                m_settingsData.TargetFrameRate = v;
                ApplyFrameRate(v);
            }).AddTo(m_disposables);

            // 3. 현재 설정값으로 상태 초기화 (구독 이후에 수행하여 위 Subscribe들이 발송됩니다)
            EffectSoundVolume.Value = m_settingsData.EffectSoundVolume;
            BgmSoundVolume.Value = m_settingsData.BackgroundSoundVolume;
            TargetFrameRate.Value = m_settingsData.TargetFrameRate;
        }

        #endregion

        #region 비즈니스 로직

        /// <summary>
        /// 현재 설정된 모든 값을 저장합니다.
        /// </summary>
        public void SaveSettings()
        {
            m_settingsData?.SaveSettings();
        }

        /// <summary>
        /// 엔진에 프레임 레이트를 직접 적용합니다.
        /// </summary>
        private void ApplyFrameRate(int frameRate)
        {
            Application.targetFrameRate = frameRate;
            QualitySettings.vSyncCount = 0;
        }

        /// <summary>
        /// 저장된 FPS 값을 슬라이더 인덱스로 변환합니다.
        /// </summary>
        public int GetFrameRateIndex()
        {
            int index = Array.IndexOf(FrameRateOptions, TargetFrameRate.Value);
            return index < 0 ? 2 : index; // 기본값 120 FPS (인덱스 2)
        }

        public void Dispose()
        {
            m_disposables.Dispose();
        }

        #endregion
    }
}
