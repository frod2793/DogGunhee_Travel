using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.Effect;

namespace InGame.Managers
{
    /// <summary>
    /// [설명]: 시각 효과 및 연출을 제어하는 서비스 인터페이스입니다.
    /// </summary>
    public interface IEffectService
    {
        void PlayEffect(EffectType type, Vector3 position, Quaternion rotation);
        void PlayEffect(EffectType type, Vector3 position);
        void PlayPlayerHitCameraShake();
        void PlayLevelUpEffect(SpriteRenderer targetRenderer);
        
        /// <summary> [설명]: 몬스터 피격 시 짧고 강렬한 점멸 효과를 재생합니다. </summary>
        void PlayMobHitEffect(SpriteRenderer targetRenderer);

        void PlayImmediateFlashEffect(SpriteRenderer targetRenderer, Color? flashColor = null);
        UniTask PlayQueuedFlashEffect(SpriteRenderer targetRenderer, Color? flashColor = null);
        void PlayKnockbackEffect(Transform targetTransform, Vector3 direction, float distance, float duration);
    }
}
