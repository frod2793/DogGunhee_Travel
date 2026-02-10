using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 뼈다귀(Bone) 무기의 물리 동작 및 시각적 설정을 관리하는 뷰 컴포넌트입니다.
    /// <br/> WeaponPoolManager나 무기 프리팹에 부착되어 로직에서 참조합니다.
    /// </summary>
    public class BoneWeaponView : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("1. 투사체 물리 설정")]
        [Tooltip("뼈 투사체가 날아가는 초기 속도입니다. (권장값: 5 ~ 20)")]
        [FormerlySerializedAs("BoneSpeed")]
        [Range(1f, 50f)] // 너무 빠르거나 느리지 않도록 제한
        [SerializeField] private float m_boneSpeed = 10f;

        #endregion

        #region 2. 공개 프로퍼티 (Properties)

        /// <summary>
        /// 투사체 비행 속도
        /// </summary>
        public float BoneSpeed => m_boneSpeed;

        #endregion
    }
}