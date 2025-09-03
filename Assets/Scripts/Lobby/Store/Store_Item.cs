using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace DogGuns_Games.Lobby
{
    /// <summary>
    /// 상점 아이템 UI 및 구매 로직을 담당합니다.
    /// </summary>
    public class Store_Item : MonoBehaviour
    {
        [Header("아이템 정보")]
        [SerializeField] private int itemCode; // 이 아이템의 고유 코드
        [SerializeField] private string itemDescription; // 상점 전용 아이템 설명

        [Header("아이템 UI")]
        [SerializeField] private TMP_Text itemName_text;
        [SerializeField] private TMP_Text itemCoinCount_text;
        [SerializeField] private TMP_Text itemDescription_text;
        [SerializeField] private Image itemImage;
        [SerializeField] private Button itemButton;

        private Item_Data _itemData;

        private void Start()
        {
            // 인스펙터에서 설정된 코드로 아이템을 초기화합니다.
            Initialize(itemCode);
        }

        /// <summary>
        /// 지정된 아이템 코드로 상점 아이템을 초기화하고 UI를 설정합니다.
        /// </summary>
        /// <param name="code">초기화할 아이템의 고유 코드</param>
        public void Initialize(int code)
        {
            itemCode = code;
            _itemData = InventoryDataManagerDontdestory.Instance.GetItemByItemCode(itemCode);

            if (_itemData == null)
            {
                LogManager.LogError($"아이템 코드 '{itemCode}'에 해당하는 데이터를 찾을 수 없습니다.", LogManager.LogCategory.InventoryManager);
                gameObject.SetActive(false);
                return;
            }

            // UI 업데이트
            itemName_text.text = _itemData.itemName;
            itemDescription_text.text = itemDescription;
            itemCoinCount_text.text = _itemData.itemcoinCount.ToString();
            // TODO: itemImage.sprite = Resources.Load<Sprite>($"ItemSprites/{_itemData.itemCode}");

            // 버튼 리스너 설정
            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener(OnBuyItemButtonPressed);
        }

        /// <summary>
        /// 아이템 구매 버튼 클릭 시 호출되는 비동기 메서드입니다.
        /// </summary>
        private async void OnBuyItemButtonPressed()
        {
            if (_itemData == null)
            {
                LogManager.LogError("구매할 아이템 데이터가 유효하지 않습니다.", LogManager.LogCategory.InventoryManager);
                return;
            }

            var playerDataManager = PlayerDataManagerDontdesytoy.Instance;
            var inventoryManager = InventoryDataManagerDontdestory.Instance;
            
            // 재화 확인
            int playerCurrency = 0;
            bool hasEnoughCurrency = false;

            if (_itemData.itemcoinType == "currency1")
            {
                playerCurrency = playerDataManager.scritpableobjPlayerData.currency1;
                if (playerCurrency >= _itemData.itemcoinCount) hasEnoughCurrency = true;
            }
            else if (_itemData.itemcoinType == "currency2")
            {
                playerCurrency = playerDataManager.scritpableobjPlayerData.currency2;
                if (playerCurrency >= _itemData.itemcoinCount) hasEnoughCurrency = true;
            }

            if (!hasEnoughCurrency)
            {
                LogManager.LogWarning("재화가 부족하여 아이템을 구매할 수 없습니다.", LogManager.LogCategory.InventoryManager);
                // TODO: 사용자에게 재화 부족 알림 UI 표시
                return;
            }

            itemButton.interactable = false; // 중복 클릭 방지
            try
            {
                // 로컬 데이터 변경
                // 1. 재화 차감
                if (_itemData.itemcoinType == "currency1") playerDataManager.scritpableobjPlayerData.currency1 -= _itemData.itemcoinCount;
                else if (_itemData.itemcoinType == "currency2") playerDataManager.scritpableobjPlayerData.currency2 -= _itemData.itemcoinCount;
                
                // 2. 인벤토리에 아이템 추가
                inventoryManager.scritpableobjInventoryData.AddItem(_itemData);
                
                LogManager.Log($"'{_itemData.itemName}' 아이템 구매 성공. 서버에 데이터를 동기화합니다.", LogManager.LogCategory.InventoryManager);

                // 서버에 데이터 동기화 (병렬 처리로 성능 향상)
                await UniTask.WhenAll(
                    playerDataManager.UploadDataToServerAsync(),
                    inventoryManager.UploadDataToServerAsync()
                );

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
                itemButton.interactable = true; // 버튼 상호작용 복원
            }
        }
    }
}