using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InGame.UI.Views
{
    /// <summary>
    /// 게임 오버 시 결과(코인, 웨이브, 킬 수)를 표시하고 재시작/종료를 처리하는 팝업 View입니다.
    /// </summary>
    public class GameOverPopup : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("패널 및 레이아웃")]
        [SerializeField, Tooltip("팝업 전체를 감싸는 부모 패널")] 
        private GameObject m_panel;

        [Header("텍스트 UI")]
        [SerializeField, Tooltip("상단 타이틀 텍스트")] 
        private TMP_Text m_titleText;
        
        [SerializeField, Tooltip("획득한 코인 표시 텍스트")] 
        private TMP_Text m_coinText;
        
        [SerializeField, Tooltip("도달한 웨이브 표시 텍스트")] 
        private TMP_Text m_waveText;
        
        [SerializeField, Tooltip("처치한 몬스터 수 표시 텍스트")] 
        private TMP_Text m_mobCountText;

        [Header("버튼 UI")]
        [SerializeField, Tooltip("게임 재시작 버튼")] 
        private Button m_restartButton;
        
        [SerializeField, Tooltip("게임 종료(로비 이동) 버튼")] 
        private Button m_exitButton;

        #endregion

        #region 2. 유니티 생명주기
        private void OnDestroy()
        {
            // Unity Object 수명 검사를 위해 명시적 null 체크 사용
            if (m_restartButton != null)
            {
                m_restartButton.onClick.RemoveAllListeners();
            }

            if (m_exitButton != null)
            {
                m_exitButton.onClick.RemoveAllListeners();
            }
        }
        #endregion

        #region 3. 초기화 및 설정 (Setup)
        /// <summary>
        /// 버튼 클릭 시 실행될 콜백 이벤트를 설정합니다.
        /// </summary>
        /// <param name="onRestart">재시작 버튼 클릭 시 실행할 액션</param>
        /// <param name="onExit">종료 버튼 클릭 시 실행할 액션</param>
        public void Setup(System.Action onRestart, System.Action onExit)
        {
            // 1. 기존 리스너 제거 (Unity Object Null Check)
            if (m_restartButton != null)
            {
                m_restartButton.onClick.RemoveAllListeners();
                
                if (onRestart != null)
                {
                    m_restartButton.onClick.AddListener(() => onRestart.Invoke());
                }
            }

            if (m_exitButton != null)
            {
                m_exitButton.onClick.RemoveAllListeners();

                if (onExit != null)
                {
                    m_exitButton.onClick.AddListener(() => onExit.Invoke());
                }
            }
        }
        #endregion

        #region 4. 공개 메서드 (Control)
        /// <summary>
        /// 게임 결과 데이터를 UI에 반영하고 팝업을 표시합니다.
        /// </summary>
        /// <param name="coins">획득한 코인 수</param>
        /// <param name="wave">도달한 웨이브</param>
        /// <param name="mobKills">몬스터 처치 수</param>
        public void Show(int coins, int wave, int mobKills)
        {
            // 텍스트 컴포넌트 유효성 검사 (안전성 강화)
            if (m_coinText != null) m_coinText.SetText("코인: {0}", coins);
            if (m_waveText != null) m_waveText.SetText("웨이브: {0}", wave);
            if (m_mobCountText != null) m_mobCountText.SetText("처치 수: {0}", mobKills);
            
            // 패널 활성화
            if (m_panel != null)
            {
                m_panel.SetActive(true);
            }
        }

        /// <summary>
        /// 팝업을 숨깁니다.
        /// </summary>
        public void Hide()
        {
            if (m_panel != null)
            {
                m_panel.SetActive(false);
            }
        }
        #endregion
    }
}