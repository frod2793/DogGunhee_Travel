using UnityEngine;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 뼈 무기(Bone)의 프리팹 설정 및 시각적 데이터를 관리하는 컴포넌트입니다.
    /// </summary>
    public class BoneWeaponView : MonoBehaviour
    {
        [Header("Weapon Settings")]
        [Tooltip("뼈 투사체가 날아가는 속도입니다.")]
        public float BoneSpeed = 10f;
    }
}
