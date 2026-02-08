using UnityEngine;

namespace InGame.Weapon.Controllers
{
    public class CatPunchWeaponView : MonoBehaviour
    {
        [Header("Tuning Data")]
        [Tooltip("공격 지속 시간 (애니메이션 길이와 무관하게 콜라이더가 활성화되는 시간)")]
        public float AttackDuration = 0.2f;

        [Tooltip("무기 회전 오프셋 (스프라이트 기준, 도리깨질 방향 보정)")]
        public float RotationOffset = -90f;

        [Tooltip("타격 대상 레이어")]
        public LayerMask TargetLayer;

        private void Reset()
        {
            TargetLayer = LayerMask.GetMask("Mob");
        }
    }
}
