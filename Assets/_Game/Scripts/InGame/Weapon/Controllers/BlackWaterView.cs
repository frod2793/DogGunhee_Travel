using UnityEngine;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 블랙워터(Ink) 무기의 프리팹 설정 및 시각적 데이터를 관리하는 컴포넌트입니다.
    /// </summary>
    public class BlackWaterView : MonoBehaviour
    {
        [Header("Tick Damage Settings")]
        [Tooltip("틱 데미지가 들어가는 간격입니다.")]
        public float DamageTickInterval = 0.5f;

        [Header("Slow Settings (Evolved)")]
        [Tooltip("적의 이동 속도를 감소시키는 비율 (0.3 = 30% 감소)")]
        [Range(0f, 1f)] public float SlowAmount = 0.3f;
        
        [Tooltip("슬로우 효과 지속 시간(초)입니다.")]
        public float SlowDuration = 1.0f;
    }
}
