using System;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using InGame.Services;

namespace InGame.Lobby.ViewModels
{
    public class StoreViewModel : IDisposable
    {
        #region 상태

        // 에러 메시지
        public Observable<string> OnError => m_errorSubject;
        private readonly Subject<string> m_errorSubject = new Subject<string>();

        // 구매 완료 메시지
        public Observable<string> OnPurchaseSuccess => m_purchaseSuccessSubject;
        private readonly Subject<string> m_purchaseSuccessSubject = new Subject<string>();

        // 재화 정보 (PlayerDataManager에서 로드)
        public ReadOnlyReactiveProperty<int> Gold => m_gold;
        private readonly ReactiveProperty<int> m_gold = new ReactiveProperty<int>();

        public ReadOnlyReactiveProperty<int> Diamond => m_diamond;
        private readonly ReactiveProperty<int> m_diamond = new ReactiveProperty<int>();

        #endregion

        #region 의존성

        private PlayerDataManager m_playerDataManager;

        #endregion

        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        public StoreViewModel()
        {
            m_playerDataManager = PlayerDataManager.Instance;
            RefreshCurrency();
        }

        public void RefreshCurrency()
        {
            if (m_playerDataManager != null && m_playerDataManager.PlayerData != null)
            {
                m_gold.Value = m_playerDataManager.PlayerData.currency1;
                m_diamond.Value = m_playerDataManager.PlayerData.currency2;
            }
        }

        public void PurchaseItem(int itemCode)
        {
            ProcessPurchase(itemCode);
        }

        /// <summary>
        /// [리팩토링] 현재 로직에서는 비동기 작업이 없으므로 동기 메서드로 변경했습니다.
        /// 실제 서버 통신 추가 시 async로 복원하세요.
        /// </summary>
        private void ProcessPurchase(int itemCode)
        {
            if (InventoryDataManager.Instance == null)
            {
                m_errorSubject.OnNext("InventoryDataManager가 없습니다.");
                return;
            }

            var itemData = InventoryDataManager.Instance.GetItemByItemCode(itemCode);
            if (itemData == null)
            {
                m_errorSubject.OnNext($"아이템 정보를 찾을 수 없습니다. (Code: {itemCode})");
                return;
            }

            // 재화 확인 및 차감
            bool isEnough = false;
            long currentCurrency = 0;

            if (itemData.itemcoinType == "Gold")
            {
                isEnough = m_playerDataManager.PlayerData.currency1 >= itemData.itemcoinCount;
                currentCurrency = m_playerDataManager.PlayerData.currency1;
            }
            else if (itemData.itemcoinType == "Diamond")
            {
                isEnough = m_playerDataManager.PlayerData.currency2 >= itemData.itemcoinCount;
                currentCurrency = m_playerDataManager.PlayerData.currency2;
            }

            if (!isEnough)
            {
                m_errorSubject.OnNext($"{itemData.itemcoinType}가 부족합니다.");
                return;
            }

            // 구매 처리 (서버 통신 시뮬레이션)
            // TODO: ServerManager.Instance.PurchaseItemAsync(...) 등 실제 통신 필요
            // 여기서는 로컬 데이터만 수정

            if (itemData.itemcoinType == "Gold")
            {
                m_playerDataManager.PlayerData.currency1 -= itemData.itemcoinCount;
            }
            else if (itemData.itemcoinType == "Diamond")
            {
                m_playerDataManager.PlayerData.currency2 -= itemData.itemcoinCount;
            }

            // 인벤토리 추가
            InventoryDataManager.Instance.GetItemByItemCode(itemCode); // 획득 처리

            // 데이터 저장
            m_playerDataManager.SavePlayerData();
            InventoryDataManager.Instance.SaveInventoryData();

            // UI 갱신
            RefreshCurrency();

            m_purchaseSuccessSubject.OnNext($"{itemData.itemName} 구매 완료!");
        }

        public void Dispose()
        {
            m_disposables.Dispose();
            m_errorSubject.Dispose();
            m_purchaseSuccessSubject.Dispose();
            m_gold.Dispose();
            m_diamond.Dispose();
        }
    }
}
