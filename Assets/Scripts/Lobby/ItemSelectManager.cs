using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace DogGuns_Games.Lobby
{
    public class ItemSelectManager : MonoBehaviour
    {
        #region 필드 및 변수

        [Header("<color=green>아이템 선택 UI</color>")]
        [SerializeField] private GameObject itemSelectPanel; // 아이템 선택 패널
        [SerializeField] private GameObject itemSelectContainer; // 아이템 선택 컨테이너
        [SerializeField] private Item_Index itemSelectPrefab; // 아이템 선택 프리팹
        [SerializeField] private GameObject itemSelectExtension; // 아이템 선택 확장 패널

        [Header("<color=green>아이템 확장창 UI</color>")]
        [SerializeField] private TMP_Text itemNameText; // 아이템 이름 텍스트
        [SerializeField] private Image itemImage; // 아이템 이미지
        [SerializeField] private TMP_Text itemDescriptionText; // 아이템 설명 텍스트
        [SerializeField] private Button itemSelectButton; // 아이템 선택 버튼

        // 아이템 데이터 관리
        private List<ItemData> _itemDataList = new List<ItemData>();
        private List<Item_Index> _itemIndexList = new List<Item_Index>();
        private int _currentSelectedItemIndex = -1;

        #endregion

        #region Unity 라이프사이클

        private void Awake()
        {
            InitializePanels();
        }

        private void Start()
        {
            LoadItemData();
            InitializeItemUI();

            // 초기 설정
            if (itemSelectButton != null)
            {
                itemSelectButton.onClick.AddListener(SelectCurrentItem);
            }
        }

        #endregion

        #region 초기화 메서드

        /// <summary>
        /// 패널 초기 상태 설정
        /// </summary>
        private void InitializePanels()
        {
            if (itemSelectPanel != null)
                itemSelectPanel.SetActive(false);

            if (itemSelectExtension != null)
                itemSelectExtension.SetActive(false);

            Debug.Log("아이템 패널 초기화 완료");
        }

        /// <summary>
        /// 아이템 데이터 로드
        /// </summary>
        private void LoadItemData()
        {
            // TODO: 실제 데이터 소스에서 아이템 데이터 로드
            // 임시 데이터 생성
            _itemDataList.Clear();

            // 테스트용 아이템 추가
            _itemDataList.Add(new ItemData
            {
                itemIndex = 0, itemName = "검", itemCode = "W001", itemDescription = "기본 공격력을 증가시키는 무기입니다.",
                itemSprite = null
            });
            _itemDataList.Add(new ItemData
            {
                itemIndex = 1, itemName = "방패", itemCode = "S001", itemDescription = "방어력을 증가시키는 방패입니다.",
                itemSprite = null
            });
            _itemDataList.Add(new ItemData
            {
                itemIndex = 2, itemName = "포션", itemCode = "P001", itemDescription = "체력을 회복시키는 물약입니다.",
                itemSprite = null
            });

            Debug.Log($"아이템 데이터 {_itemDataList.Count}개 로드 완료");
        }

        /// <summary>
        /// 아이템 UI 초기화
        /// </summary>
        private void InitializeItemUI()
        {
            ClearItemUI();
            CreateItemUI();
        }

        /// <summary>
        /// 기존 아이템 UI 정리
        /// </summary>
        private void ClearItemUI()
        {
            foreach (var item in _itemIndexList)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }

            _itemIndexList.Clear();
        }

        /// <summary>
        /// 아이템 UI 요소 생성
        /// </summary>
        /// <summary>
        /// 아이템 UI 요소 생성
        /// </summary>
        private void CreateItemUI()
        {
            if (itemSelectPrefab == null || itemSelectContainer == null)
            {
                Debug.LogError("아이템 선택 프리팹 또는 컨테이너가 null입니다.");
                return;
            }

            foreach (var itemData in _itemDataList)
            {
                Item_Index itemIndex = Instantiate(itemSelectPrefab, itemSelectContainer.transform);

                // 아이템 데이터 표시
                if (itemIndex.charactorName != null)
                {
                    itemIndex.charactorName.text = itemData.itemName;
                }

                if (itemIndex.thumbNail != null && itemData.itemSprite != null)
                {
                    itemIndex.thumbNail.sprite = itemData.itemSprite;
                }

                // 클릭 이벤트 설정 - 여기에서 아이템 프리팹 클릭 이벤트를 연결합니다
                if (itemIndex.openCharacterSelectButton != null)
                {
                    int index = itemData.itemIndex; // 클로저를 위한 변수
                    itemIndex.openCharacterSelectButton.onClick.RemoveAllListeners(); // 기존 리스너 제거
                    itemIndex.openCharacterSelectButton.onClick.AddListener(() => SelectItem(index));
                    Debug.Log($"아이템 {itemData.itemName}에 클릭 이벤트 등록 완료");
                }
                else
                {
                    Debug.LogError($"아이템 {itemData.itemName}의 버튼이 null입니다");
                }

                _itemIndexList.Add(itemIndex);
            }
        }

        #endregion

        #region 아이템 선택 메서드

        /// <summary>
        /// 아이템 선택 처리
        /// </summary>
        /// <summary>
        /// 아이템 선택 처리
        /// </summary>
        private void SelectItem(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= _itemDataList.Count)
            {
                Debug.LogError($"유효하지 않은 아이템 인덱스: {itemIndex}");
                return;
            }

            _currentSelectedItemIndex = itemIndex;

            // 아이템 정보 패널 업데이트 및 표시
            UpdateItemInfoPanel(itemIndex);
            OpenItemExtensionPanel();

            Debug.Log($"아이템 {itemIndex}번 선택됨: {_itemDataList[itemIndex].itemName}");
        }

        /// <summary>
        /// 현재 선택된 아이템 확정
        /// </summary>
        private void SelectCurrentItem()
        {
            if (_currentSelectedItemIndex >= 0 && _currentSelectedItemIndex < _itemDataList.Count)
            {
                // TODO: 아이템 선택 로직 구현 (플레이어 데이터에 저장 등)

                // 예: PlayerPrefs에 저장
                PlayerPrefs.SetInt("SelectedItemIndex", _currentSelectedItemIndex);
                PlayerPrefs.SetString("SelectedItemCode", _itemDataList[_currentSelectedItemIndex].itemCode);
                PlayerPrefs.Save();

                // 패널 닫기
                CloseItemExtensionPanel();
                CloseItemSelectPanel();

                Debug.Log($"아이템 선택 완료: {_itemDataList[_currentSelectedItemIndex].itemName}");
            }
            else
            {
                Debug.LogWarning("선택된 아이템이 없습니다.");
            }
        }

        /// <summary>
        /// 아이템 정보 패널 업데이트
        /// </summary>
        private void UpdateItemInfoPanel(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= _itemDataList.Count)
            {
                Debug.LogError($"유효하지 않은 아이템 인덱스: {itemIndex}");
                return;
            }

            ItemData itemData = _itemDataList[itemIndex];

            // UI 업데이트
            if (itemNameText != null)
                itemNameText.text = itemData.itemName;

            if (itemDescriptionText != null)
                itemDescriptionText.text = itemData.itemDescription;

            if (itemImage != null)
            {
                if (itemData.itemSprite != null)
                    itemImage.sprite = itemData.itemSprite;
                else
                    itemImage.sprite = null; // 기본 이미지 설정
            }
        }

        #endregion

        #region UI 패널 제어 메서드

        /// <summary>
        /// 아이템 선택 패널 열기
        /// </summary>
        public void OpenItemSelectPanel()
        {
            SetGameObjectActive(itemSelectPanel, true);
            SetGameObjectActive(itemSelectExtension, false);

            // 최신 데이터로 UI 리프레시
            InitializeItemUI();

            // 현재 선택된 아이템 초기화
            _currentSelectedItemIndex = -1;

            // 팝업 액션 등록
            LobbyUIManager.AddClosePopUpAction(CloseItemSelectPanel);

            Debug.Log("아이템 선택 패널 열림");
        }

        /// <summary>
        /// 아이템 확장 패널 열기
        /// </summary>
        private void OpenItemExtensionPanel()
        {
            if (itemSelectExtension != null)
            {
                itemSelectExtension.SetActive(true);

                // 팝업 액션 등록
                LobbyUIManager.AddClosePopUpAction(CloseItemExtensionPanel);

                Debug.Log("아이템 확장 패널 열림");
            }
            else
            {
                Debug.LogError("아이템 확장 패널이 null입니다");
            }
        }

        /// <summary>
        /// 아이템 선택 패널 닫기
        /// </summary>
        public void CloseItemSelectPanel()
        {
            SetGameObjectActive(itemSelectPanel, false);
            Debug.Log("아이템 선택 패널 닫힘");
        }

        /// <summary>
        /// 아이템 확장 패널 닫기
        /// </summary>
        private void CloseItemExtensionPanel()
        {
            SetGameObjectActive(itemSelectExtension, false);
            Debug.Log("아이템 확장 패널 닫힘");
        }

        #endregion

        #region 유틸리티 메서드

        /// <summary>
        /// 게임 오브젝트 활성화/비활성화 처리
        /// </summary>
        private static void SetGameObjectActive(GameObject obj, bool isActive = false)
        {
            if (obj != null)
                obj.SetActive(isActive);
            else
                Debug.LogWarning("활성화하려는 게임 오브젝트가 null입니다.");
        }

        #endregion

        #region 데이터 구조체

        /// <summary>
        /// 아이템 데이터 구조체
        /// </summary>
        [System.Serializable]
        private struct ItemData
        {
            public int itemIndex; // 아이템 번호
            public string itemName; // 아이템 이름
            public string itemCode; // 아이템 코드
            public string itemDescription; // 아이템 설명
            public Sprite itemSprite; // 아이템 이미지
        }

        #endregion
    }
}