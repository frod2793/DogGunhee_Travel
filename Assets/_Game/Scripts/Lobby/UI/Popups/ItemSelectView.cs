using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using R3;
using InGame.Lobby;
using InGame.Lobby.ViewModels;
using InGame.UI;

namespace InGame.UI.Popups
{
    /// <summary>
    /// [설명]: 아이템 선택 팝업의 시각적 요소를 관리하고 사용자 입력을 수신하는 View 클래스입니다.
    /// ItemSelectViewModel과 연동하여 보유 아이템 목록을 표시하고 장착 기능을 수행합니다.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "InGame.Lobby", "ItemSelectManager")]
    public class ItemSelectView : MonoBehaviour
    {
        #region 에디터 설정

        [Header("<color=green>아이템 목록 설정</color>")]
        [SerializeField, Tooltip("아이템 선택 패널 부모 오브젝트")]
        private GameObject m_itemSelectPanel;

        [SerializeField, Tooltip("아이템 인덱스가 생성될 컨테이너")]
        private Transform m_itemSelectContainer;

        [SerializeField, Tooltip("개별 아이템 표시용 프리팹")]
        private Item_Index m_itemSelectPrefab;

        [Header("<color=green>아이템 상세 정보창</color>")]
        [SerializeField, Tooltip("아이템 상세 확장 패널")]
        private GameObject m_itemSelectExtension;

        [SerializeField, Tooltip("아이템 이름 텍스트")]
        private TMP_Text m_itemNameText;

        [SerializeField, Tooltip("아이템 아이콘 이미지")]
        private Image m_itemImage;

        [SerializeField, Tooltip("아이템 상세 설명 텍스트")]
        private TMP_Text m_itemDescriptionText;

        [SerializeField, Tooltip("장착 확정 버튼")]
        private Button m_itemSelectButton;

        #endregion

        #region 내부 변수

        private ItemSelectViewModel m_viewModel;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        private readonly List<Item_Index> m_spawnedItems = new List<Item_Index>();

        #endregion

        #region 유니티 생명주기

        private void Start()
        {
            InitializeViewModel();
            BindViewModel();

            // 초기 데이터 로드 시작
            m_viewModel?.LoadItems();

            if (m_itemSelectButton != null)
            {
                m_itemSelectButton.onClick.AddListener(() => m_viewModel?.EquipItem());
            }
        }

        private void OnDestroy()
        {
            m_disposables.Dispose();
            m_viewModel?.Dispose();
        }

        #endregion

        #region MVVM 데이터 바인딩

        /// <summary>
        /// [설명]: 뷰모델을 생성하고 초기화합니다.
        /// </summary>
        private void InitializeViewModel()
        {
            m_viewModel = new ItemSelectViewModel();
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
                    CloseItemExtensionPanel();
                    CloseItemSelectPanel();
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
        private void UpdateItemList(List<ItemSelectData> items)
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
                if (m_itemSelectPrefab == null)
                {
                    LogManager.LogError("[ItemSelectView] 아이템 프리팹이 누락되었습니다.", LogManager.LogCategory.System);
                    return;
                }

                var instance = Instantiate(m_itemSelectPrefab, m_itemSelectContainer);

                // 데이터 바인딩
                if (instance.characterName != null)
                {
                    instance.characterName.text = itemData.ItemName;
                }

                // 클릭 시 해당 아이템 선택 처리 및 상세창 오픈
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

        /// <summary>
        /// [설명]: 우측 상세 패널의 정보를 현재 선택된 아이템 정보로 갱신합니다.
        /// </summary>
        private void UpdateDetailPanel(ItemSelectData itemData)
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

            // TODO: 아이템 코드에 따른 썸네일 로드 로직
        }

        #endregion

        #region 패널 제어 로직

        /// <summary>
        /// [설명]: 메인 아이템 선택 패널을 활성화하고 팝업 스택에 등록합니다.
        /// </summary>
        public void OpenItemSelectPanel()
        {
            if (m_itemSelectPanel != null)
            {
                m_itemSelectPanel.SetActive(true);
                PopupManager.Instance.RegisterPopup(CloseItemSelectPanel);

                // 상위 패널이 열릴 때 하위 상세창은 닫음
                if (m_itemSelectExtension != null)
                {
                    m_itemSelectExtension.SetActive(false);
                }
            }
        }

        /// <summary>
        /// [설명]: 메인 아이템 선택 패널을 비활성화합니다.
        /// </summary>
        public void CloseItemSelectPanel()
        {
            if (m_itemSelectPanel != null)
            {
                m_itemSelectPanel.SetActive(false);
            }
        }

        /// <summary>
        /// [설명]: 아이템 상세 정보창을 열고 팝업 스택에 등록합니다.
        /// </summary>
        private void OpenItemExtensionPanel()
        {
            if (m_itemSelectExtension != null)
            {
                m_itemSelectExtension.SetActive(true);
                PopupManager.Instance.RegisterPopup(CloseItemExtensionPanel);
            }
        }

        /// <summary>
        /// [설명]: 아이템 상세 정보창을 닫습니다.
        /// </summary>
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