using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using UnityEngine;
using InGame.Services;

namespace InGame.Lobby.ViewModels
{
    /// <summary>
    /// [설명]: 인벤토리 아이템 기초 데이터 구조입니다.
    /// </summary>
    public class InventoryItemData
    {
        public int ItemCode { get; set; }
        public string ItemName { get; set; }
        public string ItemDescription { get; set; }
        public bool IsUnlocked { get; set; }
        public int Count { get; set; } // 수량 추가
        public Sprite Icon { get; set; } // 아이콘 추가
        public int Price { get; set; } // 판매 가격
        public string CurrencyType { get; set; } // 재화 타입
    }

    /// <summary>
    /// [설명]: 인벤토리 및 장착 시스템의 비즈니스 로직을 처리하는 ViewModel 클래스입니다.
    /// R3를 활용하여 아이템 목록과 현재 선택 상태를 리액티브하게 관리합니다.
    /// </summary>
    public class InventoryViewModel : IDisposable
    {
        #region 반응형 프로퍼티

        /// <summary> [설명]: 표시할 인벤토리 아이템 데이터 목록 </summary>
        public ReadOnlyReactiveProperty<List<InventoryItemData>> Items => m_items;

        private readonly ReactiveProperty<List<InventoryItemData>> m_items =
            new ReactiveProperty<List<InventoryItemData>>(new List<InventoryItemData>());

        /// <summary> [설명]: 현재 유저가 클릭한 아이템 데이터 </summary>
        public ReadOnlyReactiveProperty<InventoryItemData> CurrentSelectedItem => m_currentSelectedItem;

        private readonly ReactiveProperty<InventoryItemData>
            m_currentSelectedItem = new ReactiveProperty<InventoryItemData>();

        #endregion

        #region 이벤트 발행

        /// <summary> [설명]: 시스템 에러 발생 시 알림 </summary>
        public Observable<string> OnError => m_errorSubject;

        private readonly Subject<string> m_errorSubject = new Subject<string>();

        /// <summary> [설명]: 아이템 장착 처리가 완료되었을 때 알림 </summary>
        public Observable<string> OnItemEquipped => m_itemEquippedSubject;

        private readonly Subject<string> m_itemEquippedSubject = new Subject<string>();
        
        /// <summary> [설명]: 아이템 판매 처리가 완료되었을 때 알림 </summary>
        public Observable<string> OnItemSold => m_itemSoldSubject;

        private readonly Subject<string> m_itemSoldSubject = new Subject<string>();

        #endregion

        #region 내부 필드

        private readonly InGame.Data.PlayerDataDTO m_playerData;
        private readonly InGame.Services.PlayerDataService m_playerService;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        /// <summary>
        /// [설명]: InventoryViewModel의 생성자입니다. DTO와 서비스를 주입받습니다.
        /// </summary>
        public InventoryViewModel(InGame.Data.PlayerDataDTO playerData, InGame.Services.PlayerDataService playerService)
        {
            m_playerData = playerData;
            m_playerService = playerService;
        }

        #endregion

        #region 비즈니스 로직

        /// <summary>
        /// [설명]: 보유 중인 아이템 목록을 서비스 레이어(InventoryManager)로부터 불러와 갱신합니다.
        /// </summary>
        public void LoadItems()
        {
            if (InventoryManager.Instance == null || InventoryManager.Instance.System == null)
            {
                // [TODO] 에러 처리 or Retry
                return;
            }

            var entries = InventoryManager.Instance.System.GetAllEntries();
            var viewList = new List<InventoryItemData>();

            foreach (var entry in entries)
            {
                if (entry == null || entry.item == null || entry.count <= 0) continue;

                viewList.Add(new InventoryItemData
                {
                    ItemCode = entry.item.itemCode,
                    ItemName = entry.item.itemName,
                    ItemDescription = entry.item.itemDescription,
                    IsUnlocked = true, // 보유 중이므로 true
                    Count = entry.count,
                    Icon = entry.item.itemIcon,
                    Price = entry.item.itemcoinCount,
                    CurrencyType = entry.item.itemcoinType
                });
            }

            m_items.Value = viewList;
        }

        /// <summary>
        /// [설명]: 특정 아이템을 현재 선택 중인 대상으로 지정합니다.
        /// </summary>
        public void SelectItem(InventoryItemData item)
        {
            m_currentSelectedItem.Value = item;
        }

        /// <summary>
        /// [설명]: 현재 강조된 아이템을 장착합니다.
        /// </summary>
        public void EquipItem()
        {
            var item = m_currentSelectedItem.Value;
            if (item == null)
            {
                m_errorSubject.OnNext("선택된 아이템이 없습니다.");
                return;
            }

            // [TODO] 실제 장착 로직 (PlayerController 연결 필요)
            // 현재는 UI 표시용 이벤트만 발생
            m_itemEquippedSubject.OnNext($"[{item.ItemName}] 장착이 완료되었습니다.");
        }

        /// <summary>
        /// [설명]: 현재 선택된 아이템을 판매(환전)합니다.
        /// </summary>
        /// <param name="count">판매할 수량 (기본 1개)</param>
        public void SellSelectedItem(int count = 1)
        {
            var item = m_currentSelectedItem.Value;
            if (item == null)
            {
                m_errorSubject.OnNext("선택된 아이템이 없습니다.");
                return;
            }

            if (InventoryManager.Instance == null) return;

            // 1. 판매 로직 수행
            var result = InventoryManager.Instance.System.SellItem(item.ItemCode, count);

            if (result.success)
            {
                // 2. 재화 지급
                if (m_playerService != null)
                {
                    m_playerService.AddCurrency(result.currencyType, result.totalAmount);    
                }
                
                // 3. 알림 및 UI 갱신
                string currencyName = result.currencyType == "currency1" ? "골드" : "다이아";
                m_itemSoldSubject.OnNext($"[{item.ItemName}] {count}개 판매 완료! (+{result.totalAmount} {currencyName})");
                
                // 목록 갱신 (수량 변화 반영)
                LoadItems();
                
                // 선택 정보 갱신 (선택 해제 또는 수량 업데이트)
                // 만약 아이템이 모두 사라졌다면 선택 해제
                var updatedItem = m_items.Value.FirstOrDefault(x => x.ItemCode == item.ItemCode);
                if (updatedItem != null)
                {
                    m_currentSelectedItem.Value = updatedItem;
                }
                else
                {
                    m_currentSelectedItem.Value = null;
                }
            }
            else
            {
                m_errorSubject.OnNext("아이템 판매에 실패했습니다. (수량 부족 등)");
            }
        }

        #endregion

        #region 리소스 해제

        /// <summary>
        /// [설명]: 뷰모델 파생 시 모든 구독 정보를 정리하여 메모리 누수를 방지합니다.
        /// </summary>
        public void Dispose()
        {
            m_disposables.Dispose();

            m_items.Dispose();
            m_currentSelectedItem.Dispose();

            m_errorSubject.Dispose();
            m_itemEquippedSubject.Dispose();
            m_itemSoldSubject.Dispose();
        }

        #endregion
    }
}
