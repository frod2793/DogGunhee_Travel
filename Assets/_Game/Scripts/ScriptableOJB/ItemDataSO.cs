using UnityEngine;

namespace InGame
{
    [CreateAssetMenu(fileName = "ItemDataSO", menuName = "GameData/ItemDataSO")]
    public class ItemDataSO : ScriptableObject
    {
        public string itemName; // 아이템 이름
        public int itemCode; // 아이템 코드
        public string itemtype; // 아이템 타입
        public int itemCount; // 아이템 개수
        public string itemcoinType; // 아이템 코인 타입
        public int itemcoinCount; // 아이템 코인 개수
        
        [Tooltip("아이템의 상세 설명입니다."), TextArea(3, 5)]
        public string itemDescription; // 아이템 설명
        public Sprite itemIcon; // 아이템 아이콘
    }
}