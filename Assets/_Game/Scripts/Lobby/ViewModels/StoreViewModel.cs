using System;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using InGame.Services;

namespace InGame.Lobby.ViewModels
{
    /// <summary>
    /// [설명]: 상점 시스템의 재화 상태와 아이템 구매 로직을 관리하는 ViewModel 클래스입니다.
    /// 유저의 현재 재화(골드/다이아)를 모니터링하고 구매 적합성을 판단합니다.
    /// </summary>
    public class StoreViewModel : IDisposable
    {
        #region 반응형 프로퍼티

        /// <summary> [설명]: 현재 플레이어의 보유 골드 수량 </summary>
        public ReadOnlyReactiveProperty<int> Gold => m_gold;

        private readonly ReactiveProperty<int> m_gold = new ReactiveProperty<int>();

        /// <summary> [설명]: 현재 플레이어의 보유 다이아 수량 </summary>
        public ReadOnlyReactiveProperty<int> Diamond => m_diamond;

        private readonly ReactiveProperty<int> m_diamond = new ReactiveProperty<int>();

        #endregion

        #region 이벤트 발행

        /// <summary> [설명]: 구매 실패 또는 시스템 에러 발생 시 알림 </summary>
        public Observable<string> OnError => m_errorSubject;

        private readonly Subject<string> m_errorSubject = new Subject<string>();

        /// <summary> [설명]: 성공적으로 아이템을 구매했을 때의 피드백 알림 </summary>
        public Observable<string> OnPurchaseSuccess => m_purchaseSuccessSubject;

        private readonly Subject<string> m_purchaseSuccessSubject = new Subject<string>();

        #endregion

        #region 내부 필드 및 생성자

        private readonly InGame.Data.PlayerDataDTO m_playerData;
        private readonly InGame.Services.PlayerDataService m_playerService;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        /// <summary>
        /// [설명]: StoreViewModel 생성 시 DTO와 서비스를 주입받습니다.
        /// </summary>
        public StoreViewModel(InGame.Data.PlayerDataDTO playerData, InGame.Services.PlayerDataService playerService)
        {
            m_playerData = playerData;
            m_playerService = playerService;
            RefreshCurrency();
        }

        #endregion

        #region 비즈니스 로직

        /// <summary>
        /// [설명]: 데이터 서비스로부터 최신 재화 정보를 읽어와 반응형 프로퍼티를 갱신합니다.
        /// </summary>
        public void RefreshCurrency()
        {
            if (m_playerData != null)
            {
                m_gold.Value = m_playerData.Currency1;
                m_diamond.Value = m_playerData.Currency2;
            }
        }

        /// <summary>
        /// [설명]: 특정 아이템 코드를 기반으로 상점 거래를 처리합니다.
        /// </summary>
        /// <param name="itemCode">구입할 아이템의 고유 코드</param>
        public void PurchaseItem(int itemCode)
        {
            if (InventoryManager.Instance == null)
            {
                LogManager.LogError("[StoreViewModel] 인벤토리 매니저가 누락되었습니다.", LogManager.LogCategory.StoreManager);
                m_errorSubject.OnNext("상점 연동 오류가 발생했습니다.");
                return;
            }

            // [변경] InventoryDataManager -> InventoryManager 사용
            var itemData = InventoryManager.Instance.GetItemInfo(itemCode);
            if (itemData == null)
            {
                m_errorSubject.OnNext("이 아이템은 현재 판매 정보가 존재하지 않습니다.");
                return;
            }

            // 1. 재화 검사 루틴
            bool isCurrencyEnough = false;
            // [참고] 대소문자 주의: ItemDataSO의 itemcoinType 값 (Gold/Diamond)
            if (itemData.itemcoinType == "Gold")
            {
                isCurrencyEnough = m_playerData.Currency1 >= itemData.itemcoinCount;
            }
            else if (itemData.itemcoinType == "Diamond")
            {
                isCurrencyEnough = m_playerData.Currency2 >= itemData.itemcoinCount;
            }

            if (!isCurrencyEnough)
            {
                m_errorSubject.OnNext($"{itemData.itemcoinType} 재화가 부족하여 구입할 수 없습니다.");
                return;
            }

            // 2. 실제 차감 및 지급
            ExecuteTransactionInternal(itemData);
        }

        #endregion

        #region 내부 처리 로직

        /// <summary>
        /// [설명]: 재화 차감, 아이템 지급, 데이터 저장을 포함한 실제 트랜잭션을 실행합니다.
        /// </summary>
        private void ExecuteTransactionInternal(ItemDataSO itemData)
        {
            // 재화 차감
            if (itemData.itemcoinType == "Gold")
            {
                m_playerService.SubtractCurrency("currency1", itemData.itemcoinCount);
            }
            else if (itemData.itemcoinType == "Diamond")
            {
                m_playerService.SubtractCurrency("currency2", itemData.itemcoinCount);
            }

            // 인벤토리에 아이템 추가
            // [변경] 명시적으로 AddItem 호출
            InventoryManager.Instance.System.AddItem(itemData);

            // 데이터 저장
            m_playerService.SaveData();
            // [변경] 새 매니저 저장 메서드 호출
            InventoryManager.Instance.SaveInventory();

            // 상태 갱신 및 시각적 피드백
            RefreshCurrency();
            m_purchaseSuccessSubject.OnNext($"[{itemData.itemName}] 구매에 성공했습니다!");

            LogManager.Log($"[StoreViewModel] 구매 성공: {itemData.itemName} (Code: {itemData.itemCode})",
                LogManager.LogCategory.StoreManager);
        }

        #endregion

        #region 리소스 해제

        /// <summary>
        /// [설명]: 뷰모델 파기 시 모든 리액티브 구독을 정리합니다.
        /// </summary>
        public void Dispose()
        {
            m_disposables.Dispose();

            m_gold.Dispose();
            m_diamond.Dispose();

            m_errorSubject.Dispose();
            m_purchaseSuccessSubject.Dispose();
        }

        #endregion
    }
}
