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
        public float ImpactTriggerTime = 1.07f;
        
        [Tooltip("공격 판정 후 애니메이션 마무리를 위해 대기하는 시간(초)입니다.")]
        public float FollowThroughDelay = 0.5f;

        [Header("Evolved (Boomerang) Settings")]
        [Tooltip("부메랑이 날아가는 속도입니다.")]
        public float BoomerangSpeed = 5f;
        
        [Tooltip("부메랑이 반환을 시작하기 전 대기 시간(초)입니다.")]
        public float ReturnDelay = 0.1f;
        
        [Tooltip("부메랑의 초당 회전 수입니다.")]
        public float RotationsPerSecond = 2.5f;

        #region Helper Methods

        /// <summary>
        /// 프리팹에서 필요한 컴포넌트나 설정을 미리 검증합니다.
        /// </summary>
        public void ValidateSettings()
        {
            if (ImpactTriggerTime <= 0) ImpactTriggerTime = 1.07f;
            if (FollowThroughDelay <= 0) FollowThroughDelay = 0.5f;
            if (BoomerangSpeed <= 0) BoomerangSpeed = 5f;
        }

        #endregion
    }
}
