using UnityEngine;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 신비한 불꽃(Flame) 무기의 프리팹 설정 및 시각적 데이터를 관리하는 컴포넌트입니다.
    /// </summary>
    public class FlameWeaponView : MonoBehaviour
    {
        #region 설정 데이터

        [Header("지속 데미지 설정")]
        [Tooltip("기본 데미지 대비 지속 데미지(DOT) 비율입니다. (0.5 = 50%)")]
        [UnityEngine.Serialization.FormerlySerializedAs("DotDamageRatio")]
        [SerializeField] private float m_dotDamageRatio = 0.5f;
        
        [Tooltip("적 피격 시 깜빡일 색상입니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("HitFlashColor")]
        [SerializeField] private Color m_hitFlashColor = Color.white;

        [Header("시각 효과")]
        [Tooltip("풀링할 최대 불꽃 기둥 수입니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("MaxActivePillars")]
        [SerializeField] private int m_maxActivePillars = 10;
        
        [Tooltip("오브젝트 풀링 사이즈입니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("PoolSize")]
        [SerializeField] private int m_poolSize = 20;

        #endregion

        #region 프로퍼티

        public float DotDamageRatio => m_dotDamageRatio;
        public Color HitFlashColor => m_hitFlashColor;
        public int MaxActivePillars => m_maxActivePillars;
        public int PoolSize => m_poolSize;

        #endregion
    }
}
