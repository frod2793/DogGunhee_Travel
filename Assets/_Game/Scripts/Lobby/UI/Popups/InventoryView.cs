using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;
using Cysharp.Threading.Tasks;
using R3;
using InGame.Lobby;
using InGame.Lobby.ViewModels;
using InGame.UI;

namespace InGame.UI.Popups
{
    /// <summary>
    /// [설명]: 인벤토리 팝업의 시각적 요소를 관리하고 사용자 입력을 수신하는 View 클래스입니다.
    /// InventoryViewModel과 연동하여 보유 아이템 목록을 표시하고 장착 기능을 수행합니다.
    /// </summary>
    public class InventoryView : MonoBehaviour
    {
        #region 에디터 설정

        [Header("<color=green>인벤토리 목록 설정</color>")]
        [SerializeField, Tooltip("인벤토리 패널 부모 오브젝트"), FormerlySerializedAs("m_itemSelectPanel")]
        private GameObject m_inventoryPanel;

        [SerializeField, Tooltip("아이템 인덱스가 생성될 컨테이너"), FormerlySerializedAs("m_itemSelectContainer")]
        private Transform m_inventoryContainer;

        [SerializeField, Tooltip("개별 아이템 표시용 프리팹"), FormerlySerializedAs("m_itemSelectPrefab")]
        private Item_Index m_inventoryItemPrefab;

        [Header("<color=green>아이템 상세 정보창</color>")]
        [SerializeField, Tooltip("아이템 상세 확장 패널"), FormerlySerializedAs("m_itemSelectExtension")]
        private GameObject m_inventoryExtension;

        [SerializeField, Tooltip("아이템 이름 텍스트")]
        private TMP_Text m_itemNameText;

        [SerializeField, Tooltip("아이템 아이콘 이미지")]
        private Image m_itemImage;

        [SerializeField, Tooltip("아이템 상세 설명 텍스트")]
        private TMP_Text m_itemDescriptionText;

        [SerializeField, Tooltip("장착 확정 버튼"), FormerlySerializedAs("m_itemSelectButton")]
        private Button m_equipButton;

        [SerializeField, Tooltip("아이템 판매 버튼")]
        private Button m_sellButton;

        [SerializeField, Tooltip("아이템 가격 텍스트")]
        private TMP_Text m_itemPriceText;

        #endregion

        #region 내부 변수

        private InventoryViewModel m_viewModel;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        private readonly List<Item_Index> m_spawnedItems = new List<Item_Index>();

        #endregion

        #region 초기화 및 바인딩 로직

        /// <summary>
        /// [설명]: 외부(LobbyUIViewManager 등)로부터 의존성을 주입받아 초기화합니다.
        /// </summary>
        public void Initialize(InGame.Data.PlayerDataDTO playerData, InGame.Services.PlayerDataService playerService)
        {
            if (m_viewModel != null) return;

            m_viewModel = new InventoryViewModel(playerData, playerService);
            BindViewModel();
            
            // 초기 데이터 로드 시작
            m_viewModel.LoadItems();
        }

        #endregion

        #region 유니티 생명주기

        private void Start()
        {
            // LobbyUIViewManager에서 Initialize를 호출하지 않았을 경우를 대비한 방어 코드
            if (m_viewModel == null)
            {
                // 실 서비스에서는 이런 상황이 발생하면 안 되나, 에디터 테스트 편의를 위해 유지할 수 있음
                // 여기서는 규칙에 따라 명시적 주입을 강제하기 위해 주석 처리하거나 경고를 띄움
                Debug.LogWarning("[InventoryView] 명시적 Initialize가 호출되지 않았습니다.");
            }

            if (m_equipButton != null)
            {
                m_equipButton.onClick.AddListener(() => m_viewModel?.EquipItem());
            }

            if (m_sellButton != null)
            {
                m_sellButton.onClick.AddListener(() =>
                {
                    // 기본적으로 1개 판매 (추후 수량 선택 팝업 연동 가능)
                    m_viewModel?.SellSelectedItem(1);
                });
            }
        }

        /// <summary>
        /// [설명]: 뷰모델의 속성 변화를 구독하여 UI 요소를 업데이트합니다.
        /// </summary>
        private void BindViewModel()
        {
            if (m_viewModel == null)
            {
                return;
            }

            // 1. 아이템 목록 동기화
            m_viewModel.Items
                .Subscribe(UpdateItemList)
                .AddTo(m_disposables);

            // 2. 현재 선택된 아이템 정보 동기화
            m_viewModel.CurrentSelectedItem
                .Subscribe(UpdateDetailPanel)
                .AddTo(m_disposables);

            // 3. 성공/에러 피드백
            m_viewModel.OnItemEquipped
                .Subscribe(msg =>
                {
                    LogManager.Log(msg, LogManager.LogCategory.ItemManager);
                    // 직접 Close를 호출하지 않고 스택에서 Pop하여 닫기 수행 (상세창 -> 메인창 순서)
                    PopupManager.Instance.CloseTopPopup(); // 상세창 닫기
                    PopupManager.Instance.CloseTopPopup(); // 메인창 닫기
                })
                .AddTo(m_disposables);

            m_viewModel.OnItemSold
                .Subscribe(msg =>
                {
                    // 판매 성공 시 로그 출력 (추후 토스트 메시지 연동 recommended)
                    LogManager.Log(msg, LogManager.LogCategory.ItemManager);
                })
                .AddTo(m_disposables);

            m_viewModel.OnError
                .Subscribe(msg => LogManager.LogError(msg, LogManager.LogCategory.ItemManager))
                .AddTo(m_disposables);
        }

        #endregion

        #region UI 업데이트 로직

        /// <summary>
        /// [설명]: 아이템 데이터 목록을 기반으로 UI 리스트를 동적으로 구성합니다.
        /// </summary>
        private void UpdateItemList(List<InventoryItemData> items)
        {
            // 기존 오브젝트 정리
            foreach (var item in m_spawnedItems)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }

            m_spawnedItems.Clear();

            if (items == null)
            {
                return;
            }

            foreach (var itemData in items)
            {
                if (m_inventoryItemPrefab == null)
                {
                    LogManager.LogError("[InventoryView] 아이템 프리팹이 누락되었습니다.", LogManager.LogCategory.System);
                    return;
                }

                var instance = Instantiate(m_inventoryItemPrefab, m_inventoryContainer);

                // 데이터 바인딩
                if (instance.characterName != null)
                {
                    // 이름과 수량을 같이 표시
                    instance.characterName.text = $"{itemData.ItemName} ({itemData.Count})";
                }

                // 클릭 시 해당 아이템 선택 처리 및 상세창 오픈
                if (instance.openCharacterSelectButton != null)
                {
                    instance.openCharacterSelectButton.onClick.RemoveAllListeners();
                    instance.openCharacterSelectButton.onClick.AddListener(() =>
                    {
                        m_viewModel.SelectItem(itemData);
                        OpenInventoryExtensionPanel();
                    });
                }

                m_spawnedItems.Add(instance);
            }
        }

        /// <summary>
        /// [설명]: 우측 상세 패널의 정보를 현재 선택된 아이템 정보로 갱신합니다.
        /// </summary>
        private void UpdateDetailPanel(InventoryItemData itemData)
        {
            if (itemData == null)
            {
                return;
            }

            if (m_itemNameText != null)
            {
                m_itemNameText.text = itemData.ItemName;
            }

            if (m_itemDescriptionText != null)
            {
                m_itemDescriptionText.text = itemData.ItemDescription;
            }

            if (m_itemPriceText != null)
            {
                string currencyName = itemData.CurrencyType == "currency1" ? "G" : "D";
                m_itemPriceText.text = $"{itemData.Price} {currencyName}";
            }

            // TODO: 아이템 코드에 따른 썸네일 로드 로직
        }

        #endregion

        #region 패널 제어 로직

        /// <summary>
        /// [설명]: 메인 인벤토리 패널을 활성화하고 팝업 스택에 등록합니다.
        /// </summary>
        public void OpenInventoryPanel()
        {
            if (m_inventoryPanel != null)
            {
                UnityEngine.Debug.Log("[InventoryView] m_inventoryPanel.SetActive(true)");
                m_inventoryPanel.SetActive(true);
                PopupManager.Instance.RegisterPopup(CloseInventoryPanel);

                // 상위 패널이 열릴 때 하위 상세창은 닫음
                if (m_inventoryExtension != null)
                {
                    m_inventoryExtension.SetActive(false);
                }
            }
            else
            {
                UnityEngine.Debug.LogError("[InventoryView] m_inventoryPanel is NULL!");
            }
        }

        /// <summary>
        /// [설명]: 메인 인벤토리 패널을 비활성화합니다.
        /// </summary>
        public void CloseInventoryPanel()
        {
            if (m_inventoryPanel != null)
            {
                m_inventoryPanel.SetActive(false);
            }
        }

        /// <summary>
        /// [설명]: 아이템 상세 정보창을 열고 팝업 스택에 등록합니다.
        /// </summary>
        private void OpenInventoryExtensionPanel()
        {
            if (m_inventoryExtension != null)
            {
                m_inventoryExtension.SetActive(true);
                PopupManager.Instance.RegisterPopup(CloseInventoryExtensionPanel);
            }
        }

        /// <summary>
        /// [설명]: 아이템 상세 정보창을 닫습니다.
        /// </summary>
        private void CloseInventoryExtensionPanel()
        {
            if (m_inventoryExtension != null)
            {
                m_inventoryExtension.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            m_disposables.Dispose();
            m_viewModel?.Dispose();
        }

        #endregion
    }
}
