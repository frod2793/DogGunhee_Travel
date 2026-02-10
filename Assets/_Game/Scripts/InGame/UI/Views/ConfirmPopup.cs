using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using InGame.UI.ViewModels;

namespace InGame.UI.Views
{
    /// <summary>
    /// 확인/취소 팝업의 View 클래스입니다.
    /// <br/> ViewModel의 상태를 구독(R3)하여 UI를 갱신하고, 사용자 입력을 ViewModel로 전달합니다.
    /// </summary>
    public class ConfirmPopup : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)
        [Header("텍스트 컴포넌트")] 
        [SerializeField, Tooltip("팝업 제목 텍스트")] 
        private TMP_Text m_titleText;

        [SerializeField, Tooltip("팝업 본문 메시지 텍스트")] 
        private TMP_Text m_messageText;

        [Header("버튼 컴포넌트")] 
        [SerializeField, Tooltip("확인 버튼")] 
        private Button m_confirmButton;

        [SerializeField, Tooltip("취소 버튼")] 
        private Button m_cancelButton;

        [Header("제어 및 연출")] 
        [SerializeField, Tooltip("페이드 효과를 위한 캔버스 그룹")] 
        private CanvasGroup m_canvasGroup;

        [SerializeField, Tooltip("스케일 애니메이션 대상 트랜스폼")] 
        private RectTransform m_popupTransform;

        [SerializeField, Tooltip("팝업 최상위 오브젝트 (Active 제어용)")] 
        private GameObject m_rootObject; 
        #endregion

        #region 2. 내부 변수 및 상태
        // ViewModel 참조
        private ConfirmPopupViewModel m_viewModel;
        
        // R3 구독 해제 관리자 (View 파괴 시 일괄 해제)
        private readonly CompositeDisposable m_viewDisposables = new CompositeDisposable();
        #endregion

        #region 3. 유니티 생명주기
        private void Awake()
        {
            // 초기 상태 설정 (화면 깜빡임 방지)
            InitializeUI();
        }

        private void OnDestroy()
        {
            // 메모리 누수 방지를 위한 구독 해제
            m_viewDisposables.Dispose();
        }
        #endregion

        #region 4. 초기화 및 바인딩
        /// <summary>
        /// ViewModel과 View를 연결합니다. 
        /// <br/> 이전 바인딩을 초기화하고 새로운 ViewModel의 상태를 구독합니다.
        /// </summary>
        /// <param name="viewModel">연결할 팝업 ViewModel</param>
        public void Bind(ConfirmPopupViewModel viewModel)
        {
            m_viewModel = viewModel;
            m_viewDisposables.Clear(); // 재사용 시 기존 구독 정리

            BindButtons();
            BindData();
            BindVisibility();
        }

        /// <summary>
        /// 초기 UI 상태를 설정합니다. (투명화 및 비활성화)
        /// </summary>
        private void InitializeUI()
        {
            if (m_canvasGroup != null)
            {
                m_canvasGroup.alpha = 0f;
                m_canvasGroup.blocksRaycasts = false;
            }

            if (m_popupTransform != null)
            {
                m_popupTransform.localScale = Vector3.zero;
            }

            // RootObject가 없으면 자기 자신을 비활성화
            if (m_rootObject != null) m_rootObject.SetActive(false);
            else gameObject.SetActive(false);
        }

        private void BindButtons()
        {
            if (m_confirmButton != null)
            {
                m_confirmButton.OnClickAsObservable()
                    .Subscribe(_ => m_viewModel.Confirm())
                    .AddTo(m_viewDisposables);
            }

            if (m_cancelButton != null)
            {
                m_cancelButton.OnClickAsObservable()
                    .Subscribe(_ => m_viewModel.Cancel())
                    .AddTo(m_viewDisposables);
            }
        }

        private void BindData()
        {
            m_viewModel.Title
                .Subscribe(title => m_titleText.SetSafeText(title)) // 확장 메서드 가정
                .AddTo(m_viewDisposables);

            m_viewModel.Message
                .Subscribe(msg => m_messageText.SetSafeText(msg))
                .AddTo(m_viewDisposables);
        }

        private void BindVisibility()
        {
            m_viewModel.IsVisible
                .Subscribe(isVisible =>
                {
                    // 비동기 애니메이션 실행 (Fire-and-Forget)
                    if (isVisible) OpenAnimation().Forget();
                    else CloseAnimation().Forget();
                })
                .AddTo(m_viewDisposables);
        }
        #endregion

        #region 5. UI 애니메이션 (DOTween)
        /// <summary>
        /// 팝업이 나타나는 애니메이션을 재생합니다.
        /// <br/> UniTaskVoid: 이 메서드는 결과를 기다리지 않고 실행됩니다.
        /// </summary>
        private async UniTaskVoid OpenAnimation()
        {
            if (m_rootObject != null) m_rootObject.SetActive(true);
            else gameObject.SetActive(true);

            if (m_canvasGroup != null)
            {
                m_canvasGroup.blocksRaycasts = true;
                // SetUpdate(true): 게임이 일시정지(TimeScale=0) 상태에서도 팝업 애니메이션 작동
                m_canvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
            }

            if (m_popupTransform != null)
            {
                // Ease.OutBack: 약간 커졌다가 원래 크기로 돌아오는 탄성 효과
                await m_popupTransform.DOScale(1f, 0.2f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true)
                    .ToUniTask();
            }
        }

        /// <summary>
        /// 팝업이 사라지는 애니메이션을 재생하고 오브젝트를 비활성화합니다.
        /// </summary>
        private async UniTaskVoid CloseAnimation()
        {
            if (m_canvasGroup != null)
            {
                m_canvasGroup.blocksRaycasts = false;
                m_canvasGroup.DOFade(0f, 0.2f).SetUpdate(true);
            }

            if (m_popupTransform != null)
            {
                // Ease.InBack: 작아지기 전에 살짝 커졌다가 사라지는 효과
                await m_popupTransform.DOScale(0f, 0.2f)
                    .SetEase(Ease.InBack)
                    .SetUpdate(true)
                    .ToUniTask();
            }

            if (m_rootObject != null) m_rootObject.SetActive(false);
            else gameObject.SetActive(false);
        }
        #endregion
    }
}