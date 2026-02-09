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
        private SmellWeaponController m_controller;

        public void Initialize(SmellWeaponController controller)
        {
            m_controller = controller;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (m_controller != null)
            {
                m_controller.ProcessTriggerDamage(other);
            }
        }
    }
}
