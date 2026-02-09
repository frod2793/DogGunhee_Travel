using UnityEngine;
using UnityEngine.UI;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어 캐릭터 상단에 표시되는 체력 바(HP Slider)의 생명주기와 갱신을 관리하는 POCO 클래스입니다.
    /// </summary>
    public class PlayerUIHandler
    {
        #region 내부 상태 및 캐시

        private readonly Slider m_hpSlider;

        #endregion

        #region 초기화 및 제어

        public PlayerUIHandler(Slider hpSliderPrefab, Transform parent)
        {
            if (hpSliderPrefab != null && parent != null)
            {
                // 월드 스페이스 UI로 프리팹 생성 및 부모 설정
                m_hpSlider = Object.Instantiate(hpSliderPrefab, parent);
                m_hpSlider.transform.localPosition = new Vector3(0, -0.4f, 0);
            }
        }

        /// <summary>
        /// 현재 체력 수치를 UI에 반영합니다.
        /// </summary>
        public void UpdateHpUI(float current, float max)
        {
            if (m_hpSlider != null)
            {
                m_hpSlider.maxValue = max;
                m_hpSlider.value = current;
            }
        }

        /// <summary>
        /// 생성된 UI 오브젝트를 제거합니다.
        /// </summary>
        public void Dispose()
        {
            if (m_hpSlider != null)
            {
                Object.Destroy(m_hpSlider.gameObject);
            }
        }

        #endregion
    }
}
