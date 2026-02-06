using UnityEngine;
using UnityEngine.UI;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 체력 UI(HP 슬라이더)를 담당하는 POCO 클래스입니다.
    /// </summary>
    public class PlayerUIHandler
    {
        #region 내부 변수
        private readonly Slider m_hpSlider;
        #endregion

        #region 생성자
        public PlayerUIHandler(Slider hpSliderPrefab, Transform parent)
        {
            if (hpSliderPrefab != null && parent != null)
            {
                m_hpSlider = Object.Instantiate(hpSliderPrefab, parent);
                m_hpSlider.transform.localPosition = new Vector3(0, -0.4f, 0);
            }
        }
        #endregion

        #region UI 갱신
        public void UpdateHpUI(float current, float max)
        {
            if (m_hpSlider != null)
            {
                m_hpSlider.value = current;
                m_hpSlider.maxValue = max;
            }
        }

        /// <summary>
        /// UI 오브젝트를 파괴합니다. (소멸자 역할)
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
