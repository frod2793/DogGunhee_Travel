using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using R3;
using InGame.Lobby; // Item_Index
using InGame.Lobby.ViewModels;
using InGame.UI; // PopupManager

namespace InGame.UI.Popups
{
    /// <summary>
    /// 아이템 선택 UI를 관리하는 View 클래스
    /// ItemSelectViewModel과 연동됩니다.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "InGame.Lobby", "ItemSelectManager")]
    public class ItemSelectView : MonoBehaviour
    {
        #region UI 컴포넌트

        [Header("<color=green>아이템 선택 UI</color>")]
        [SerializeField] private GameObject m_itemSelectPanel;
        [SerializeField] private Transform m_itemSelectContainer;
        [SerializeField] private Item_Index m_itemSelectPrefab;
        
        [Header("<color=green>아이템 상세/확장 UI</color>")]
        [SerializeField] private GameObject m_itemSelectExtension;
        [SerializeField] private TMP_Text m_itemNameText;
        [SerializeField] private Image m_itemImage;
        [SerializeField] private TMP_Text m_itemDescriptionText;
        [SerializeField] private Button m_itemSelectButton; // 장착/선택 확정 버튼

        #endregion

        #region ViewModel & 상태

        private ItemSelectViewModel m_viewModel;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        private readonly List<Item_Index> m_spawnedItems = new List<Item_Index>();

        #endregion

        #region Unity 라이프사이클

        private void Start()
        {
            m_viewModel = new ItemSelectViewModel();
            BindViewModel();
            m_viewModel.LoadItems();

            if (m_itemSelectButton != null)
            {
                m_itemSelectButton.onClick.AddListener(() => m_viewModel.EquipItem());
            }
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
            // 아이템 리스트 갱신
            m_viewModel.Items
                .Subscribe(UpdateItemList)
                .AddTo(m_disposables);

            // 선택된 아이템 변경 시 상세창 업데이트
            m_viewModel.CurrentSelectedItem
                .Subscribe(UpdateDetailPanel)
                .AddTo(m_disposables);

            // 장착 완료 메시지
            m_viewModel.OnItemEquipped
                .Subscribe(msg => 
                {
                    LogManager.Log(msg, LogManager.LogCategory.ItemManager);
                    CloseItemExtensionPanel();
                    CloseItemSelectPanel();
                })
                .AddTo(m_disposables);

            m_viewModel.OnError
                .Subscribe(msg => LogManager.LogError(msg, LogManager.LogCategory.ItemManager))
                .AddTo(m_disposables);
        }

        #endregion

        #region UI 업데이트

        private void UpdateItemList(List<ItemSelectData> items)
        {
            foreach (var item in m_spawnedItems)
            {
                if (item != null) Destroy(item.gameObject);
            }
            m_spawnedItems.Clear();

            if (items == null) return;

            foreach (var itemData in items)
            {
                if (m_itemSelectPrefab == null)
                {
                    Debug.LogError("ItemSelectView: m_itemSelectPrefab이 할당되지 않았습니다. 인스펙터에서 Item_Index 프리팹을 연결해주세요.");
                    return;
                }

                var instance = Instantiate(m_itemSelectPrefab, m_itemSelectContainer);
                
                // 데이터 표시
                if (instance.characterName != null) instance.characterName.text = itemData.ItemName;
                // if (instance.thumbNail != null) ...

                // 클릭 이벤트
                if (instance.openCharacterSelectButton != null)
                {
                    instance.openCharacterSelectButton.onClick.RemoveAllListeners();
                    instance.openCharacterSelectButton.onClick.AddListener(() => 
                    {
                        m_viewModel.SelectItem(itemData);
                        OpenItemExtensionPanel();
                    });
                }
                
                m_spawnedItems.Add(instance);
            }
        }

        private void UpdateDetailPanel(ItemSelectData itemData)
        {
            if (itemData == null) return;

            if (m_itemNameText != null) m_itemNameText.text = itemData.ItemName;
            if (m_itemDescriptionText != null) m_itemDescriptionText.text = itemData.ItemDescription;
            // if (m_itemImage != null) ...
        }

        #endregion

        #region 패널 제어 (Public)

        public void OpenItemSelectPanel()
        {
            if (m_itemSelectPanel != null)
            {
                m_itemSelectPanel.SetActive(true);
                PopupManager.Instance.RegisterPopup(CloseItemSelectPanel);
                
                if (m_itemSelectExtension != null) 
                    m_itemSelectExtension.SetActive(false);
            }
        }

        public void CloseItemSelectPanel()
        {
            if (m_itemSelectPanel != null)
            {
                m_itemSelectPanel.SetActive(false);
            }
        }

        private void OpenItemExtensionPanel()
        {
            if (m_itemSelectExtension != null)
            {
                m_itemSelectExtension.SetActive(true);
                PopupManager.Instance.RegisterPopup(CloseItemExtensionPanel);
            }
        }

        private void CloseItemExtensionPanel()
        {
            if (m_itemSelectExtension != null)
            {
                m_itemSelectExtension.SetActive(false);
            }
        }

        #endregion
    }
}