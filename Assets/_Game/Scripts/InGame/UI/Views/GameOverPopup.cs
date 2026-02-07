using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InGame.UI.Views
{
    /// <summary>
    /// 게임 오버 시 결과를 표시하는 팝업 클래스입니다.
    /// </summary>
    public class GameOverPopup : MonoBehaviour
    {
        #region UI 컴포넌트

        [SerializeField] private GameObject m_panel;
        [SerializeField] private TMP_Text m_titleText;
        [SerializeField] private TMP_Text m_coinText;
        [SerializeField] private TMP_Text m_waveText;
        [SerializeField] private TMP_Text m_mobCountText;

        [SerializeField] private Button m_restartButton;
        [SerializeField] private Button m_exitButton;

        #endregion

        public void Setup(System.Action onRestart, System.Action onExit)
        {
            m_restartButton?.onClick.AddListener(() => onRestart?.Invoke());
            m_exitButton?.onClick.AddListener(() => onExit?.Invoke());
        }

        public void Show(int coins, int wave, int mobKills)
        {
            m_coinText.SetText("코인: {0}", coins);
            m_waveText.SetText("웨이브: {0}", wave);
            m_mobCountText.SetText("처치 수: {0}", mobKills);
            
            m_panel.SetActive(true);
        }

        public void Hide()
        {
            m_panel.SetActive(false);
        }
    }
}
