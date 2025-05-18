using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DogGuns_Games.Lobby
{
    public class QuestPanelManager : MonoBehaviour
    {
        #region 필드 및 변수

        [Header("<color=green> 퀘스트 패널")] [SerializeField]
        private GameObject questPanel;
        [SerializeField] private GameObject questContainer;
        [SerializeField] private Quest_Index questPrefab;
      

        [Header("<color=green> 확장 패널")] [SerializeField]
        private GameObject questPanelExtension;

        [SerializeField] private TMP_Text questPanelExtensionText; // 상세 내용
        [SerializeField] private TMP_Text rewardItemNameText;

        #endregion

        #region Unity 라이프사이클

        private void Start()
        {
            addQuest_index();
        }

        #endregion

        #region UI 패널 관리

        public void OpenQuestPanel()
        {
            questPanel.SetActive(true);
            LobbyUIManager.AddClosePopUpAction(CloseQuestPanel);
        }
        private void CloseQuestPanel()
        {
            questPanel.SetActive(false);
          //  LobbyUIManager.closePopUpActionList.Remove(CloseQuestPanel);
        }

        private void OpenQuestPanel_Extension(string message, string questName, string rewardItemName, int itemCode)
        {
            questPanelExtension.SetActive(true);
            questPanelExtensionText.text = message;

            rewardItemNameText.text = rewardItemName;
            LobbyUIManager.AddClosePopUpAction(CloseQuestPanel_Extension);
          //  questPanelExtensionCloseBtn.onClick.AddListener(() => ConfiromReword(itemCode));
        }

        private void CloseQuestPanel_Extension()
        {
            questPanelExtension.SetActive(false);
        }

        #endregion

        #region 퀘스트 데이터 관리

        private void ConfiromReword(int itemCode)
        {
            InventoryDataManagerDontdestory.Instance.GetItemByItemCode(itemCode);
        }

        private void addQuest_index()
        {
            for (int i = 0; i < 10; i++)
            {
                Quest_Index questIndex = Instantiate(questPrefab, questContainer.transform);
                questIndex.SetQuestIndex("퀘스트" + i);
                questIndex.QuestButton.onClick.AddListener(() =>
                {
                    OpenQuestPanel_Extension("퀘스트 상세 내용", questIndex.name, "보상 아이템", 1001);
                });
            }
        }

        #endregion
    }
}