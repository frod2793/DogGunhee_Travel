using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 히어로 랜딩(Shield) 무기의 애니메이션 타이밍 및 투사체 물리를 설정하는 뷰 컴포넌트입니다.
    /// <br/> WeaponPoolManager나 무기 프리팹에 부착되어 로직(Controller/Logic)에 튜닝 데이터를 제공합니다.
    /// </summary>
    public class ShieldWeaponView : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("1. 애니메이션 타이밍")]
        [Tooltip("공격 애니메이션 시작 후, 실제 충격파나 부메랑이 생성되는 시점(초)입니다.")]
        [FormerlySerializedAs("ImpactTriggerTime")]
        [Range(0.0f, 5.0f)]
        [SerializeField] private float m_impactTriggerTime = 1.07f;
        
        [Tooltip("공격 판정 후, 다음 동작으로 넘어가기 전 대기하는 시간(초)입니다. (후딜레이)")]
        [FormerlySerializedAs("FollowThroughDelay")]
        [Range(0.0f, 2.0f)]
        [SerializeField] private float m_followThroughDelay = 0.5f;

        [Header("2. 진화 무기(부메랑) 설정")]
        [Tooltip("방패 파편(부메랑)이 날아가는 속도입니다.")]
        [FormerlySerializedAs("BoomerangSpeed")]
        [Range(1.0f, 20.0f)]
        [SerializeField] private float m_boomerangSpeed = 5f;
        
        [Tooltip("부메랑이 최대 거리 도달 후 돌아오기 전 대기하는 시간(초)입니다.")]
        [FormerlySerializedAs("ReturnDelay")]
        [Range(0.0f, 2.0f)]
        [SerializeField] private float m_returnDelay = 0.1f;
        
        [Tooltip("부메랑의 초당 회전 수입니다. (높을수록 빨리 돔)")]
        [FormerlySerializedAs("RotationsPerSecond")]
        [Range(0.0f, 10.0f)]
        [SerializeField] private float m_rotationsPerSecond = 2.5f;

        #endregion

        #region 2. 공개 프로퍼티 (Properties)

        /// <summary>
        /// 임팩트(충격파) 발생 트리거 시간
        /// </summary>
        public float ImpactTriggerTime => m_impactTriggerTime;

        /// <summary>
        /// 공격 후 마무리 동작 지연 시간
        /// </summary>
        public float FollowThroughDelay => m_followThroughDelay;

        /// <summary>
        /// 부메랑 비행 속도
        /// </summary>
        public float BoomerangSpeed => m_boomerangSpeed;

        /// <summary>
        /// 부메랑 반환 대기 시간
        /// </summary>
        public float ReturnDelay => m_returnDelay;

        /// <summary>
        /// 부메랑 초당 회전 속도
        /// </summary>
        public float RotationsPerSecond => m_rotationsPerSecond;

        #endregion

        #region 3. 유효성 검사 (Validation)
        
        /// <summary>
        /// 에디터에서 값이 변경될 때 실시간으로 범위를 제한합니다.
        /// </summary>
        private void OnValidate()
        {
            m_impactTriggerTime = Mathf.Max(0.1f, m_impactTriggerTime);
            m_boomerangSpeed = Mathf.Max(1.0f, m_boomerangSpeed);
        }

        #endregion
    }
}