using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace DogGuns_Games.Lobby
{
    /// <summary>
    /// 상점 아이템
    /// </summary>
    public class Store_Item : MonoBehaviour
    {
        Item_Data itemData;

        [Header("아이템 정보")] [SerializeField] private int itemCode; // 아이템 코드
        [SerializeField] private string itemName; // 아이템 이름
        [SerializeField] private string itemDescription; // 아이템 설명
        [SerializeField] private string itemType; // 아이템 코인 타입
        [SerializeField] private string itemCoinType; // 아이템 코인 타입
        [SerializeField] private int itemCoinCount; // 아이템 코인 개수
        private Inventory_Data _scritpableobjInventoryData;


        [Header("아이템 UI")] [SerializeField] private TMP_Text itemName_text; // 아이템 이름
        [SerializeField] private TMP_Text itemCoinCount_text; // 아이템 코인 개수
        [SerializeField] private TMP_Text itemDescription_text; // 아이템 설명
        [SerializeField] private Image itemImage; // 아이템 이미지
        [SerializeField] private Button itemButton; // 아이템 버튼

        public int ItemCode => itemCode;
        public string ItemName => itemName;
        public string ItemDescription => itemDescription;
        public string ItemType => itemType;
        public string ItemCoinType => itemCoinType;
        public Image ItemImage => itemImage;
        public int ItemCoinCount => itemCoinCount;

        private void Start()
        {
            itemData = ScriptableObject.CreateInstance<Item_Data>();
            itemData.itemCode = itemCode;
            itemData.itemName = itemName;
            itemData.itemtype = itemType;
            itemData.itemCount = itemCoinCount;
            itemData.itemcoinType = itemCoinType;
            itemData.itemcoinCount = itemCoinCount;

            itemCoinCount_text.text = itemCoinCount.ToString();
            itemName_text.text = itemName;
            itemDescription_text.text = itemDescription;

            itemButton.onClick.AddListener(Func_BuyItem);
        }


        private void Func_BuyItem()
        {
            //todo:
            // 아이템 구매
            // 서버에 아이템 구매 요청
            // 서버에서 아이템 구매 성공시
            // 인벤토리에 아이템 추가

            // 로컬 인벤토리 데이터에 아이템 추가
            InventoryDataManagerDontdestory.Instance.scritpableobjInventoryData.AddItem(itemData);
            // 변경된 인벤토리 데이터를 로컬에 저장
            InventoryDataManagerDontdestory.Instance.SaveInventoryData();
            // 변경된 인벤토리 데이터를 서버에 업로드
            InventoryDataManagerDontdestory.Instance.UploadDataToServer();
        }
    }
}