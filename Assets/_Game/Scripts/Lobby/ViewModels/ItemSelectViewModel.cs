using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using UnityEngine;
using InGame.Services;

namespace InGame.Lobby.ViewModels
{
    // 아이템 선택에 필요한 데이터 모델 (간소화)
    public class ItemSelectData
    {
        public int ItemCode { get; set; }
        public string ItemName { get; set; }
        public string ItemDescription { get; set; }
        public bool IsUnlocked { get; set; }
        // 이미지 경로 등 추가 가능
    }

    /// <summary>
    /// 아이템 선택 로직을 관리하는 ViewModel
    /// </summary>
    public class ItemSelectViewModel : IDisposable
    {
        #region 상태 프로퍼티

        // 아이템 리스트
        public ReadOnlyReactiveProperty<List<ItemSelectData>> Items => m_items;
        private readonly ReactiveProperty<List<ItemSelectData>> m_items = new ReactiveProperty<List<ItemSelectData>>(new List<ItemSelectData>());

        // 현재 선택된 아이템
        public ReadOnlyReactiveProperty<ItemSelectData> CurrentSelectedItem => m_currentSelectedItem;
        private readonly ReactiveProperty<ItemSelectData> m_currentSelectedItem = new ReactiveProperty<ItemSelectData>();

        #endregion

        #region 이벤트

        public Observable<string> OnError => m_errorSubject;
        private readonly Subject<string> m_errorSubject = new Subject<string>();

        public Observable<string> OnItemEquipped => m_itemEquippedSubject;
        private readonly Subject<string> m_itemEquippedSubject = new Subject<string>();

        #endregion

        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        public ItemSelectViewModel()
        {
            // 초기화
        }

        public void LoadItems()
        {
            // TODO: InventoryDataManager 등을 통해 실제 보유 아이템 로드
            // 기존 ItemSelectManager의 하드코딩된 데이터를 참고하여 더미 데이터 생성 또는 실제 데이터 로드
            // 여기서는 예시로 생성
            var list = new List<ItemSelectData>();
            for (int i = 0; i < 5; i++)
            {
                list.Add(new ItemSelectData
                {
                    ItemCode = 2000 + i,
                    ItemName = $"아이템 {i + 1}",
                    ItemDescription = $"능력치 증가 +{i * 10}",
                    IsUnlocked = true
                });
            }
            m_items.Value = list;
        }

        public void SelectItem(ItemSelectData item)
        {
            m_currentSelectedItem.Value = item;
        }

        public void EquipItem()
        {
            var item = m_currentSelectedItem.Value;
            if (item == null) return;

            // TODO: 장착 로직 (서버 통신/로컬 저장)
            // PlayerDataManager.Instance.EquipItem(item.ItemCode);
            
            m_itemEquippedSubject.OnNext($"{item.ItemName}을(를) 장착했습니다.");
        }

        public void Dispose()
        {
            m_disposables.Dispose();
            m_items.Dispose();
            m_currentSelectedItem.Dispose();
            m_errorSubject.Dispose();
            m_itemEquippedSubject.Dispose();
        }
    }
}
