using UnityEngine;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 블랙워터(Ink) 무기의 프리팹 설정 및 시각적 데이터를 관리하는 컴포넌트입니다.
    /// </summary>
    public class BlackWaterView : MonoBehaviour
    {
        [Header("틱 데미지 설정")]
        [Tooltip("틱 데미지가 들어가는 간격입니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("DamageTickInterval")]
        [SerializeField] private float m_damageTickInterval = 0.5f;
        public float DamageTickInterval => m_damageTickInterval;

        [Header("슬로우 설정 (진화 시)")]
        [Tooltip("적의 이동 속도를 감소시키는 비율 (0.3 = 30% 감소)")]
        [Range(0f, 1f)] 
        [UnityEngine.Serialization.FormerlySerializedAs("SlowAmount")]
        [SerializeField] private float m_slowAmount = 0.3f;
        public float SlowAmount => m_slowAmount;
        
        [Tooltip("슬로우 효과 지속 시간(초)입니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("SlowDuration")]
        [SerializeField] private float m_slowDuration = 1.0f;
        public float SlowDuration => m_slowDuration;
    }
}
