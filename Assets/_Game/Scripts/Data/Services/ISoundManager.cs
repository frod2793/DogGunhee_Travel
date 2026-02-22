using UnityEngine;

namespace InGame.Services
{
    /// <summary>
    /// [설명]: 사운드 시스템을 위한 인터페이스입니다.
    /// </summary>
    public interface ISoundManager
    {
        float EffectSoundVolume { get; }
        float BgmSoundVolume { get; }

        /// <summary>
        /// [설명]: 사운드를 재생합니다.
        /// </summary>
        void Play(string clipName, Sound type = Sound.SFX, float pitch = 1.0f, bool loop = false);

        /// <summary>
        /// [설명]: 3D 공간 상의 특정 위치에서 사운드를 재생합니다.
        /// </summary>
        void Play(string clipName, Vector3 position, float pitch = 1.0f);

        /// <summary>
        /// [설명]: 특정 타입의 볼륨을 설정합니다.
        /// </summary>
        void SetVolume(Sound type, float volume);

        /// <summary>
        /// [설명]: 모든 사운드를 정지하고 데이터를 정리합니다.
        /// </summary>
        void Clear();

        /// <summary>
        /// [설명]: 저장된 사운드 설정을 로드합니다.
        /// </summary>
        void LoadSoundSetting();

        /// <summary>
        /// [설명]: 씬 이름에 매핑된 BGM으로 크로스페이드 전환합니다.
        /// SoundData의 SceneBgmMapping에 등록된 클립과 페이드 시간을 사용합니다.
        /// </summary>
        /// <param name="sceneName">전환 대상 씬 이름</param>
        void TransitionBGMForScene(string sceneName);
    }
}