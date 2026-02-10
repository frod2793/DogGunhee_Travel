using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using InGame.vamsir; // SkillData 네임스페이스 가정

namespace InGame.UI.Views
{
    /// <summary>
    /// 레벨업 시 스킬 선택 팝업을 관리하는 View 클래스입니다.
    /// <br/> 스킬 버튼 오브젝트 풀링 및 선택 애니메이션 재생을 담당합니다.
    /// </summary>
    public class InGameSkillView : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("패널 및 컨테이너")]
        [SerializeField, Tooltip("스킬 선택 팝업 최상위 패널")] 
        private GameObject m_skillSelectionPanel;
        
        [SerializeField, Tooltip("스킬 버튼이 생성될 부모 Transform")] 
        private GameObject m_skillButtonContainer;

        [Header("UI 컨트롤")]
        [SerializeField, Tooltip("새로고침(리롤) 버튼")] 
        private Button m_refreshButton;
        
        [SerializeField, Tooltip("스킬 선택 버튼 프리팹")] 
        private SelectSkillBtnPrefab m_skillSelectionButtonPrefab;

        [Header("타이머 표시 (옵션)")]
        [SerializeField, Tooltip("남은 시간 텍스트")] 
        private TMP_Text m_countdownText;
        
        [SerializeField, Tooltip("남은 시간 슬라이더")] 
        private Slider m_countDownSlider;

        #endregion

        #region 2. 내부 변수 및 상태
        // 외부 이벤트를 중계하기 위한 액션
        private Action m_onRefreshRequested;
        
        // 버튼 재사용을 위한 오브젝트 풀
        private readonly List<SelectSkillBtnPrefab> m_skillButtonPool = new List<SelectSkillBtnPrefab>();
        #endregion

        #region 3. 유니티 생명주기
        private void OnDestroy()
        {
            // 리스너 해제 (메모리 누수 방지)
            if (m_refreshButton != null)
            {
                m_refreshButton.onClick.RemoveAllListeners();
            }
        }
        #endregion

        #region 4. 초기화 및 설정
        /// <summary>
        /// 뷰를 초기화하고 이벤트를 연결합니다.
        /// </summary>
        /// <param name="onRefresh">새로고침 버튼 클릭 시 실행될 콜백</param>
        public void Initialize(Action onRefresh)
        {
            m_onRefreshRequested = onRefresh;

            if (m_refreshButton != null)
            {
                m_refreshButton.onClick.RemoveAllListeners();
                m_refreshButton.onClick.AddListener(() => m_onRefreshRequested?.Invoke());
            }
        }
        #endregion

        #region 5. UI 제어 (Show/Hide/Update)
        /// <summary>
        /// 스킬 선택 창의 표시 여부를 설정합니다.
        /// </summary>
        public void Show(bool active)
        {
            // 방어적 처리: 필수 패널이 없으면 경고
            if (m_skillSelectionPanel == null)
            {
                Debug.LogWarning("[InGameSkillView] m_skillSelectionPanel이 할당되지 않았습니다.");
                return;
            }
            
            m_skillSelectionPanel.SetActive(active);

            // 팝업이 닫힐 때 타이머 UI도 비활성화 (필요 시)
            if (!active)
            {
                if (m_countdownText != null) m_countdownText.gameObject.SetActive(false);
                if (m_countDownSlider != null) m_countDownSlider.gameObject.SetActive(false);
            }
            else
            {
                // 팝업이 열릴 때 타이머 UI 활성화 (UpdateTimer에서 매번 켜는 것보다 효율적)
                if (m_countdownText != null) m_countdownText.gameObject.SetActive(true);
                if (m_countDownSlider != null) m_countDownSlider.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 남은 선택 시간을 UI에 표시합니다.
        /// </summary>
        /// <param name="normalizedTime">0.0 ~ 1.0 정규화된 시간 (슬라이더용)</param>
        /// <param name="secondsRemaining">남은 초 (텍스트용)</param>
        public void UpdateTimer(float normalizedTime, int secondsRemaining)
        {
            if (m_countdownText != null) 
            {
                m_countdownText.text = secondsRemaining.ToString();
            }

            if (m_countDownSlider != null) 
            {
                m_countDownSlider.value = normalizedTime;
            }
        }
        #endregion

        #region 6. 스킬 선택 로직 (Pooling)
        /// <summary>
        /// 선택 가능한 스킬 목록을 받아 버튼을 생성하거나 갱신합니다. (오브젝트 풀링 적용)
        /// </summary>
        /// <param name="choices">표시할 스킬 데이터 리스트</param>
        /// <param name="onSelected">스킬 선택 시 실행될 콜백</param>
        public void RefreshSkillChoices(List<SkillData> choices, Action<SkillData> onSelected)
        {
            if (m_skillButtonContainer == null || m_skillSelectionButtonPrefab == null) return;

            // 1. 기존 버튼 모두 비활성화 (풀링 반환 효과)
            foreach (var btn in m_skillButtonPool) 
            {
                if (btn != null) btn.gameObject.SetActive(false);
            }

            // 2. 필요한 만큼 버튼 활성화 또는 생성
            for (int i = 0; i < choices.Count; i++)
            {
                SelectSkillBtnPrefab btn;

                // 풀에 여유가 있으면 재사용
                if (i < m_skillButtonPool.Count)
                {
                    btn = m_skillButtonPool[i];
                }
                // 없으면 새로 생성
                else
                {
                    btn = Instantiate(m_skillSelectionButtonPrefab, m_skillButtonContainer.transform);
                    m_skillButtonPool.Add(btn);
                }

                // 버튼 설정 및 활성화
                if (btn != null)
                {
                    btn.gameObject.SetActive(true);
                    // 람다 캡처 주의: 버튼 내부에서 invoke 시점의 데이터가 보장되도록 Setup에 데이터 전달
                    btn.Setup(choices[i], skill => onSelected?.Invoke(skill));
                }
            }
        }

        /// <summary>
        /// 선택된 스킬 버튼의 애니메이션을 재생하고 대기합니다.
        /// </summary>
        public async UniTask PlaySelectionAnimation(SkillData skill)
        {
            foreach (var btn in m_skillButtonPool)
            {
                // 활성화된 버튼 중 선택된 스킬을 가진 버튼 찾기
                if (btn != null && btn.gameObject.activeSelf && btn.GetCurrentSkillData() == skill)
                {
                    await btn.PlaySelectionAnimation();
                    break;
                }
            }
        }
        #endregion
    }
}