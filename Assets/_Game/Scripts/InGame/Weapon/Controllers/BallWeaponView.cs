using UnityEngine;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 공놀이(Ball) 무기의 시각적 설정 및 튜닝 데이터를 관리하는 뷰 컴포넌트입니다.
    /// WeaponPoolManager 오브젝트에 부착하여 중앙에서 조절합니다.
    /// </summary>
    public class BallWeaponView : MonoBehaviour
    {
        [Header("회전 설정")]
        [Tooltip("Z축 기본 회전 보정값입니다.")]
        public float RotationOffset = 0f;

        [Tooltip("체크 시 투사체가 궤도 진행 방향을 바라봅니다.")]
        public bool RotateWithOrbit = true;

        [Header("애니메이션 설정")]
        [Tooltip("공의 회전 속도 배율입니다.")]
        public float RotationSpeedMultiplier = 1.0f;
    }
}
