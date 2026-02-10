using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 신비한 불꽃(Flame) 무기의 데미지 계수 및 오브젝트 풀링 설정을 관리하는 뷰 컴포넌트입니다.
    /// <br/> WeaponPoolManager나 무기 프리팹에 부착되어 Controller 로직에 데이터를 제공합니다.
    /// </summary>
    public class FlameWeaponView : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("1. 데미지 설정")]
        [Tooltip("직접 타격 데미지 대비 지속 피해(DoT)의 비율입니다. (예: 0.5 = 50% 데미지)")]
        [FormerlySerializedAs("DotDamageRatio")]
        [Range(0.1f, 2.0f)] // 과도한 배율 방지
        [SerializeField] private float m_dotDamageRatio = 0.5f;
        
        [Tooltip("적이 불기둥에 피격될 때 잠깐 표시될 틴트(Tint) 색상입니다.")]
        [FormerlySerializedAs("HitFlashColor")]
        [SerializeField] private Color m_hitFlashColor = Color.white;

        [Header("2. 풀링 및 제한 설정")]
        [Tooltip("화면에 동시에 활성화될 수 있는 최대 불기둥 개수입니다.")]
        [FormerlySerializedAs("MaxActivePillars")]
        [Range(1, 50)] // 성능 고려 제한
        [SerializeField] private int m_maxActivePillars = 10;
        
        [Tooltip("생성할 오브젝트 풀의 총 크기입니다. (최대 활성 개수보다 커야 함)")]
        [FormerlySerializedAs("PoolSize")]
        [Range(5, 100)]
        [SerializeField] private int m_poolSize = 20;

        #endregion

        #region 2. 공개 프로퍼티 (Properties)

        /// <summary>
        /// 지속 데미지 비율
        /// </summary>
        public float DotDamageRatio => m_dotDamageRatio;

        /// <summary>
        /// 피격 시 플래시 색상
        /// </summary>
        public Color HitFlashColor => m_hitFlashColor;

        /// <summary>
        /// 동시 활성화 최대 개수
        /// </summary>
        public int MaxActivePillars => m_maxActivePillars;

        /// <summary>
        /// 오브젝트 풀 할당 크기
        /// </summary>
        public int PoolSize => m_poolSize;

        #endregion

        #region 3. 에디터 유효성 검사 (OnValidate)

        /// <summary>
        /// 인스펙터에서 값이 변경될 때 데이터 무결성을 검사합니다.
        /// </summary>
        private void OnValidate()
        {
            // 풀 사이즈는 항상 활성화 제한 개수보다 크거나 같아야 함
            if (m_poolSize < m_maxActivePillars)
            {
                m_poolSize = m_maxActivePillars + 2;
            }
        }

        #endregion
    }
}