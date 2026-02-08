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
        public float HitCooldown = 0.5f;

        [Header("Trail Settings")]
        public float TrailTime = 0.3f;
        public float TrailStartWidth = 0.2f;
        public float TrailEndWidth = 0.0f;

        [Header("Trail Colors")]
        public Color TrailColorLv1 = new Color(1f, 1f, 1f, 0.5f);
        public Color TrailColorLv2 = new Color(1f, 0f, 1f, 0.5f);
    }
}
