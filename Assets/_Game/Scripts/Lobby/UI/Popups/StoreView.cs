using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using R3;
using InGame.Lobby.ViewModels;
using InGame.UI.Elements;

namespace InGame.UI.Popups
{
    /// <summary>
    /// [설명]: 게임 내 상점 시스템을 총괄적으로 시각화하는 View 클래스입니다.
    /// Addressable 시스템을 연동하여 런타임에 동적으로 아이템 프리팹을 로드하고 표시합니다.
    /// </summary>
    public class StoreView : MonoBehaviour
    {
        #region 에디터 설정

        [Header("<color=green>상점 메인 설정</color>")]
        [SerializeField, Tooltip("상점 팝업 패널 오브젝트")]
        private GameObject m_storePanel;

        [SerializeField, Tooltip("상점 아이템들이 배치될 리스트 부모 컨테이너")]
        private RectTransform m_storeItemListContainer;

        #endregion

        #region 내부 변수

        private StoreViewModel m_viewModel;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        // Addressable로 로드된 에셋 및 활성화된 인스턴스 관리
        private readonly List<GameObject> m_storeItemPrefabs = new List<GameObject>();
        private readonly List<StoreItemView> m_spawnedItems = new List<StoreItemView>();

        #endregion

        #region 초기화 및 바인딩 로직

        /// <summary>
        /// [설명]: 외부(LobbyUIViewManager 등)로부터 의존성을 주입받아 초기화합니다.
        /// </summary>
        public void Initialize(InGame.Data.PlayerDataDTO playerData, InGame.Services.PlayerDataService playerService)
        {
            if (m_viewModel != null) return;

            m_viewModel = new StoreViewModel(playerData, playerService);
            BindViewModel();
        }

        #endregion

        #region 유니티 생명주기

        private void Start()
        {
            // 상점 에셋(아이템들) 비동기 로드 시작
            LoadAddressableStoreAssets();
        }

        private void OnDestroy()
        {
            m_disposables.Dispose();
            m_viewModel?.Dispose();
        }

        /// <summary>
        /// [설명]: 뷰모델의 상태 피드백을 구독하여 로그 또는 UI 이벤트를 처리합니다.
        /// </summary>
        private void BindViewModel()
        {
            if (m_viewModel == null)
            {
                return;
            }

            // 1. 구매 중 에러 발생 시 처리
            m_viewModel.OnError
                .Subscribe(msg => LogManager.LogError($"[StoreView] {msg}", LogManager.LogCategory.StoreManager))
                .AddTo(m_disposables);

            // 2. 구매 성공 피드백 알림
            m_viewModel.OnPurchaseSuccess
                .Subscribe(msg =>
                {
                    LogManager.Log($"[StoreView] {msg}", LogManager.LogCategory.StoreManager);
                    // TODO: 연출용 성공 팝업 트리거
                })
                .AddTo(m_disposables);
        }

        #endregion

        #region 상점 패널 제어

        /// <summary>
        /// [설명]: 상점 패널을 활성화하고 최신 재화 상태를 뷰모델에 요청합니다.
        /// </summary>
        public void OpenStorePanel()
        {
            if (m_storePanel == null)
            {
                return;
            }

            m_storePanel.SetActive(true);
            PopupManager.Instance.RegisterPopup(CloseStorePanel);

            // 열릴 때 최신 골드/다이아 갱신
            m_viewModel?.RefreshCurrency();
        }

        /// <summary>
        /// [설명]: 상점 패널을 비활성화합니다.
        /// </summary>
        public void CloseStorePanel()
        {
            if (m_storePanel != null)
            {
                m_storePanel.SetActive(false);
            }
        }

        #endregion

        #region Addressable 및 UI 빌드 로직

        /// <summary>
        /// [설명]: 'Store_Item' 라벨이 붙은 모든 Addressable 에셋을 비동기로 로드합니다.
        /// </summary>
        private void LoadAddressableStoreAssets()
        {
            Addressables.LoadAssetsAsync<GameObject>("Store_Item", null).Completed += OnStoreItemsLoadedNotify;
        }

        /// <summary>
        /// [설명]: 에셋 로드가 완료되었을 때 호출되는 콜백입니다. 성공 시 인스턴스화를 시작합니다.
        /// </summary>
        private void OnStoreItemsLoadedNotify(AsyncOperationHandle<IList<GameObject>> op)
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

                BuildStoreItemUI();
            }
            else
            {
                LogManager.LogError("[StoreView] 상점 프리팹 에셋 로딩에 실패했습니다.", LogManager.LogCategory.StoreManager);
            }
        }

        /// <summary>
        /// [설명]: 로드된 프리팹 목록을 바탕으로 실제 UI 아이템들을 생성하고 이벤트를 연결합니다.
        /// </summary>
        private void BuildStoreItemUI()
        {
            // 기존 목록 청소
            foreach (var item in m_spawnedItems)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }

            m_spawnedItems.Clear();

            // 컨테이너에 신규 아이템들 배치
            foreach (var prefab in m_storeItemPrefabs)
            {
                GameObject instance = Instantiate(prefab, m_storeItemListContainer);
                StoreItemView itemView = instance.GetComponent<StoreItemView>();

                if (itemView != null)
                {
                    instance.name = $"{prefab.name}_ViewItem";
                    instance.transform.localScale = Vector3.one;

                    // 개별 아이템의 구매 요청 이벤트를 뷰모델에 바인딩
                    itemView.OnPurchaseRequest += (code) => m_viewModel?.PurchaseItem(code);

                    m_spawnedItems.Add(itemView);
                }
            }
        }

        #endregion
    }
}