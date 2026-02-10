using UnityEngine;
using UnityEngine.UI;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어 캐릭터 주변(World Space)에 표시되는 UI 요소를 관리하는 로직 클래스입니다.
    /// <br/> 체력바(Slider)의 생성, 위치 조정, 값 갱신 및 파괴를 담당합니다.
    /// </summary>
    public class PlayerUIHandler
    {
        #region 1. 내부 변수 및 캐시

        private readonly Slider m_hpSlider;

        #endregion

        #region 2. 생성자 및 초기화

        /// <summary>
        /// UI 핸들러를 초기화하고 체력바를 생성합니다.
        /// </summary>
        /// <param name="hpSliderPrefab">생성할 슬라이더 프리팹</param>
        /// <param name="parent">UI가 부착될 부모 트랜스폼 (플레이어)</param>
        /// <param name="offset">부모 기준 상대 위치 오프셋 (기본값: (0, -0.5, 0))</param>
        public PlayerUIHandler(Slider hpSliderPrefab, Transform parent, Vector3? offset = null)
        {
            if (hpSliderPrefab == null || parent == null)
            {
                // 필수 요소가 없으면 생성하지 않음
                return;
            }

            // 프리팹 인스턴스화
            m_hpSlider = Object.Instantiate(hpSliderPrefab, parent);
            
            // 위치 설정 (오프셋 적용)
            Vector3 localPos = offset ?? new Vector3(0, -0.5f, 0); // 기본값 설정
            m_hpSlider.transform.localPosition = localPos;
        }

        #endregion

        #region 3. 공개 메서드 (Public Methods)

        /// <summary>
        /// 체력바 UI의 상태를 갱신합니다.
        /// </summary>
        /// <param name="currentHp">현재 체력</param>
        /// <param name="maxHp">최대 체력</param>
        public void UpdateHpUI(float currentHp, float maxHp)
        {
            if (m_hpSlider == null) return;

            // 최대 체력이 변경되었을 때만 갱신 (최적화)
            if (!Mathf.Approximately(m_hpSlider.maxValue, maxHp))
            {
                m_hpSlider.maxValue = maxHp;
            }

            m_hpSlider.value = currentHp;
        }

        /// <summary>
        /// UI 오브젝트를 안전하게 제거하고 리소스를 정리합니다.
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