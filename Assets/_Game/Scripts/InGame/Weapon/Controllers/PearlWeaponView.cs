using UnityEngine;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 싸구려 진주 무기의 비주얼 및 전투 파라미터를 튜닝하는 View 클래스입니다.
    /// </summary>
    public class PearlWeaponView : MonoBehaviour
    {
        #region 설정 데이터

        [Header("전투 튜닝")]
        [Tooltip("동일 몬스터 타격 간격 (초)")]
        [UnityEngine.Serialization.FormerlySerializedAs("HitCooldown")]
        [SerializeField] private float m_hitCooldown = 0.5f;

        [Header("트레일 설정")]
        [UnityEngine.Serialization.FormerlySerializedAs("TrailTime")]
        [SerializeField] private float m_trailTime = 0.3f;

        [UnityEngine.Serialization.FormerlySerializedAs("TrailStartWidth")]
        [SerializeField] private float m_trailStartWidth = 0.2f;

        [UnityEngine.Serialization.FormerlySerializedAs("TrailEndWidth")]
        [SerializeField] private float m_trailEndWidth = 0.0f;

        [Header("트레일 색상")]
        [UnityEngine.Serialization.FormerlySerializedAs("TrailColorLv1")]
        [SerializeField] private Color m_trailColorLv1 = new Color(1f, 1f, 1f, 0.5f);

        [UnityEngine.Serialization.FormerlySerializedAs("TrailColorLv2")]
        [SerializeField] private Color m_trailColorLv2 = new Color(1f, 0f, 1f, 0.5f);

        #endregion

        #region 프로퍼티

        public float HitCooldown => m_hitCooldown;
        public float TrailTime => m_trailTime;
        public float TrailStartWidth => m_trailStartWidth;
        public float TrailEndWidth => m_trailEndWidth;
        public Color TrailColorLv1 => m_trailColorLv1;
        public Color TrailColorLv2 => m_trailColorLv2;

        #endregion
    }
}
