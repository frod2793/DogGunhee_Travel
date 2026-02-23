using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using System;
using Lobby.Core;
using Lobby.UI;

namespace Lobby.UI.Elements
{
    /// <summary>
    /// [설명]: 로비의 메인 버튼들과 게임 모드 선택 팝업 등을 관리하는 뷰 컴포넌트입니다.
    /// </summary>
    public class LobbyMainMenuView : MonoBehaviour
    {
        #region 에디터 설정
        [Header("메인 버튼")]
        [SerializeField] private Button m_startBtn;
        [SerializeField] private Button m_tutorialBtn;
        [SerializeField] private Button m_optionBtn;

        [Header("게임 선택 팝업")]
        [SerializeField] private GameObject m_gameSelectPopUp;
        [SerializeField] private Button m_closeGameSelectPopUpBtn;
        [SerializeField] private Button m_gameStartButton;
        [SerializeField] private Button m_gametestStartButton;

        [Header("서브 시스템 버튼")]
        [SerializeField] private Button m_openPostButton;
        [SerializeField] private Button m_openQuestPanelButton;
        [SerializeField] private Button m_openStoreButton;
        [SerializeField, FormerlySerializedAs("m_openItemSelectButton")]
        private Button m_openInventoryButton;

        [Header("상점/우편/퀘스트 제어 버튼")]
        [SerializeField] private Button m_getPostRewardButton;
        [SerializeField] private Button m_closePostButton;
        [SerializeField] private Button m_getPostExpansionRewardButton;
        [SerializeField] private Button m_closePostExpansionButton;
        [SerializeField] private Button m_closeQuestPanelButton;
        [SerializeField] private Button m_closeQuestExpansionButton;
        [SerializeField] private Button m_closeStoreButton;
        [SerializeField] private Button m_closeStoreExpendPopUp;
        [SerializeField, FormerlySerializedAs("m_closeItemSelectButton")]
        private Button m_closeInventoryButton;
        [SerializeField, FormerlySerializedAs("m_closeItemSelectExpansionButton")]
        private Button m_closeInventoryExpansionButton;
        #endregion

        #region 초기화 및 이벤트 바인딩
        /// <summary>
        /// [설명]: 각 버튼의 이벤트를 초기화합니다.
        /// </summary>
        public void Initialize(ILobbyNavigator navigator, ILobbySubSystemService subSystemService, InGame.Data.ScenePayloadDTO payload)
        {
            if (navigator == null || subSystemService == null) return;

            // 메인 버튼
            m_startBtn.SetOnClick(() => OpenGameSelectPopup(navigator));
            m_tutorialBtn.SetOnClick(() => navigator.LoadScene("Tutorial", payload));
            m_optionBtn.SetOnClick(navigator.OpenOptionPopup);

            // 게임 선택 팝업
            m_closeGameSelectPopUpBtn.SetOnClick(navigator.CloseTopPopup);
            m_gameStartButton.SetOnClick(() => navigator.LoadScene("VamSerlike", payload));
            m_gametestStartButton.SetOnClick(() => navigator.LoadScene("VamSerLike_Test", payload));

            // 서브 시스템 열기
            m_openPostButton.SetOnClick(subSystemService.OpenPostBox);
            m_openQuestPanelButton.SetOnClick(subSystemService.OpenQuestPanel);
            m_openStoreButton.SetOnClick(subSystemService.OpenStore);
            
            if (m_openInventoryButton != null)
            {
                m_openInventoryButton.SetOnClick(() => {
                    Debug.Log("[LobbyMainMenuView] Inventory Button Clicked");
                    subSystemService.OpenInventory();
                });
            }
            else
            {
                Debug.LogError("[LobbyMainMenuView] m_openInventoryButton is NULL!");
            }

            // 서브 시스템 제어
            m_closePostButton.SetOnClick(navigator.CloseTopPopup);
            m_closePostExpansionButton.SetOnClick(navigator.CloseTopPopup);
            m_getPostRewardButton.SetOnClick(subSystemService.GetPostReward);
            m_getPostExpansionRewardButton.SetOnClick(subSystemService.GetPostReward);

            m_closeQuestPanelButton.SetOnClick(navigator.CloseTopPopup);
            m_closeQuestExpansionButton.SetOnClick(navigator.CloseTopPopup);

            m_closeStoreButton.SetOnClick(navigator.CloseTopPopup);
            m_closeStoreExpendPopUp.SetOnClick(navigator.CloseTopPopup);

            m_closeInventoryButton.SetOnClick(navigator.CloseTopPopup);
            m_closeInventoryExpansionButton.SetOnClick(navigator.CloseTopPopup);
        }

        private void OpenGameSelectPopup(ILobbyNavigator navigator)
        {
            if (m_gameSelectPopUp == null) return;
            
            m_gameSelectPopUp.SetActive(true);
            navigator.RegisterPopup(() => m_gameSelectPopUp.SetActive(false));
        }
        #endregion
    }
}
