using UnityEngine;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// [설명]: 공놀이(Ball) 무기의 시각적 연출 및 회전 동작을 제어하는 설정 컴포넌트입니다.
    /// WeaponPoolManager나 무기 최상위 부모에 부착되어, 생성되는 모든 투사체의 회전 방식을 중앙에서 제어합니다.
    /// </summary>
    public class BallWeaponView : MonoBehaviour
    {
        #region 에디터 설정

        [Header("회전 및 방향 설정")]
        [Tooltip("투사체 스프라이트의 초기 Z축 회전 각도 보정값입니다. (0 ~ 360)")]
        [Range(0f, 360f)]
        [SerializeField] private float m_rotationOffset = 0f;

        [Tooltip("활성화 시 투사체가 공전 궤도의 진행 방향(접선)을 바라보며 회전합니다.")]
        [SerializeField] private bool m_rotateWithOrbit = true;

        [Header("애니메이션 설정")]
        [Tooltip("공 자체의 자전(Spin) 속도 배율입니다. (기본값: 1.0)")]
        [Range(0.1f, 20.0f)]
        [SerializeField] private float m_rotationSpeedMultiplier = 1.0f;

        #endregion

        #region 공개 프로퍼티

        /// <summary>
        /// 스프라이트의 초기 회전 오프셋 (도 단위)
        /// </summary>
        public float RotationOffset => m_rotationOffset;

        /// <summary>
        /// 궤도 진행 방향으로 회전 여부
        /// </summary>
        public bool RotateWithOrbit => m_rotateWithOrbit;

        /// <summary>
        /// 자전 속도 계수
        /// </summary>
        public float RotationSpeedMultiplier => m_rotationSpeedMultiplier;

        #endregion
    }
}