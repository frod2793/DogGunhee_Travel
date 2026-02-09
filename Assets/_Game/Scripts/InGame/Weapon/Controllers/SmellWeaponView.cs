using UnityEngine;
using InGame.Weapon.Controllers;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 향긋한 꼬순내(SmellWeapon)의 투사체(흔적) 프리팹에 부착되어
    /// 물리 충돌 이벤트를 Controller로 전달하는 View 클래스입니다.
    /// </summary>
    public class SmellWeaponView : MonoBehaviour
    {
        #region 내부 상태 및 변수

        private SmellWeaponController m_controller;

        #endregion

        #region 초기화 및 제어

        /// <summary>
        /// 뷰를 컨트롤러와 연결합니다.
        /// </summary>
        public void Init(SmellWeaponController controller)
        {
            m_controller = controller;
        }

        #endregion

        #region Unity 라이프사이클

        private void OnTriggerStay2D(Collider2D other)
        {
            if (m_controller != null)
            {
                m_controller.ProcessTriggerDamage(other);
            }
        }

        #endregion
    }
}
