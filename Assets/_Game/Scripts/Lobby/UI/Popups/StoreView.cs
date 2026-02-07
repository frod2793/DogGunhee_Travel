using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using R3;
using InGame.Lobby.ViewModels;
using InGame.UI.Elements;

namespace InGame.UI.Popups
{
    /// <summary>
    /// 상점 UI를 관리하는 View 클래스
    /// Addressable로 아이템 프리팹을 로드하고 StoreViewModel과 연동합니다.
    /// </summary>
    public class StoreView : MonoBehaviour
    {
        #region UI 컴포넌트

        [Header("상점 UI")]
        [SerializeField] private GameObject m_storePanel;
        [SerializeField] private RectTransform m_storeItemListContainer;
        
        // 아이템 프리팹 리스트 (Addressable 로드 결과)
        private List<GameObject> m_storeItemPrefabs = new List<GameObject>(); 
        
        // 생성된 아이템 뷰 리스트
        private List<StoreItemView> m_spawnedItems = new List<StoreItemView>();

        #endregion

        #region ViewModel & 상태

        private StoreViewModel m_viewModel;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        #endregion

        #region Unity 라이프사이클

        private void Start()
        {
            m_viewModel = new StoreViewModel();
            BindViewModel();
            LoadAddressableStoreItems(); // 비동기 로드 시작
        }

        private void OnDestroy()
        {
            m_disposables.Dispose();
            m_viewModel?.Dispose();
        }

        #endregion

        #region MVVM 바인딩

        private void BindViewModel()
        {
            // 에러 메시지
            m_viewModel.OnError
                .Subscribe(msg => LogManager.LogError(msg, LogManager.LogCategory.StoreManager))
                .AddTo(m_disposables);

            // 구매 성공 메시지
            m_viewModel.OnPurchaseSuccess
                .Subscribe(msg => 
                {
                    LogManager.Log(msg, LogManager.LogCategory.StoreManager);
                    // TODO: 성공 팝업 표시
                })
                .AddTo(m_disposables);

            // 재화 갱신 구독 (상점 UI에 재화 표시가 있다면 연결)
            // m_viewModel.Gold.Subscribe(...);
        }

        #endregion

        #region UI 제어 (Public)

        public void OpenStorePanel()
        {
            if (m_storePanel != null)
            {
                m_storePanel.SetActive(true);
                PopupManager.Instance.RegisterPopup(CloseStorePanel);
                
                // 열릴 때 재화 정보 갱신
                m_viewModel.RefreshCurrency();
            }
        }

        public void CloseStorePanel()
        {
            if (m_storePanel != null)
            {
                m_storePanel.SetActive(false);
            }
        }

        #endregion

        #region 아이템 로드 및 생성

        private void LoadAddressableStoreItems()
        {
            // 'Store_Item' 라벨로 에셋 로드
            Addressables.LoadAssetsAsync<GameObject>("Store_Item", null).Completed += OnStoreItemsLoaded;
        }

        private void OnStoreItemsLoaded(AsyncOperationHandle<IList<GameObject>> op)
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                m_storeItemPrefabs.Clear();
                foreach (var item in op.Result)
                {
                    if (item != null)
                    {
                        m_storeItemPrefabs.Add(item);
                    }
                }
                SpawnStoreItems();
            }
            else
            {
                LogManager.LogError("상점 아이템 로드 실패", LogManager.LogCategory.StoreManager);
            }
        }

        private void SpawnStoreItems()
        {
            // 기존 아이템 제거
            foreach (var item in m_spawnedItems)
            {
                if (item != null) Destroy(item.gameObject);
            }
            m_spawnedItems.Clear();

            // 프리팹 인스턴스화
            foreach (var prefab in m_storeItemPrefabs)
            {
                GameObject instance = Instantiate(prefab, m_storeItemListContainer);
                
                // StoreItemView 컴포넌트 확인
                StoreItemView itemView = instance.GetComponent<StoreItemView>();
                if (itemView == null)
                {
                    // 만약 프리팹에 아직 예전 스크립트(Store_Item)가 붙어있다면?
                    // GUID를 유지했으므로 StoreItemView로 인식될 것임.
                    itemView = instance.GetComponent<StoreItemView>();
                }

                if (itemView != null)
                {
                    // 이름 정리
                    instance.name = prefab.name + "_Item";
                    instance.transform.localScale = Vector3.one;

                    // 구매 이벤트 연결
                    itemView.OnPurchaseRequest += (code) => m_viewModel.PurchaseItem(code);
                    
                    m_spawnedItems.Add(itemView);
                }
            }
        }

        #endregion
    }
}