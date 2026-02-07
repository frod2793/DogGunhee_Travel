using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using InGame.vamsir;
using InGame.UI.ViewModels;

namespace InGame.UI.Views
{
    /// <summary>
    /// 레벨업 시 나타나는 스킬 선택 팝업을 관리하는 클래스입니다.
    /// </summary>
    public class InGameSkillView : MonoBehaviour
    {
        #region UI 컴포넌트

        [SerializeField] private GameObject m_skillSelectionPanel;
        [SerializeField] private Button m_refreshButton;
        [SerializeField] private SelectSkillBtnPrefab m_skillSelectionButtonPrefab;
        [SerializeField] private GameObject m_skillButtonContainer;
        [SerializeField] private TMP_Text m_countdownText;
        [SerializeField] private Slider m_countDownSlider;

        #endregion

        private Action m_onRefreshRequested;
        private readonly List<SelectSkillBtnPrefab> m_skillButtonPool = new List<SelectSkillBtnPrefab>();

        public void Initialize(Action onRefresh)
        {
            m_onRefreshRequested = onRefresh;
            m_refreshButton?.onClick.AddListener(() => m_onRefreshRequested?.Invoke());
        }

        public void Show(bool active)
        {
            // [방어적 처리] 인스펙터 미할당 시 경고 후 반환
            if (m_skillSelectionPanel == null)
            {
                Debug.LogWarning("[InGameSkillView] m_skillSelectionPanel이 할당되지 않았습니다. 인스펙터에서 연결해주세요.");
                return;
            }
            
            m_skillSelectionPanel.SetActive(active);
            if (!active)
            {
                if (m_countdownText != null) m_countdownText.gameObject.SetActive(false);
                if (m_countDownSlider != null) m_countDownSlider.gameObject.SetActive(false);
            }
        }

        public void UpdateTimer(float normalizedTime, int secondsRemaining)
        {
            if (m_countdownText != null) m_countdownText.text = secondsRemaining.ToString();
            if (m_countDownSlider != null) m_countDownSlider.value = normalizedTime;
            
            m_countdownText?.gameObject.SetActive(true);
            m_countDownSlider?.gameObject.SetActive(true);
        }

        /// <summary>
        /// 선택 가능한 스킬 버튼들을 생성하거나 갱신합니다.
        /// </summary>
        public void RefreshSkillChoices(List<SkillData> choices, Action<SkillData> onSelected)
        {
            foreach (var btn in m_skillButtonPool) btn.gameObject.SetActive(false);

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

                btn.gameObject.SetActive(true);
                btn.Setup(choices[i], skill => onSelected?.Invoke(skill));
            }
        }

        public async UniTask PlaySelectionAnimation(SkillData skill)
        {
            foreach (var btn in m_skillButtonPool)
            {
                if (btn.gameObject.activeSelf && btn.GetCurrentSkillData() == skill)
                {
                    await btn.PlaySelectionAnimation();
                    break;
                }
            }
        }
    }
}
