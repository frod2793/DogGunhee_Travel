using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using UnityEngine;
using InGame.Services;

namespace InGame.Lobby.ViewModels
{
    /// <summary>
    /// 아이템 선택 프로세스에 필요한 기초 데이터 구조입니다.
    /// </summary>
    public class ItemSelectData
    {
        public int ItemCode { get; set; }
        public string ItemName { get; set; }
        public string ItemDescription { get; set; }
        public bool IsUnlocked { get; set; }
    }

    /// <summary>
    /// 인벤토리 및 장착 시스템의 비즈니스 로직을 처리하는 ViewModel 클래스입니다.
    /// <br/>R3를 활용하여 아이템 목록과 현재 선택 상태를 리액티브하게 관리합니다.
    /// </summary>
    public class ItemSelectViewModel : IDisposable
    {
        #region 1. 반응형 프로퍼티 (View가 구독)

        /// <summary> 표시할 아이템 데이터 목록 </summary>
        public ReadOnlyReactiveProperty<List<ItemSelectData>> Items => m_items;

        private readonly ReactiveProperty<List<ItemSelectData>> m_items =
            new ReactiveProperty<List<ItemSelectData>>(new List<ItemSelectData>());

        /// <summary> 현재 유저가 클릭한 아이템 데이터 </summary>
        public ReadOnlyReactiveProperty<ItemSelectData> CurrentSelectedItem => m_currentSelectedItem;

        private readonly ReactiveProperty<ItemSelectData>
            m_currentSelectedItem = new ReactiveProperty<ItemSelectData>();

        #endregion

        #region 2. 이벤트 발행 (View가 리슨)

        /// <summary> 시스템 에러 발생 시 알림 </summary>
        public Observable<string> OnError => m_errorSubject;

        private readonly Subject<string> m_errorSubject = new Subject<string>();

        /// <summary> 아이템 장착 처리가 완료되었을 때 알림 </summary>
        public Observable<string> OnItemEquipped => m_itemEquippedSubject;

        private readonly Subject<string> m_itemEquippedSubject = new Subject<string>();

        #endregion

        #region 3. 내부 필드 및 생성자

        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        /// <summary>
        /// ItemSelectViewModel의 기본 생성자입니다.
        /// </summary>
        public ItemSelectViewModel()
        {
            // 초기화 시 필요한 로직 기술 (DI 주입 등)
        }

        #endregion

        #region 4. 비즈니스 로직

        /// <summary>
        /// 보유 중인 아이템 목록을 서비스 레이어(데이터 매니저)로부터 불러와 갱신합니다.
        /// </summary>
        public void LoadItems()
        {
            // [TODO] 실제 로직: InventoryDataManager.Instance를 통해 필터링된 데이터 로드

            // 현재는 시연을 위한 가상 데이터 생성
            var dummyList = new List<ItemSelectData>();
            for (int i = 0; i < 5; i++)
            {
                dummyList.Add(new ItemSelectData
                {
                    ItemCode = 2000 + i,
                    ItemName = $"신비한 소모품 {i + 1}",
                    ItemDescription = $"능력치 강화 효능 +{(i + 1) * 5}%",
                    IsUnlocked = true
                });
            }

            m_items.Value = dummyList;
        }

        /// <summary>
        /// 특정 아이템을 현재 선택 중인 대상으로 지정합니다.
        /// </summary>
        public void SelectItem(ItemSelectData item)
        {
            m_currentSelectedItem.Value = item;
        }

        /// <summary>
        /// 현재 강조된 아이템을 실제로 플레이어 캐릭터에게 장착하거나 사용을 확정합니다.
        /// </summary>
        public void EquipItem()
        {
            var item = m_currentSelectedItem.Value;
            if (item == null)
            {
                m_errorSubject.OnNext("선택된 아이템이 없습니다.");
                return;
            }

            // [TODO] 실제 장착 로직 수행 (예: 서버 연동 또는 로컬 데이터 저장)
            // PlayerDataManager.Instance.EquipItem(item.ItemCode);

            m_itemEquippedSubject.OnNext($"[{item.ItemName}] 장착이 완료되었습니다.");
        }

        #endregion

        #region 5. 리소스 해제 (IDisposable)

        /// <summary>
        /// 뷰모델 파생 시 모든 구독 정보를 정리하여 메모리 누수를 방지합니다.
        /// </summary>
        public void Dispose()
        {
            m_disposables.Dispose();

            m_items.Dispose();
            m_currentSelectedItem.Dispose();

            m_errorSubject.Dispose();
            m_itemEquippedSubject.Dispose();
        }

        #endregion
    }
}
