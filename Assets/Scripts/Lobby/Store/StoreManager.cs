using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DogGuns_Games.Lobby
{
    public class StoreManager : MonoBehaviour
    {
        #region 필드 및 변수

        [SerializeField] private RectTransform storeItemList; // 상점 아이템 리스트
        [SerializeField] private List<Store_Item> storeItems = new List<Store_Item>(); // 상점 아이템 리스트

        [SerializeField] private GameObject storeItemPrefab;
        [Header("<color=green>상점 UI")]
        [SerializeField] private GameObject storePanel;
        [SerializeField] private GameObject storeItemPopUp;
        
        #endregion

        #region Unity 라이프사이클

        private void Start()
        {
            LoadAddressableStoreItems();
        }

        #endregion

        private void StorePanelControl(bool isActive)
        {
            storePanel.SetActive(isActive);
        }
        private void StoreItemPopUpControl(bool isActive)
        {
            storeItemPopUp.SetActive(isActive);
        }
        
      /// <summary>
      /// 상점 패널을 화면에 표시합니다.
      /// </summary>
      public void OpenStorePanel()
      {
          Debug.Log("상점 패널 열기");
          StorePanelControl(true);
          LobbyUIManager.AddClosePopUpAction(CloseStorePanel);
      }
      
      /// <summary>
      /// 상점 아이템 팝업을 닫습니다.
      /// </summary>
      public void CloseStoreItemPopUp()
      {
          Debug.Log("상점 아이템 팝업 닫기");
          StoreItemPopUpControl(false);
      }

      public void CloseStorePanel()
      {
            Debug.Log("상점 패널 닫기");
            StorePanelControl(false);
      }
        
        
        #region 상점 아이템 로드

        private void LoadAddressableStoreItems()
        {
            storeItems.Clear();

            // Addressable 로드를 통해 GameObject 타입으로 상점 아이템 프리팹들을 로드하여 상점 리스트에 추가
            Addressables.LoadAssetsAsync<GameObject>("Store_Item", null).Completed += OnStoreItemsLoaded;
        }

        private void OnStoreItemsLoaded(AsyncOperationHandle<IList<GameObject>> op)
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                foreach (var item in op.Result)
                {
                    Store_Item storeItem = item.GetComponent<Store_Item>();
                    if (storeItem != null)
                    {
                        storeItems.Add(storeItem);
                    }
                }

                LoadStoreItems();
            }
            else
            {
                Debug.LogError("상점 아이템 로드에 실패했습니다. 'Store_Item' 레이블이 올바른지 및 자산이 제대로 설정되었는지 확인하세요.");
            }
        }

        private void LoadStoreItems()
        {
            foreach (var item in storeItems)
            {
                Store_Item newItem = Instantiate(item, storeItemList);
                //오브젝트 이름을 상점 아이템 이름 + item으로 변경
                newItem.name = item.name + " item";
                newItem.transform.localScale = Vector3.one;
            }
        }

        #endregion
    }
}