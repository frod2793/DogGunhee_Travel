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
    /// 상점에서 개별 아이템의 정보를 표시하고 구매 요청을 담당하는 항목형 View 클래스입니다.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Lobby", "Store_Item")]
    public class StoreItemView : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("<color=green>아이템 데이터 정보</color>")] [SerializeField, Tooltip("아이템 식별 코드")]
        private int m_itemCode;

        [SerializeField, Tooltip("UI에 표시될 아이템 상세 설명")]
        private string m_itemDescription;

        [Header("<color=green>내부 UI 참조</color>")] [SerializeField, Tooltip("아이템 이름 텍스트")]
        private TMP_Text m_itemNameText;

        [SerializeField, Tooltip("판매 가격 텍스트")] private TMP_Text m_itemCoinCountText;

        [SerializeField, Tooltip("아이템 설명 텍스트")]
        private TMP_Text m_itemDescriptionText;

        [SerializeField, Tooltip("아이템 이미지")] private Image m_itemImage;

        [SerializeField, Tooltip("구매 버튼")] private Button m_itemButton;

        #endregion

        #region 2. 이벤트 및 상태

        /// <summary>
        /// 아이템 구매를 요청할 때 발생하는 이벤트입니다. (파라미터: ItemCode)
        /// </summary>
        public event Action<int> OnPurchaseRequest;

        private ItemDataSO m_itemData;

        #endregion

        #region 3. 유니티 생명주기

        private void Start()
        {
            // 인스펙터에 설정된 기본 코드로 로드 시도
            Initialize(m_itemCode);
        }

        #endregion

        #region 4. 로직 및 UI 제어

        /// <summary>
        /// 특정 아이템 코드를 전달받아 데이터 매니저로부터 실데이터를 불러오고 UI를 동기화합니다.
        /// </summary>
        /// <param name="code">초기화할 아이템 코드</param>
        public void Initialize(int code)
        {
            m_itemCode = code;

            // 데이터 관리자로부터 설정값 로드
            if (InventoryDataManager.Instance != null)
            {
                m_itemData = InventoryDataManager.Instance.GetItemByItemCode(m_itemCode);
            }

            if (m_itemData == null)
            {
                LogManager.LogWarning("아이템 정보가 올바르지 않습니다.", LogManager.LogCategory.System);
                gameObject.SetActive(false);
                return;
            }

            UpdateUI();

            // 상호작용 버튼 이벤트 연결
            if (m_itemButton != null)
            {
                m_itemButton.onClick.RemoveAllListeners();
                m_itemButton.onClick.AddListener(() => OnPurchaseRequest?.Invoke(m_itemCode));
            }
        }

        /// <summary>
        /// 로드된 ItemDataSO 데이터를 기반으로 UI 요소들을 갱신합니다.
        /// </summary>
        private void UpdateUI()
        {
            if (m_itemData == null)
            {
                return;
            }

            if (m_itemNameText != null)
            {
                m_itemNameText.text = m_itemData.itemName;
            }

            if (m_itemDescriptionText != null)
            {
                m_itemDescriptionText.text = m_itemDescription;
            }

            if (m_itemCoinCountText != null)
            {
                m_itemCoinCountText.text = m_itemData.itemcoinCount.ToString();
            }

            // TODO: 필요한 경우 아이템 아이콘(thumbnail) 연동 로직 추가 가능
        }

        #endregion
    }
}