using UnityEngine;

namespace InGame.Weapon.Controllers
{
    public class CatPunchWeaponView : MonoBehaviour
    {
        [Header("Tuning Data")]
        [Tooltip("공격 지속 시간 (애니메이션 길이와 무관하게 콜라이더가 활성화되는 시간)")]
        [UnityEngine.Serialization.FormerlySerializedAs("AttackDuration")]
        [SerializeField] private float m_attackDuration = 0.2f;
        public float AttackDuration => m_attackDuration;

        [Tooltip("무기 회전 오프셋 (스프라이트 기준, 도리깨질 방향 보정)")]
        [UnityEngine.Serialization.FormerlySerializedAs("RotationOffset")]
        [SerializeField] private float m_rotationOffset = -90f;
        public float RotationOffset => m_rotationOffset;

        [Tooltip("타격 대상 레이어")]
        [UnityEngine.Serialization.FormerlySerializedAs("TargetLayer")]
        [SerializeField] private LayerMask m_targetLayer;
        public LayerMask TargetLayer => m_targetLayer;

        private void Reset()
        {
            m_targetLayer = LayerMask.GetMask("Mob");
        }
    }
}
