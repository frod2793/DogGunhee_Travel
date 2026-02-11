using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace InGame
{
    /// <summary>
    /// 게임 선택 팝업창을 관리하는 클래스입니다.
    /// 다양한 게임 모드로 진입하거나 팝업을 닫는 기능을 담당합니다.
    /// </summary>
    public class ChoosegamePopup : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [SerializeField, Tooltip("게임 선택 버튼 리스트"), FormerlySerializedAs("CgameBtn")]
        private List<Button> m_gameButtons = new List<Button>();

        [SerializeField, Tooltip("닫기 버튼"), FormerlySerializedAs("exitBtn")]
        private Button m_exitButton;

        [SerializeField, Tooltip("메시지 관리자"), FormerlySerializedAs("messageManager")]
        private MessageManager m_messageManager;

        #endregion

        #region 2. 유니티 생명주기

        private void Start()
        {
            InitializeButtons();
        }

        #endregion

        #region 3. 초기화 로직

        /// <summary>
        /// 버튼 이벤트 리스너를 초기화합니다.
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

        #region 4. 내부 로직

        /// <summary>
        /// 선택된 인덱스에 따라 각 게임 모드 진입 로직을 실행합니다.
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
        /// 팝업을 닫습니다.
        /// </summary>
        private void OnExitButtonClicked()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 뱀파 서바이버라이크 게임 씬으로 이동합니다.
        /// </summary>
        private void EnterVamsirlike()
        {
            SceneLoader.Instance.LoadVamSerLikeScene();
            LogManager.LogError("메시지 매니저가 유효하지 않습니다.", LogManager.LogCategory.System);
        }

        /// <summary>
        /// 땅따먹기 게임 모드로 진입합니다. (현재 미구현)
        /// </summary>
        private void EnterGroundGame()
        {
            if (m_messageManager != null)
            {
                m_messageManager.OnEmptyGameMessage();
            }

            LogManager.Log("[ChoosegamePopup] 뱀서라이크 게임 시작", LogManager.LogCategory.System);
        }

        /// <summary>
        /// 슈팅 게임 모드로 진입합니다. (현재 미구현)
        /// </summary>
        private void EnterShootingGame()
        {
            if (m_messageManager != null)
            {
                m_messageManager.OnEmptyGameMessage();
            }

            LogManager.Log("[ChoosegamePopup] 슈팅 게임 진입 시도 (미구현)", LogManager.LogCategory.System);
        }

        #endregion
    }
}