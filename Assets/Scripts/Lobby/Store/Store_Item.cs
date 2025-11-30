using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;


namespace Vamser_like.Lobby
{
    /// <summary>
    /// 상점 아이템 UI 및 구매 로직을 담당합니다.
    /// </summary>
    public class Store_Item : MonoBehaviour
    {
        [Header("아이템 정보")]
        [Tooltip("이 아이템의 고유 코드입니다.")]
        [FormerlySerializedAs("itemCode")]
        [SerializeField] private int m_itemCode;
        [Tooltip("상점에 표시될 아이템의 설명입니다.")]
        [FormerlySerializedAs("itemDescription")]
        [SerializeField] private string m_itemDescription;

        [Header("아이템 UI")]
        [Tooltip("아이템의 이름을 표시하는 텍스트입니다.")]
        [FormerlySerializedAs("itemName_text")]
        [SerializeField] private TMP_Text m_itemNameText;
        [Tooltip("아이템의 가격을 표시하는 텍스트입니다.")]
        [FormerlySerializedAs("itemCoinCount_text")]
        [SerializeField] private TMP_Text m_itemCoinCountText;
        [Tooltip("아이템의 설명을 표시하는 텍스트입니다.")]
        [FormerlySerializedAs("itemDescription_text")]
        [SerializeField] private TMP_Text m_itemDescriptionText;
        [Tooltip("아이템의 이미지를 표시합니다.")]
        [FormerlySerializedAs("itemImage")]
        [SerializeField] private Image m_itemImage;
        [Tooltip("아이템 구매 버튼입니다.")]
        [FormerlySerializedAs("itemButton")]
        [SerializeField] private Button m_itemButton;

        private Item_Data m_itemData;

        private void Start()
        {
            // 인스펙터에서 설정된 코드로 아이템을 초기화합니다.
            Initialize(m_itemCode);
        }

        /// <summary>
        /// 아이템 구매 버튼 클릭 시 호출되는 비동기 메서드입니다.
        /// </summary>
        private async void OnBuyItemButtonPressed()
        {
            if (m_itemData == null)
            {
                LogManager.LogError("구매할 아이템 데이터가 유효하지 않습니다.", LogManager.LogCategory.InventoryManager);
                return;
            }

            var playerDataManager = PlayerDataManagerDontdesytoy.Instance;

            // 재화 확인
            if (!HasEnoughCurrency(playerDataManager))
            {
                LogManager.LogWarning("재화가 부족하여 아이템을 구매할 수 없습니다.", LogManager.LogCategory.InventoryManager);
                // TODO: 사용자에게 재화 부족 알림 UI 표시
                return;
            }

            m_itemButton.interactable = false; // 중복 클릭 방지
            try
            {
                // 로컬 데이터 변경 및 서버 동기화
                await ProcessPurchaseAsync(playerDataManager);
                LogManager.Log("데이터 동기화 완료.", LogManager.LogCategory.InventoryManager);
                // TODO: 구매 성공 UI 피드백 표시
            }
            catch (Exception e)
            {
                LogManager.LogError($"아이템 구매 중 오류 발생: {e.Message}", LogManager.LogCategory.InventoryManager);
                // TODO: 구매 실패 UI 피드백 및 롤백 로직 고려
            }
            finally
            {
                m_itemButton.interactable = true; // 버튼 상호작용 복원
            }
        }

        /// <summary>
        /// 지정된 아이템 코드로 상점 아이템을 초기화하고 UI를 설정합니다.
        /// </summary>
        /// <param name="code">초기화할 아이템의 고유 코드</param>
        public void Initialize(int code)
        {
            m_itemCode = code;
            m_itemData = InventoryDataManagerDontdestory.Instance.GetItemByItemCode(m_itemCode);

            if (m_itemData == null)
            {
                LogManager.LogError($"아이템 코드 '{m_itemCode}'에 해당하는 데이터를 찾을 수 없습니다.", LogManager.LogCategory.InventoryManager);
                gameObject.SetActive(false);
                return;
            }

            // UI 업데이트
            m_itemNameText.text = m_itemData.itemName;
            m_itemDescriptionText.text = m_itemDescription;
            m_itemCoinCountText.text = m_itemData.itemcoinCount.ToString();
            // TODO: m_itemImage.sprite = Resources.Load<Sprite>($"ItemSprites/{m_itemData.itemCode}");

            // 버튼 리스너 설정
            m_itemButton.onClick.RemoveAllListeners();
            m_itemButton.onClick.AddListener(OnBuyItemButtonPressed);
        }

        private async UniTask ProcessPurchaseAsync(PlayerDataManagerDontdesytoy playerDataManager)
        {
            var inventoryManager = InventoryDataManagerDontdestory.Instance;

            // 1. 재화 차감
            switch (m_itemData.itemcoinType)
            {
                case "currency1":
                    playerDataManager.PlayerData.currency1 -= m_itemData.itemcoinCount;
                    break;
                case "currency2":
                    playerDataManager.PlayerData.currency2 -= m_itemData.itemcoinCount;
                    break;
            }

            // 2. 인벤토리에 아이템 추가
            inventoryManager.InventoryData.AddItem(m_itemData);

            LogManager.Log($"'{m_itemData.itemName}' 아이템 구매 성공. 서버에 데이터를 동기화합니다.", LogManager.LogCategory.InventoryManager);

                // 서버에 데이터 동기화 (병렬 처리로 성능 향상)
                await UniTask.WhenAll(
                    playerDataManager.UploadDataToServerAsync(),
                    inventoryManager.UploadDataToServerAsync()
                );
        }

        private bool HasEnoughCurrency(PlayerDataManagerDontdesytoy playerDataManager)
        {
            switch (m_itemData.itemcoinType)
            {
                case "currency1":
                    return playerDataManager.PlayerData.currency1 >= m_itemData.itemcoinCount;
                case "currency2":
                    return playerDataManager.PlayerData.currency2 >= m_itemData.itemcoinCount;
                default:
                    return false;
            }
        }
    }
}