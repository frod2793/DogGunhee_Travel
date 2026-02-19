using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InGame.UI.Views
{
    /// <summary>
    /// [설명]: 게임 오버 시 최종 결과(코인, 웨이브, 킬 합계)를 요약 표시하고,
    /// 플레이어에게 재시작 혹은 로비 이동(종료) 선택지를 제공하는 팝업 View입니다.
    /// </summary>
    public class GameOverPopup : MonoBehaviour
    {
        #region 에디터 설정

        [Header("패널 및 레이아웃")]
        [SerializeField, Tooltip("팝업의 가시성을 물리적으로 제어하는 부모 패널 오브젝트")]
        private GameObject m_panel;

        [Header("텍스트 UI")]
        [SerializeField, Tooltip("게임 종료를 알리는 상단 제목 텍스트")]
        private TMP_Text m_titleText;

        [SerializeField, Tooltip("이번 판에서 최종 획득한 코인 수량을 표시하는 텍스트")]
        private TMP_Text m_coinText;

        [SerializeField, Tooltip("플레이어가 버텨낸 최대 웨이브 단계를 표시하는 텍스트")]
        private TMP_Text m_waveText;

        [SerializeField, Tooltip("누적 처치한 몬스터의 총합을 표시하는 텍스트")]
        private TMP_Text m_mobCountText;

        [Header("버튼 UI")]
        [SerializeField, Tooltip("현재 스테이지를 다시 시작하거나 처음부터 발동하는 버튼")]
        private Button m_restartButton;

        [SerializeField, Tooltip("현재 게임 세션을 종료하고 메인 화면으로 복귀하는 버튼")]
        private Button m_exitButton;

        #endregion

        #region 유니티 생명주기

        /// <summary>
        /// [설명]: 오브젝트 파기 시 등록된 모든 UI 리스너를 제거하여 메모리 참조 무결성을 유지합니다.
        /// </summary>
        private void OnDestroy()
        {
            // Unity Native Null Check 기반 리스너 초기화
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

        #region 초기화

        /// <summary>
        /// [설명]: 팝업 내 버튼 클릭 시 수행될 외부 액션들을 바인딩하고 리스너를 구성합니다.
        /// </summary>
        /// <param name="onRestart">재시작 로직 대리자</param>
        /// <param name="onExit">로비 이동 로직 대리자</param>
        public void Setup(System.Action onRestart, System.Action onExit)
        {
            // 재시작 버튼 이벤트 연결
            if (m_restartButton != null)
            {
                m_restartButton.onClick.RemoveAllListeners();

                if (onRestart != null)
                {
                    m_restartButton.onClick.AddListener(() => onRestart.Invoke());
                }
            }

            // 종료 버튼 이벤트 연결
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

        #region 공개 인터페이스

        /// <summary>
        /// [설명]: 결과 수치들을 UI 텍스트에 적용하고 팝업 패널을 활성화하여 화면에 표시합니다.
        /// </summary>
        /// <param name="coins">최종 획득 코인</param>
        /// <param name="wave">도달 웨이브</param>
        /// <param name="mobKills">총 처치 수</param>
        public void Show(int coins, int wave, int mobKills)
        {
            // 수치 동기화 (Unity Native Null Check)
            if (m_coinText != null)
            {
                m_coinText.SetText("코인: {0}", coins);
            }

            if (m_waveText != null)
            {
                m_waveText.SetText("웨이브: {0}", wave);
            }

            if (m_mobCountText != null)
            {
                m_mobCountText.SetText("처치 수: {0}", mobKills);
            }

            // 패널 활성화
            if (m_panel != null)
            {
                m_panel.SetActive(true);
            }
        }

        /// <summary>
        /// [설명]: 활성화된 게임 오버 팝업을 즉시 비활성화 처리합니다.
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