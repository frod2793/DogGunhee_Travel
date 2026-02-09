using UnityEngine;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 부메랑(Boomerang) 무기의 프리팹 설정 및 시각적 데이터를 관리하는 컴포넌트입니다.
    /// </summary>
    public class BoomerangWeaponView : MonoBehaviour
    {
        #region 설정 데이터

        [Header("발사 패턴")]
        [Tooltip("발사 시 시작 각도 오프셋입니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("StartAngle")]
        [SerializeField] private float m_startAngle = -15f;
        
        [Tooltip("발사체 간의 각도 간격입니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("AngleStep")]
        [SerializeField] private float m_angleStep = 30f;
        
        [Tooltip("연사 발사 시 간격(ms)입니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("BurstDelayMs")]
        [SerializeField] private int m_burstDelayMs = 50;

        #endregion

        #region 프로퍼티

        public float StartAngle => m_startAngle;
        public float AngleStep => m_angleStep;
        public int BurstDelayMs => m_burstDelayMs;

        #endregion
    }
}
