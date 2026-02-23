using UnityEngine;
using UnityEngine.UI;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// [설명]: 플레이어 캐릭터 주변(World Space)에 시각적으로 표시되는 UI 위젯들을 관리하는 로직 클래스입니다.
    /// 체력바(HP Slider)의 생성, 실시간 수치 동기화 및 생명주기에 따른 파괴 관리를 담당합니다.
    /// </summary>
    public class PlayerUIHandler
    {
        #region 내부 필드

        /// <summary> 플레이어 상단 또는 하단에 배치된 체력 게이지 UI 객체 </summary>
        private readonly Slider m_hpSlider;

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: UI 전용 핸들러를 구성하며 필수 위젯(체력바)을 지정된 위치에 생성 및 배치합니다.
        /// </summary>
        /// <param name="hpSliderPrefab">UI 인스턴스화에 사용될 슬라이더 프리팹</param>
        /// <param name="parent">UI가 추적하며 부착될 부모 오브젝트의 트랜스폼</param>
        /// <param name="offset">부모 위치를 기준으로 한 상대적 배치 간격</param>
        public PlayerUIHandler(Slider hpSliderPrefab, Transform parent, Vector3? offset = null)
        {
            if (hpSliderPrefab == null || parent == null)
            {
                return;
            }

            // 프리팹을 지정된 부모 아래에 생성
            m_hpSlider = Object.Instantiate(hpSliderPrefab, parent);

            // 로컬 좌표계 오프셋 설정 (기본값 하단 배치)
            Vector3 localPos = offset ?? new Vector3(0, -0.5f, 0);
            m_hpSlider.transform.localPosition = localPos;
        }

        #endregion

        #region 공개 인터페이스

        /// <summary>
        /// [설명]: 플레이어의 현재 체력 비율을 UI 슬라이더 프로퍼티에 동기화합니다.
        /// </summary>
        /// <param name="currentHp">반영할 현재 체력 수치</param>
        /// <param name="maxHp">반영할 최대 체력 기준값</param>
        public void UpdateHpUI(float currentHp, float maxHp)
        {
            if (m_hpSlider == null)
            {
                return;
            }

            // 최대치 변경이 필요할 때만 갱신하여 연산 최소화
            if (!Mathf.Approximately(m_hpSlider.maxValue, maxHp))
            {
                m_hpSlider.maxValue = maxHp;
            }

            m_hpSlider.value = currentHp;
        }

        /// <summary>
        /// [설명]: 핸들러가 소유한 월드 공간 UI 객체들을 파기하고 메모리를 정리합니다.
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