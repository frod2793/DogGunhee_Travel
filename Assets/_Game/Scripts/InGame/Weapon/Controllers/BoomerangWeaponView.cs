using UnityEngine;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 부메랑(Boomerang) 무기의 프리팹 설정 및 시각적 데이터를 관리하는 컴포넌트입니다.
    /// </summary>
    public class BoomerangWeaponView : MonoBehaviour
    {
        [Header("Firing Pattern")]
        [Tooltip("발사 시 시작 각도 오프셋입니다.")]
        public float StartAngle = -15f;
        
        [Tooltip("발사체 간의 각도 간격입니다.")]
        public float AngleStep = 30f;
        
        [Tooltip("연사 발사 시 간격(ms)입니다.")]
        public int BurstDelayMs = 50;
    }
}
