using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using R3;
using InGame.Lobby.ViewModels;
using InGame.Lobby;
using InGame.UI; // PopupManager

namespace InGame.UI.Popups
{
    /// <summary>
    /// 퀘스트 UI를 관리하는 View 클래스
    /// QuestViewModel과 바인딩되어 데이터를 표시합니다.
    /// </summary>
    public class QuestInfoView : MonoBehaviour
    {
        #region UI 컴포넌트

        [Header("<color=green> 퀘스트 패널")] 
        [SerializeField] private GameObject m_questPanel;
        [SerializeField] private GameObject m_questContainer;
        [SerializeField] private Quest_Index m_questPrefab;
      
        [Header("<color=green> 확장 패널")] 
        [SerializeField] private GameObject m_questPanelExtension;
        [SerializeField] private TMP_Text m_questPanelExtensionText; // 상세 내용
        [SerializeField] private TMP_Text m_rewardItemNameText;

        #endregion

        #region ViewModel & 상태

        private QuestViewModel m_viewModel;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        // 생성된 퀘스트 아이템 목록
        private readonly List<Quest_Index> m_questItems = new List<Quest_Index>();

        #endregion

        #region Unity 라이프사이클

        private void Start()
        {
            m_viewModel = new QuestViewModel();
            BindViewModel();
            m_viewModel.LoadQuests();
        }

        private void OnDestroy()
        {
            m_disposables.Dispose();
            m_viewModel?.Dispose();
        }

        #endregion

        #region MVVM 바인딩

        private void BindViewModel()
        {
            // 퀘스트 리스트 구독
            m_viewModel.Quests
                .Subscribe(UpdateQuestList)
                .AddTo(m_disposables);

            // 선택된 퀘스트 변경 시 상세창 업데이트 (여기서는 버튼 클릭 시 수동 업데이트하므로 생략 가능하나 반응형으로 구현)
            // m_viewModel.CurrentSelectedQuest.Subscribe(...);

            m_viewModel.OnError.Subscribe(msg => LogManager.LogError(msg, LogManager.LogCategory.QuestManager)).AddTo(m_disposables);
            
            m_viewModel.OnRewardClaimed.Subscribe(msg => 
            {
                LogManager.Log(msg, LogManager.LogCategory.QuestManager);
                // 보상 수령 후 상세 패널 닫기?? 기획에 따라 다름.
            }).AddTo(m_disposables);
        }

        #endregion

        #region UI 업데이트

        private void UpdateQuestList(List<QuestData> quests)
        {
            // 기존 아이템 제거 (풀링 미사용 시)
            foreach (var item in m_questItems)
            {
                if (item != null) Destroy(item.gameObject);
            }
            m_questItems.Clear();

            if (quests == null) return;

            foreach (var quest in quests)
            {
                if (m_questPrefab == null)
                {
                    Debug.LogError("QuestInfoView: m_questPrefab이 할당되지 않았습니다. 인스펙터에서 Quest_Index 프리팹을 연결해주세요.");
                    return;
                }

                if (m_questContainer == null)
                {
                    Debug.LogError("QuestInfoView: m_questContainer가 할당되지 않았습니다. 인스펙터에서 퀘스트 리스트가 생성될 부모 Transform을 연결해주세요.");
                    return;
                }

                var questItem = Instantiate(m_questPrefab, m_questContainer.transform);
                if (questItem != null)
                {
                    questItem.SetQuestIndex(quest.Title);
                    
                    // 버튼 클릭 시 상세 정보창 열기
                    var btn = questItem.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.AddListener(() => 
                        {
                            m_viewModel.SelectQuest(quest);
                            OpenQuestPanel_Extension(quest.Description, quest.Title, quest.RewardName);
                        });
                    }
                }
                m_questItems.Add(questItem);
            }
        }

        #endregion

        #region 패널 제어 (Public)

        public void OpenQuestPanel()
        {
            if (m_questPanel == null) return;
            m_questPanel.SetActive(true);
            PopupManager.Instance.RegisterPopup(CloseQuestPanel);
        }

        private void CloseQuestPanel()
        {
            if (m_questPanel == null) return;
            m_questPanel.SetActive(false);
        }

        private void OpenQuestPanel_Extension(string message, string questName, string rewardItemName)
        {
            if (m_questPanelExtension == null) return;

            m_questPanelExtension.SetActive(true);
            if (m_questPanelExtensionText != null) m_questPanelExtensionText.text = message;
            if (m_rewardItemNameText != null) m_rewardItemNameText.text = rewardItemName;

            PopupManager.Instance.RegisterPopup(CloseQuestPanel_Extension);
            
            // 확장 패널에도 닫기/보상받기 버튼 등이 있을 것.
            // 하지만 QuestPanelManager 원본 코드에는 확장 패널 내 버튼 이벤트 연결 로직이 모호했음. (주석처리됨)
            // 여기서는 ViewModel의 ClaimReward를 호출하는 버튼이 별도로 연결되어 있다고 가정하거나,
            // LobbyUIViewManager에서 연결해주는 방식을 따름.
        }

        private void CloseQuestPanel_Extension()
        {
            if (m_questPanelExtension == null) return;
            m_questPanelExtension.SetActive(false);
        }

        // 외부(LobbyUIViewManager 등)에서 보상 받기 버튼 클릭 시 호출
        public void OnClickRewardButton()
        {
            m_viewModel.ClaimReward();
        }

        #endregion
    }
}