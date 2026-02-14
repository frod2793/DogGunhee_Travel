using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using InGame.vamsir;

namespace InGame.UI.Views
{
    /// <summary>
    /// [설명]: 레벨업 시 발생하는 스킬 선택 이벤트를 시각화하고 관리하는 View 클래스입니다.
    /// 랜덤하게 제시된 스킬 버튼들의 오브젝트 풀링을 관리하며, 선택 시의 시각적 피드백(애니메이션) 및 타이머 동기화 로직을 담당합니다.
    /// </summary>
    public class InGameSkillView : MonoBehaviour
    {
        #region 에디터 설정

        [Header("패널 및 컨테이너")]
        [SerializeField, Tooltip("스킬 선택 화면을 구성하는 최상위 팝업 패널")]
        private GameObject m_skillSelectionPanel;

        [SerializeField, Tooltip("동적으로 생성된 스킬 버튼들을 정렬하여 담을 부모 컨테이너")]
        private GameObject m_skillButtonContainer;

        [Header("UI 컨트롤")]
        [SerializeField, Tooltip("제시된 스킬 목록을 다시 굴리기(Reroll) 위한 새로고침 버튼")]
        private Button m_refreshButton;

        [SerializeField, Tooltip("스킬 버튼 항목으로 사용될 프리팹 참조")]
        private SelectSkillBtnPrefab m_skillSelectionButtonPrefab;

        [Header("타이머 및 보조 UI")]
        [SerializeField, Tooltip("스킬 선택 제한 시간을 숫자로 표시할 텍스트")]
        private TMP_Text m_countdownText;

        [SerializeField, Tooltip("남은 선택 시간을 시각적 게이지로 표시할 슬라이더")]
        private Slider m_countDownSlider;

        #endregion

        #region 내부 필드

        /// <summary> 새로고침 버튼 클릭 시 외부(Presenter/ViewModel)로 전달할 요청 콜백 </summary>
        private Action m_onRefreshRequested;

        /// <summary> 버튼의 잦은 메모리 할당을 방지하기 위해 재사용되는 위젯 인스턴스 목록 </summary>
        private readonly List<SelectSkillBtnPrefab> m_skillButtonPool = new List<SelectSkillBtnPrefab>();

        #endregion

        #region 유니티 생명주기

        /// <summary>
        /// [설명]: 뷰 파기 시 버튼 리스너를 명시적으로 해제하여 참조 누수를 방지합니다.
        /// </summary>
        private void OnDestroy()
        {
            if (m_refreshButton != null)
            {
                m_refreshButton.onClick.RemoveAllListeners();
            }
        }

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 뷰의 가동에 필요한 기초 데이터를 설정하고 하드웨어 상호작용(버튼)을 바인딩합니다.
        /// </summary>
        /// <param name="onRefresh">새로고침이 발생했을 때 수행할 로직 대리자</param>
        public void Initialize(Action onRefresh)
        {
            m_onRefreshRequested = onRefresh;

            if (m_refreshButton != null)
            {
                m_refreshButton.onClick.RemoveAllListeners();
                m_refreshButton.onClick.AddListener(() =>
                {
                    if (m_onRefreshRequested != null)
                    {
                        m_onRefreshRequested.Invoke();
                    }
                });
            }
        }

        #endregion

        #region UI 상태 제어

        /// <summary>
        /// [설명]: 스킬 선택 패널의 활성 상태를 물리적으로 토글하고 부수적인 타이머 위젯들의 가시성을 동기화합니다.
        /// </summary>
        /// <param name="active">표시 여부 플래그</param>
        public void Show(bool active)
        {
            if (m_skillSelectionPanel == null)
            {
                Debug.LogWarning("[InGameSkillView] m_skillSelectionPanel 참조가 누락되었습니다.");
                return;
            }

            m_skillSelectionPanel.SetActive(active);

            // 팝업 가시성에 따른 타이머 연동
            if (!active)
            {
                if (m_countdownText != null)
                {
                    m_countdownText.gameObject.SetActive(false);
                }
                
                if (m_countDownSlider != null)
                {
                    m_countDownSlider.gameObject.SetActive(false);
                }
            }
            else
            {
                if (m_countdownText != null)
                {
                    m_countdownText.gameObject.SetActive(true);
                }
                
                if (m_countDownSlider != null)
                {
                    m_countDownSlider.gameObject.SetActive(true);
                }
            }
        }

        /// <summary>
        /// [설명]: 현재 진행 중인 스킬 선택 타이머 수치를 위젯에 실시간으로 투영합니다.
        /// </summary>
        /// <param name="normalizedTime">0~1 사이의 정규화된 시간 값 (슬라이더용)</param>
        /// <param name="secondsRemaining">정수 형태의 남은 초 (텍스트용)</param>
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

        #region 스킬 선택 및 풀링

        /// <summary>
        /// [설명]: 전달된 스킬 후보군 데이터를 기반으로 선택 버튼들을 갱신하거나 새로 생성(풀링)합니다.
        /// </summary>
        /// <param name="choices">제시할 스킬 데이터 리스트</param>
        /// <param name="onSelected">최종 선택 시 호출될 콜백</param>
        public void RefreshSkillChoices(List<SkillData> choices, Action<SkillData> onSelected)
        {
            if (m_skillButtonContainer == null || m_skillSelectionButtonPrefab == null)
            {
                return;
            }

            // 1. 기존에 노출되어 있던 모든 버튼을 숨겨 풀(Pool)로 반환 처리
            for (int j = 0; j < m_skillButtonPool.Count; j++)
            {
                if (m_skillButtonPool[j] != null)
                {
                    m_skillButtonPool[j].gameObject.SetActive(false);
                }
            }

            // 2. 새로운 데이터에 맞춰 버튼 활성화 및 바인딩
            for (int i = 0; i < choices.Count; i++)
            {
                SelectSkillBtnPrefab btn;

                if (i < m_skillButtonPool.Count)
                {
                    btn = m_skillButtonPool[i];
                }
                else
                {
                    btn = Instantiate(m_skillSelectionButtonPrefab, m_skillButtonContainer.transform);
                    m_skillButtonPool.Add(btn);
                }

                if (btn != null)
                {
                    btn.gameObject.SetActive(true);
                    
                    // 클로저 캡처 주의: choices[i]의 참조를 전달하거나 로컬 변수 활용
                    SkillData currentData = choices[i];
                    btn.Setup(currentData, skill =>
                    {
                        if (onSelected != null)
                        {
                            onSelected.Invoke(skill);
                        }
                    });
                }
            }
        }

        /// <summary>
        /// [설명]: 특정 스킬이 선택되었을 때 해당 버튼의 클릭 시각 연출 트윈을 명시적으로 실행하고 완료를 대기합니다.
        /// </summary>
        /// <param name="skill">선택된 스킬 데이터</param>
        public async UniTask PlaySelectionAnimation(SkillData skill)
        {
            for (int i = 0; i < m_skillButtonPool.Count; i++)
            {
                var btn = m_skillButtonPool[i];
                
                // 현재 활성 상태이며 매칭되는 데이터를 가진 버튼 검색
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