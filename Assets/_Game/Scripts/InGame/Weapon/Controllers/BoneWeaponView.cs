using UnityEngine;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 뼈 무기(Bone)의 프리팹 설정 및 시각적 데이터를 관리하는 컴포넌트입니다.
    /// </summary>
    public class BoneWeaponView : MonoBehaviour
    {
        #region 설정 데이터

        [Header("무기 설정")]
        [Tooltip("뼈 투사체가 날아가는 속도입니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("BoneSpeed")]
        [SerializeField] private float m_boneSpeed = 10f;

        #endregion

        #region 프로퍼티

        public float BoneSpeed => m_boneSpeed;

        #endregion
    }
}
