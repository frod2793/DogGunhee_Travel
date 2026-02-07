using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using InGame.Services;

namespace InGame.Lobby.ViewModels
{
    public class QuestData
    {
        public int QuestId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string RewardName { get; set; }
        public int RewardItemCode { get; set; }
        public bool IsCompleted { get; set; }
    }

    /// <summary>
    /// 퀘스트 시스템의 비즈니스 로직을 관리하는 ViewModel
    /// </summary>
    public class QuestViewModel : IDisposable
    {
        #region 상태 프로퍼티

        // 퀘스트 리스트
        public ReadOnlyReactiveProperty<List<QuestData>> Quests => m_quests;
        private readonly ReactiveProperty<List<QuestData>> m_quests = new ReactiveProperty<List<QuestData>>(new List<QuestData>());

        // 현재 선택된 퀘스트
        public ReadOnlyReactiveProperty<QuestData> CurrentSelectedQuest => m_currentSelectedQuest;
        private readonly ReactiveProperty<QuestData> m_currentSelectedQuest = new ReactiveProperty<QuestData>();

        #endregion

        #region 이벤트

        // 에러 발생 이벤트
        public Observable<string> OnError => m_errorSubject;
        private readonly Subject<string> m_errorSubject = new Subject<string>();

        // 보상 수령 완료 이벤트
        public Observable<string> OnRewardClaimed => m_rewardClaimedSubject;
        private readonly Subject<string> m_rewardClaimedSubject = new Subject<string>();

        #endregion

        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        public QuestViewModel()
        {
            // 초기화
        }

        public void LoadQuests()
        {
            // TODO: 실제 데이터 로드 (서버/로컬)
            // 현재는 QuestPanelManager의 더미 로직을 그대로 옮김
            var list = new List<QuestData>();
            for (int i = 0; i < 10; i++)
            {
                list.Add(new QuestData
                {
                    QuestId = i,
                    Title = $"퀘스트 {i}",
                    Description = "퀘스트 상세 내용",
                    RewardName = "보상 아이템",
                    RewardItemCode = 1001,
                    IsCompleted = false
                });
            }
            m_quests.Value = list;
        }

        public void SelectQuest(QuestData quest)
        {
            m_currentSelectedQuest.Value = quest;
        }

        public void ClaimReward()
        {
            var quest = m_currentSelectedQuest.Value;
            if (quest == null) return;
            
            if (quest.IsCompleted)
            {
                m_errorSubject.OnNext("이미 완료된 퀘스트입니다.");
                return;
            }

            // 보상 지급 로직
            if (InventoryDataManager.Instance != null)
            {
                InventoryDataManager.Instance.GetItemByItemCode(quest.RewardItemCode);
                
                // 완료 처리
                quest.IsCompleted = true; // 실제로는 서버 통신 후 처리
                
                m_rewardClaimedSubject.OnNext($"{quest.RewardName}을(를) 수령했습니다.");
                
                // 리스트 갱신 알림 (새로운 리스트 인스턴스로 교체해야 ReactiveProperty가 반응함)
                m_quests.Value = new List<QuestData>(m_quests.Value); 
            }
            else
            {
                m_errorSubject.OnNext("인벤토리 매니저를 찾을 수 없습니다.");
            }
        }

        public void Dispose()
        {
            m_disposables.Dispose();
            m_quests.Dispose();
            m_currentSelectedQuest.Dispose();
            m_errorSubject.Dispose();
            m_rewardClaimedSubject.Dispose();
        }
    }
}
