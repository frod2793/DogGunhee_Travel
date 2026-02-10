using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 블랙워터(Ink/먹물) 무기의 장판 효과 및 디버프 수치를 설정하는 뷰 컴포넌트입니다.
    /// <br/> 프리팹이나 WeaponPoolManager에 부착되어 로직(Strategy)에서 참조합니다.
    /// </summary>
    public class BlackWaterView : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("1. 데미지 주기 설정")]
        [Tooltip("장판 위에 있는 적에게 데미지를 입히는 시간 간격(초)입니다.")]
        [FormerlySerializedAs("DamageTickInterval")]
        [Range(0.1f, 2.0f)] // 너무 짧으면 연산 부하, 너무 길면 성능 저하 방지
        [SerializeField] private float m_damageTickInterval = 0.5f;

        [Header("2. 디버프 설정 (진화 효과)")]
        [Tooltip("적의 이동 속도 감소율입니다. (0.0 ~ 1.0, 예: 0.3 = 30% 감속)")]
        [FormerlySerializedAs("SlowAmount")]
        [Range(0f, 1f)] 
        [SerializeField] private float m_slowAmount = 0.3f;
        
        [Tooltip("슬로우 효과가 지속되는 시간(초)입니다.")]
        [FormerlySerializedAs("SlowDuration")]
        [Range(0.1f, 5.0f)]
        [SerializeField] private float m_slowDuration = 1.0f;

        #endregion

        #region 2. 공개 프로퍼티 (Properties)

        /// <summary>
        /// 데미지 틱 간격 (초)
        /// </summary>
        public float DamageTickInterval => m_damageTickInterval;

        /// <summary>
        /// 이동 속도 감소율 (0~1)
        /// </summary>
        public float SlowAmount => m_slowAmount;

        /// <summary>
        /// 디버프 지속 시간 (초)
        /// </summary>
        public float SlowDuration => m_slowDuration;

        #endregion
    }
}