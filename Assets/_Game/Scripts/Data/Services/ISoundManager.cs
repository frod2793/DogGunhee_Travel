using UnityEngine;

namespace InGame.Services
{
    /// <summary>
    /// 사운드 시스템을 위한 인터페이스입니다.
    /// 구체적인 구현(SoundManager)에 대한 의존성을 낮추기 위해 사용됩니다.
    /// </summary>
    public interface ISoundManager
    {
        float EffectSoundVolume { get; }
        float BgmSoundVolume { get; }

        /// <summary>
        /// 사운드를 재생합니다.
        /// </summary>
        void Play(string clipName, Sound type = Sound.SFX, float pitch = 1.0f, bool loop = false);
        
        /// <summary>
        /// 3D 공간 상의 특정 위치에서 사운드를 재생합니다.
        /// </summary>
        void Play(string clipName, Vector3 position, float pitch = 1.0f);

        /// <summary>
        /// 특정 타입의 볼륨을 설정합니다.
        /// </summary>
        void SetVolume(Sound type, float volume);

        /// <summary>
        /// 모든 사운드를 정지하고 데이터를 정리합니다.
        /// </summary>
        void Clear();
    }
}
