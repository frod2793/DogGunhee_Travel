using UnityEngine;

namespace InGame.Weapon.Controllers
{
    /// <summary>
    /// 향긋한 꼬순내(Smell) 무기의 물리 충돌 이벤트를 감지하여 컨트롤러로 전달하는 뷰 컴포넌트입니다.
    /// <br/> 실제 데미지 로직은 Controller에 위임하며, 이 클래스는 Unity Physics Event의 진입점 역할만 수행합니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))] // 이 컴포넌트는 콜라이더가 필수입니다.
    public class SmellWeaponView : MonoBehaviour
    {
        #region 1. 내부 변수 (Internal State)

        /// <summary>
        /// 이벤트를 전달할 대상 컨트롤러 참조
        /// </summary>
        private SmellWeaponController m_controller;

        #endregion

        #region 2. 초기화 (Initialization)

        /// <summary>
        /// 컨트롤러 인스턴스를 주입받아 연결합니다.
        /// </summary>
        /// <param name="controller">로직을 처리할 SmellWeaponController</param>
        public void Init(SmellWeaponController controller)
        {
            m_controller = controller;
        }

        #endregion

        #region 3. 유니티 이벤트 (Unity Physics Events)

        /// <summary>
        /// 콜라이더 영역 내에 객체가 머무를 때 매 물리 프레임마다 호출됩니다.
        /// </summary>
        /// <param name="other">충돌한 객체의 Collider2D</param>
        private void OnTriggerStay2D(Collider2D other)
        {
            // 컨트롤러가 유효하다면 충돌 처리를 위임
            if (m_controller != null)
            {
                m_controller.ProcessTriggerDamage(other);
            }
        }

        #endregion
    }
}