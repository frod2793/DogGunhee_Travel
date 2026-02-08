using UnityEngine;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 신비한 불꽃(Flame) 무기의 프리팹 설정 및 시각적 데이터를 관리하는 컴포넌트입니다.
    /// </summary>
    public class FlameWeaponView : MonoBehaviour
    {
        [Header("DOT Settings")]
        [Tooltip("기본 데미지 대비 지속 데미지(DOT) 비율입니다. (0.5 = 50%)")]
        public float DotDamageRatio = 0.5f;
        
        [Tooltip("적 피격 시 깜빡일 색상입니다.")]
        public Color HitFlashColor = Color.white;

        [Header("Visual Effects")]
        [Tooltip("풀링할 최대 불꽃 기둥 수입니다.")]
        public int MaxActivePillars = 10;
        
        [Tooltip("오브젝트 풀링 사이즈입니다.")]
        public int PoolSize = 20;
    }
}
