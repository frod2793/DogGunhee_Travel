using UnityEngine;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 싸구려 진주 무기의 비주얼 및 전투 파라미터를 튜닝하는 View 클래스입니다.
    /// </summary>
    public class PearlWeaponView : MonoBehaviour
    {
        [Header("Combat Tuning")]
        [Tooltip("동일 몬스터 타격 간격 (초)")]
        [UnityEngine.Serialization.FormerlySerializedAs("HitCooldown")]
        [SerializeField] private float m_hitCooldown = 0.5f;
        public float HitCooldown => m_hitCooldown;

        [Header("Trail Settings")]
        [UnityEngine.Serialization.FormerlySerializedAs("TrailTime")]
        [SerializeField] private float m_trailTime = 0.3f;
        public float TrailTime => m_trailTime;

        [UnityEngine.Serialization.FormerlySerializedAs("TrailStartWidth")]
        [SerializeField] private float m_trailStartWidth = 0.2f;
        public float TrailStartWidth => m_trailStartWidth;

        [UnityEngine.Serialization.FormerlySerializedAs("TrailEndWidth")]
        [SerializeField] private float m_trailEndWidth = 0.0f;
        public float TrailEndWidth => m_trailEndWidth;

        [Header("Trail Colors")]
        [UnityEngine.Serialization.FormerlySerializedAs("TrailColorLv1")]
        [SerializeField] private Color m_trailColorLv1 = new Color(1f, 1f, 1f, 0.5f);
        public Color TrailColorLv1 => m_trailColorLv1;

        [UnityEngine.Serialization.FormerlySerializedAs("TrailColorLv2")]
        [SerializeField] private Color m_trailColorLv2 = new Color(1f, 0f, 1f, 0.5f);
        public Color TrailColorLv2 => m_trailColorLv2;
    }
}
