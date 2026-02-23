using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// [설명]: 고양이 펀치(Cat Punch) 무기의 물리 판정 범위 및 시각적 오프셋을 설정하는 뷰 컴포넌트입니다.
    /// WeaponPoolManager나 무기 프리팹에 부착되어 Controller 로직에서 참조합니다.
    /// </summary>
    public class CatPunchWeaponView : MonoBehaviour
    {
        #region 에디터 설정

        [Header("판정 및 시간 설정")]
        [Tooltip("공격 판정이 유지되는 시간(초)입니다. (애니메이션 길이와 별개로 동작)")]
        [FormerlySerializedAs("AttackDuration")]
        [Range(0.05f, 1.0f)]
        [SerializeField] private float m_attackDuration = 0.2f;

        [Header("위치 및 회전 보정")]
        [Tooltip("무기 스프라이트의 초기 회전 보정값입니다. (기본 -90도: 위쪽을 바라보게 함)")]
        [FormerlySerializedAs("RotationOffset")]
        [Range(-180f, 180f)]
        [SerializeField] private float m_rotationOffset = -90f;

        [Header("타겟 설정")]
        [Tooltip("공격이 유효하게 들어가는 레이어입니다.")]
        [FormerlySerializedAs("TargetLayer")]
        [SerializeField] private LayerMask m_targetLayer;

        #endregion

        #region 공개 프로퍼티

        /// <summary>
        /// 공격(콜라이더 활성화) 지속 시간
        /// </summary>
        public float AttackDuration => m_attackDuration;

        /// <summary>
        /// 스프라이트 회전 오프셋 (각도)
        /// </summary>
        public float RotationOffset => m_rotationOffset;

        /// <summary>
        /// 타격 대상 레이어 마스크
        /// </summary>
        public LayerMask TargetLayer => m_targetLayer;

        #endregion

        #region 유니티 라이프사이클

        /// <summary>
        /// 컴포넌트가 처음 추가되거나 Reset 될 때 기본값을 설정합니다.
        /// </summary>
        private void Reset()
        {
            // 기본 타겟 레이어를 'Mob'으로 자동 설정 시도
            m_targetLayer = LayerMask.GetMask("Enemy");
            
            // 만약 'Mob' 레이어가 없다면 모든 레이어로 설정
            if (m_targetLayer == 0)
            {
                m_targetLayer = -1; // Everything
            }
        }

        #endregion
    }
}