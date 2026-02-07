using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using InGame;
using InGame.Lobby;

namespace InGame.UI.Elements
{
    /// <summary>
    /// 상점 아이템의 개별 UI를 담당하는 View 클래스
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Lobby", "Store_Item")]
    public class StoreItemView : MonoBehaviour
    {
        #region UI 컴포넌트

        [Header("아이템 정보")]
        [Tooltip("이 아이템의 고유 코드입니다.")]
        [SerializeField] private int m_itemCode;
        
        [Tooltip("상점에 표시될 아이템의 설명입니다.")]
        [SerializeField] private string m_itemDescription;

        [Header("아이템 UI")]
        [SerializeField] private TMP_Text m_itemNameText;
        [SerializeField] private TMP_Text m_itemCoinCountText;
        [SerializeField] private TMP_Text m_itemDescriptionText;
        [SerializeField] private Image m_itemImage;
        [SerializeField] private Button m_itemButton;

        #endregion

        // 구매 요청 이벤트 (ItemCode)
        public event Action<int> OnPurchaseRequest;

        private ItemDataSO m_itemData;

        private void Start()
        {
            Initialize(m_itemCode);
        }

        public void Initialize(int code)
        {
            m_itemCode = code;
            
            // 데이터 로드 (표시용)
            // View에서 직접 DataManager에 접근하는 것은 허용 (단순 조회)
            // 엄격한 MVVM에서는 ViewModel이 데이터를 꽂아주어야 하지만,
            // 현재 구조(Addressable로 Prefab 로드)상 여기서 조회하는 것이 현실적임.
            if (InventoryDataManager.Instance != null)
            {
                m_itemData = InventoryDataManager.Instance.GetItemByItemCode(m_itemCode);
            }

            if (m_itemData == null)
            {
                // 데이터 없음 처리
                gameObject.SetActive(false);
                return;
            }

            UpdateUI();
            
            // 버튼 리스너 연결
            if (m_itemButton != null)
            {
                m_itemButton.onClick.RemoveAllListeners();
                m_itemButton.onClick.AddListener(() => OnPurchaseRequest?.Invoke(m_itemCode));
            }
        }

        private void UpdateUI()
        {
            if (m_itemData == null) return;

            if (m_itemNameText != null) m_itemNameText.text = m_itemData.itemName;
            if (m_itemDescriptionText != null) m_itemDescriptionText.text = m_itemDescription;
            if (m_itemCoinCountText != null) m_itemCoinCountText.text = m_itemData.itemcoinCount.ToString();
            
            // 이미지 로드 로직 (필요 시 복구)
            // if (m_itemImage != null) ...
        }
    }
}