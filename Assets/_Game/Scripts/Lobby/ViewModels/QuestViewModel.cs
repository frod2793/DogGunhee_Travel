using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using InGame.Services;

namespace InGame.Lobby.ViewModels
{
    /// <summary>
    /// [설명]: 개별 퀘스트 요소의 상태를 나타내는 데이터 구조입니다.
    /// </summary>
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
    /// [설명]: 로비의 퀘스트 시스템의 데이터 처리와 보상 획득 로직을 담당하는 ViewModel 클래스입니다.
    /// 서버 또는 로컬 데이터로부터 퀘스트 목록을 구성하고 상태 변화를 View에 전달합니다.
    /// </summary>
    public class QuestViewModel : IDisposable
    {
        #region 반응형 프로퍼티

        /// <summary> [설명]: 전체 퀘스트 목록 데이터 </summary>
        public ReadOnlyReactiveProperty<List<QuestData>> Quests => m_quests;

        private readonly ReactiveProperty<List<QuestData>> m_quests =
            new ReactiveProperty<List<QuestData>>(new List<QuestData>());

        /// <summary> [설명]: 현재 상세 내용을 확인 중인 퀘스트 </summary>
        public ReadOnlyReactiveProperty<QuestData> CurrentSelectedQuest => m_currentSelectedQuest;

        private readonly ReactiveProperty<QuestData> m_currentSelectedQuest = new ReactiveProperty<QuestData>();

        #endregion

        #region 이벤트 발행

        /// <summary> [설명]: 퀘스트 로드 또는 보상 수령 실패 시 안내 </summary>
        public Observable<string> OnError => m_errorSubject;

        private readonly Subject<string> m_errorSubject = new Subject<string>();

        /// <summary> [설명]: 성공적으로 퀘스트 보상을 수령했을 때의 알림 </summary>
        public Observable<string> OnRewardClaimed => m_rewardClaimedSubject;

        private readonly Subject<string> m_rewardClaimedSubject = new Subject<string>();

        #endregion

        #region 내부 변수 및 생성자

        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        /// <summary>
        /// [설명]: QuestViewModel의 기본 생성자입니다.
        /// </summary>
        public QuestViewModel()
        {
            // 초기화 로직 (필요 시 작성)
        }

        #endregion

        #region 비즈니스 로직

        /// <summary>
        /// [설명]: 서버 또는 데이터 파일로부터 퀘스트 목록을 로드하여 상태를 갱신합니다.
        /// </summary>
        public void LoadQuests()
        {
            // [TODO] 실제 로직: 어드레스블 또는 서버 API 호출
            // 현재 구조에서는 시연 및 정합성을 위해 더미 목록 구성
            var list = new List<QuestData>();
            for (int i = 0; i < 10; i++)
            {
                list.Add(new QuestData
                {
                    QuestId = i,
                    Title = $"일일 모험 퀘스트 {i + 1}",
                    Description = $"이 퀘스트는 캐릭터의 모험 성장을 돕는 상세 가이드 0{i + 1}입니다.",
                    RewardName = "성장 비약",
                    RewardItemCode = 1001,
                    IsCompleted = false
                });
            }

            m_quests.Value = list;
        }

        /// <summary>
        /// [설명]: 특정 퀘스트를 선택 처리하여 상세 정보와 연결합니다.
        /// </summary>
        public void SelectQuest(QuestData quest)
        {
            m_currentSelectedQuest.Value = quest;
        }

        /// <summary>
        /// [설명]: 현재 선택된 퀘스트가 완료 가능한 상태라면 보상을 지급하고 상태를 반영합니다.
        /// </summary>
        public void ClaimReward()
        {
            var quest = m_currentSelectedQuest.Value;
            if (quest == null)
            {
                return;
            }

            if (quest.IsCompleted)
            {
                m_errorSubject.OnNext("이미 보상 수령이 완료된 퀘스트입니다.");
                return;
            }

            // 1. 보상 아이템 지급 (인벤토리 매니저 연동)
            // 1. 보상 아이템 지급 (인벤토리 매니저 연동)
            if (InventoryManager.Instance != null)
            {
                // [변경] InventoryDataManager -> InventoryManager
                // 기존 GetItemByItemCode는 아이템을 추가하는 부작용이 있었음.
                // 변경된 로직에서는 명시적으로 아이템 정보를 조회하고 추가함.
                var rewardItem = InventoryManager.Instance.GetItemInfo(quest.RewardItemCode);
                if (rewardItem != null)
                {
                    InventoryManager.Instance.System.AddItem(rewardItem);
                    InventoryManager.Instance.SaveInventory();
                }

                // 2. 내부 데이터 상태 완료 처리 (서버 연동 시 API 응답에 맞춰 처리)
                quest.IsCompleted = true;

                // 3. UI 갱신 알림 (새 인스턴스 할당을 통한 반응 유도)
                m_quests.Value = new List<QuestData>(m_quests.Value);

                m_rewardClaimedSubject.OnNext($"[{quest.RewardName}] 보상을 수령하였습니다.");
            }
            else
            {
                LogManager.LogError("[QuestViewModel] 인벤토리 매니저 참조를 찾을 수 없습니다.", LogManager.LogCategory.QuestManager);
                m_errorSubject.OnNext("시스템 연동 오류로 보상을 수령할 수 없습니다.");
            }
        }

        #endregion

        #region 리소스 해제

        /// <summary>
        /// [설명]: 뷰모델 파생 시 모든 반응형 데이터와 구독을 해제합니다.
        /// </summary>
        public void Dispose()
        {
            m_disposables.Dispose();

            m_quests.Dispose();
            m_currentSelectedQuest.Dispose();

            m_errorSubject.Dispose();
            m_rewardClaimedSubject.Dispose();
        }

        #endregion
    }
}
