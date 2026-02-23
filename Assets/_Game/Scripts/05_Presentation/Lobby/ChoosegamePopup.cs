using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace InGame
{
    /// <summary>
    /// [설명]: 게임 선택 팝업창을 관리하는 클래스입니다.
    /// 다양한 게임 모드로 진입하거나 팝업을 닫는 기능을 담당합니다.
    /// </summary>
    public class ChoosegamePopup : MonoBehaviour
    {
        #region 에디터 설정 (Inspector)

        [SerializeField, Tooltip("게임 선택 버튼 리스트"), FormerlySerializedAs("CgameBtn")]
        private List<Button> m_gameButtons = new List<Button>();

        [SerializeField, Tooltip("닫기 버튼"), FormerlySerializedAs("exitBtn")]
        private Button m_exitButton;

        [SerializeField, Tooltip("메시지 관리자"), FormerlySerializedAs("messageManager")]
        private MessageManager m_messageManager;

        #endregion

        #region 내부 변수

        private ISceneLoader m_sceneLoader;

        #endregion

        #region 유니티 생명주기

        private void Start()
        {
            // 방어 로직: Initialize가 호출되지 않았을 경우를 대비
            InitializeButtons();
        }

        #endregion

        #region 초기화 로직

        /// <summary>
        /// [설명]: 외부로부터 의존성을 주입받아 초기화합니다.
        /// </summary>
        public void Initialize(ISceneLoader sceneLoader)
        {
            m_sceneLoader = sceneLoader;
            
            if (m_messageManager != null)
            {
                m_messageManager.Initialize(m_sceneLoader);
            }

            InitializeButtons();
        }

        /// <summary>
        /// [설명]: 버튼 이벤트 리스너를 초기화합니다.
        /// </summary>
        private void InitializeButtons()
        {
            for (int i = 0; i < m_gameButtons.Count; i++)
            {
                int index = i;
                m_gameButtons[i].onClick.AddListener(() =>
                {
                    OnGameButtonClicked(index);
                    LogManager.Log("[ChoosegamePopup] 팝업 활성화", LogManager.LogCategory.System);
                });
            }

            if (m_exitButton != null)
            {
                m_exitButton.onClick.AddListener(OnExitButtonClicked);
            }
        }

        #endregion

        #region 내부 비즈니스 로직

        /// <summary>
        /// [설명]: 선택된 인덱스에 따라 각 게임 모드 진입 로직을 실행합니다.
        /// </summary>
        /// <param name="funcNum">버튼 인덱스</param>
        private void OnGameButtonClicked(int funcNum)
        {
            LogManager.Log($"[ChoosegamePopup] 게임 모드 선택됨: {funcNum}", LogManager.LogCategory.System);

            switch (funcNum)
            {
                case 0:
                    EnterVamsirlike();
                    break;
                case 1:
                    EnterGroundGame();
                    break;
                case 2:
                    EnterShootingGame();
                    break;
                default:
                    if (m_messageManager != null)
                    {
                        m_messageManager.OnEmptyGameMessage();
                    }

                    break;
            }
        }

        /// <summary>
        /// [설명]: 팝업을 닫습니다.
        /// </summary>
        private void OnExitButtonClicked()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// [설명]: 구 버전 스크립트입니다. 씬 진입은 LobbyMainMenuView에서 payload를 포함해 수행합니다.
        /// 이 메서드는 중복 입력 충돌을 방지하기 위해 더 이상 직접 씬 로더를 호출하지 않습니다.
        /// </summary>
        private void EnterVamsirlike()
        {
            LogManager.Log("[ChoosegamePopup] (구버전) 뱀서라이크 게임 진입 로직은 무시됩니다. (LobbyMainMenuView에서 처리)", LogManager.LogCategory.System);
        }

        /// <summary>
        /// [설명]: 구 버전 스크립트입니다. 땅따먹기 게임 모드로 진입합니다. (현재 미구현)
        /// </summary>
        private void EnterGroundGame()
        {
            LogManager.Log("[ChoosegamePopup] (구버전) 땅따먹기 게임 시작 시도", LogManager.LogCategory.System);
        }

        /// <summary>
        /// [설명]: 구 버전 스크립트입니다. 슈팅 게임 모드로 진입합니다. (현재 미구현)
        /// </summary>
        private void EnterShootingGame()
        {
            LogManager.Log("[ChoosegamePopup] (구버전) 슈팅 게임 진입 시도", LogManager.LogCategory.System);
        }

        #endregion
    }
}