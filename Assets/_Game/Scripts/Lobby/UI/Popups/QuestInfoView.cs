using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using R3;
using InGame.Lobby.ViewModels;
using InGame.Lobby;
using InGame.UI;

namespace InGame.UI.Popups
{
    /// <summary>
    /// 로비의 퀘스트 시스템을 시각화하고 진행 상황을 보여주는 View 클래스입니다.
    /// <br/>QuestViewModel과 연동하여 서버로부터 퀘스트 목록을 불러와 표시합니다.
    /// </summary>
    public class QuestInfoView : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("<color=green>퀘스트 목록 설정</color>")] [SerializeField, Tooltip("퀘스트 패널 메인 오브젝트")]
        private GameObject m_questPanel;

        [SerializeField, Tooltip("퀘스트 아이템들이 배치될 컨테이너")]
        private GameObject m_questContainer;

        [SerializeField, Tooltip("개별 퀘스트 항목 프리팹")]
        private Quest_Index m_questPrefab;

        [Header("<color=green>상세 정보 확장 패널</color>")] [SerializeField, Tooltip("퀘스트 상세 설명 패널")]
        private GameObject m_questPanelExtension;

        [SerializeField, Tooltip("퀘스트 상세 내용 텍스트")]
        private TMP_Text m_questPanelExtensionText;

        [SerializeField, Tooltip("퀘스트 보상 아이템 이름 텍스트")]
        private TMP_Text m_rewardItemNameText;

        #endregion

        #region 2. 내부 변수 및 상태

        private QuestViewModel m_viewModel;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        // 관리 중인 퀘스트 항목 리스트
        private readonly List<Quest_Index> m_questItems = new List<Quest_Index>();

        #endregion

        #region 3. 유니티 생명주기

        private void Start()
        {
            InitializeViewModel();
            BindViewModel();

            // 진입 시 퀘스트 로드 시도
            m_viewModel?.LoadQuests();
        }

        private void OnDestroy()
        {
            m_disposables.Dispose();
            m_viewModel?.Dispose();
        }

        #endregion

        #region 4. MVVM 데이터 바인딩

        /// <summary>
        /// 퀘스트 비즈니스 로직을 처리할 뷰모델을 생성합니다.
        /// </summary>
        private void InitializeViewModel()
        {
            m_viewModel = new QuestViewModel();
        }

        /// <summary>
        /// 뷰모델의 반응형 데이터를 구독하여 UI를 동기화합니다.
        /// </summary>
        private void BindViewModel()
        {
            if (m_viewModel == null) return;

            // 1. 서버 퀘스트 리스트 데이터 갱신 알림 구독
            m_viewModel.Quests
                .Subscribe(UpdateQuestList)
                .AddTo(m_disposables);

            // 2. 에러 로그 출력
            m_viewModel.OnError
                .Subscribe(msg => LogManager.LogError($"[QuestInfoView] {msg}", LogManager.LogCategory.QuestManager))
                .AddTo(m_disposables);

            // 3. 보상 성공 수령 알림
            m_viewModel.OnRewardClaimed
                .Subscribe(msg => { LogManager.Log($"[QuestInfoView] {msg}", LogManager.LogCategory.QuestManager); })
                .AddTo(m_disposables);
        }

        #endregion

        #region 5. 리스트 화면 구성 로직

        /// <summary>
        /// 퀘스트 데이터 리스트를 기반으로 UI 인스턴스들을 생성하거나 갱신합니다.
        /// </summary>
        private void UpdateQuestList(List<QuestData> quests)
        {
            // 효율을 위해 기존 리스트 제거 (필요 시 풀링 도입 가능)
            foreach (var item in m_questItems)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }

            m_questItems.Clear();

            if (quests == null) return;

            foreach (var questData in quests)
            {
                if (m_questPrefab == null || m_questContainer == null)
                {
                    LogManager.LogError("[QuestInfoView] 퀘스트 프리팹 또는 컨테이너가 누락되었습니다.", LogManager.LogCategory.System);
                    return;
                }

                var questItem = Instantiate(m_questPrefab, m_questContainer.transform);
                if (questItem != null)
                {
                    questItem.SetQuestIndex(questData.Title);

                    // 퀘스트 상세 보기 버튼 이벤트 연결
                    if (questItem.QuestButton != null)
                    {
                        questItem.QuestButton.onClick.RemoveAllListeners();
                        questItem.QuestButton.onClick.AddListener(() =>
                        {
                            m_viewModel.SelectQuest(questData);
                            OpenQuestPanelExtension(questData.Description, questData.Title, questData.RewardName);
                        });
                    }
                }

                m_questItems.Add(questItem);
            }
        }

        #endregion

        #region 6. 패널 제어 로직 (Open/Close)

        /// <summary>
        /// 메인 퀘스트 목록 패널을 활성화하고 팝업 스택에 관리 동작을 등록합니다.
        /// </summary>
        public void OpenQuestPanel()
        {
            if (m_questPanel == null) return;

            m_questPanel.SetActive(true);
            PopupManager.Instance.RegisterPopup(CloseQuestPanel);
        }

        /// <summary>
        /// 메인 퀘스트 패널을 비활성화합니다.
        /// </summary>
        private void CloseQuestPanel()
        {
            if (m_questPanel != null)
            {
                m_questPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 특정 퀘스트의 상세 정보를 보여주는 패널을 활성화합니다.
        /// </summary>
        private void OpenQuestPanelExtension(string message, string questName, string rewardItemName)
        {
            if (m_questPanelExtension == null) return;

            m_questPanelExtension.SetActive(true);

            if (m_questPanelExtensionText != null) m_questPanelExtensionText.SetText(message);
            if (m_rewardItemNameText != null) m_rewardItemNameText.SetText(rewardItemName);

            PopupManager.Instance.RegisterPopup(CloseQuestPanelExtension);
        }

        /// <summary>
        /// 상세 정보 패널을 닫습니다.
        /// </summary>
        private void CloseQuestPanelExtension()
        {
            if (m_questPanelExtension != null)
            {
                m_questPanelExtension.SetActive(false);
            }
        }

        /// <summary>
        /// 외부(버튼 등)에서 퀘스트 보상 수령을 시도할 때 호출됩니다.
        /// </summary>
        public void OnClickRewardButton()
        {
            m_viewModel?.ClaimReward();
        }

        #endregion
    }
}