using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// [설명]: 부메랑(Boomerang) 무기의 발사 패턴(각도, 간격)과 연사 설정을 관리하는 뷰 컴포넌트입니다.
    /// WeaponPoolManager나 무기 프리팹에 부착되어 전략(Strategy) 로직에서 참조합니다.
    /// </summary>
    public class BoomerangWeaponView : MonoBehaviour
    {
        #region 에디터 설정

        [Header("발사 궤적 설정")]
        [Tooltip("첫 번째 투사체의 시작 각도 오프셋입니다. (플레이어 정면 기준, 음수: 왼쪽, 양수: 오른쪽)")]
        [FormerlySerializedAs("StartAngle")]
        [Range(-180f, 180f)]
        [SerializeField] private float m_startAngle = -15f;
        
        [Tooltip("다중 발사 시, 투사체 간의 각도 간격입니다.")]
        [FormerlySerializedAs("AngleStep")]
        [Range(0f, 90f)]
        [SerializeField] private float m_angleStep = 30f;
        
        [Header("타이밍 설정")]
        [Tooltip("한 번의 공격 주기 내에서 여러 발을 쏠 때의 발사 간격(밀리초)입니다.")]
        [FormerlySerializedAs("BurstDelayMs")]
        [Range(0, 500)]
        [SerializeField] private int m_burstDelayMs = 50;

        #endregion

        #region 공개 프로퍼티

        /// <summary>
        /// 시작 각도 (도)
        /// </summary>
        public float StartAngle => m_startAngle;

        /// <summary>
        /// 투사체 간 각도 차이 (도)
        /// </summary>
        public float AngleStep => m_angleStep;

        /// <summary>
        /// 연사 지연 시간 (ms)
        /// </summary>
        public int BurstDelayMs => m_burstDelayMs;

        #endregion
    }
}