using UnityEngine;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 히어로 랜딩(방패) 무기의 프리팹 설정 및 시각적 데이터를 관리하는 컴포넌트입니다.
    /// 에디터 인스펙터에서 하드코딩된 수치들을 조정할 수 있도록 합니다.
    /// </summary>
    public class ShieldWeaponView : MonoBehaviour
    {
        [Header("Animation Timings")]
        [Tooltip("공격 시작 후 충격파/부메랑이 발생하는 타이밍(초)입니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("ImpactTriggerTime")]
        [SerializeField] private float m_impactTriggerTime = 1.07f;
        public float ImpactTriggerTime => m_impactTriggerTime;
        
        [Tooltip("공격 판정 후 애니메이션 마무리를 위해 대기하는 시간(초)입니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("FollowThroughDelay")]
        [SerializeField] private float m_followThroughDelay = 0.5f;
        public float FollowThroughDelay => m_followThroughDelay;

        [Header("Evolved (Boomerang) Settings")]
        [Tooltip("부메랑이 날아가는 속도입니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("BoomerangSpeed")]
        [SerializeField] private float m_boomerangSpeed = 5f;
        public float BoomerangSpeed => m_boomerangSpeed;
        
        [Tooltip("부메랑이 반환을 시작하기 전 대기 시간(초)입니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("ReturnDelay")]
        [SerializeField] private float m_returnDelay = 0.1f;
        public float ReturnDelay => m_returnDelay;
        
        [Tooltip("부메랑의 초당 회전 수입니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("RotationsPerSecond")]
        [SerializeField] private float m_rotationsPerSecond = 2.5f;
        public float RotationsPerSecond => m_rotationsPerSecond;

        #region Helper Methods

        /// <summary>
        /// 프리팹에서 필요한 컴포넌트나 설정을 미리 검증합니다.
        /// </summary>
        public void ValidateSettings()
        {
            if (m_impactTriggerTime <= 0) m_impactTriggerTime = 1.07f;
            if (m_followThroughDelay <= 0) m_followThroughDelay = 0.5f;
            if (m_boomerangSpeed <= 0) m_boomerangSpeed = 5f;
        }

        #endregion
    }
}
