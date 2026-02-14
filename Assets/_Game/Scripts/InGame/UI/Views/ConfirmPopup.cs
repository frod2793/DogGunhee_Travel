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
    /// [설명]: 시스템 확인(Confirm) 및 취소(Cancel)를 위한 범용 팝업 뷰 클래스입니다.
    /// MVVM 기반으로 설계되었으며, ConfirmPopupViewModel의 상태를 구독하여 UI를 갱신하고
    /// 사용자 입력(클릭) 이벤트를 전달합니다.
    /// </summary>
    public class ConfirmPopup : MonoBehaviour
    {
        #region 에디터 설정

        [Header("텍스트 컴포넌트")]
        [SerializeField, Tooltip("팝업의 상단 제목을 표시하는 텍스트")]
        private TMP_Text m_titleText;

        [SerializeField, Tooltip("플레이어에게 전달할 본문 메시지 텍스트")]
        private TMP_Text m_messageText;

        [Header("버튼 컴포넌트")]
        [SerializeField, Tooltip("수락/진행 의사를 전달하는 확인 버튼")]
        private Button m_confirmButton;

        [SerializeField, Tooltip("거부/중단 의사를 전달하는 취소 버튼")]
        private Button m_cancelButton;

        [Header("제어 및 연출")]
        [SerializeField, Tooltip("팝업 전체의 투명도 조절 및 레이캐스트 차단을 위한 캔버스 그룹")]
        private CanvasGroup m_canvasGroup;

        [SerializeField, Tooltip("열기/닫기 시 스케일 애니메이션이 적용될 중심 트랜스폼")]
        private RectTransform m_popupTransform;

        [SerializeField, Tooltip("팝업 계층 구조의 최상위 게임 오브젝트")]
        private GameObject m_rootObject;

        #endregion

        #region 내부 필드

        /// <summary> 바인딩된 확인 팝업 비즈니스 로직 및 상태 보관소 </summary>
        private ConfirmPopupViewModel m_viewModel;

        /// <summary> 뷰의 생명주기 동안 유지되는 R3 이벤트 구독 해제 관리자 </summary>
        private readonly CompositeDisposable m_viewDisposables = new CompositeDisposable();

        #endregion

        #region 유니티 생명주기

        /// <summary>
        /// [설명]: 초기 기동 시 팝업을 투명/비활성 상태로 초기화하여 의도치 않은 노출을 방지합니다.
        /// </summary>
        private void Awake()
        {
            InitializeUI();
        }

        /// <summary>
        /// [설명]: 뷰가 파기될 때 모든 이벤트 스트림 구독을 정리하여 안정성을 확보합니다.
        /// </summary>
        private void OnDestroy()
        {
            m_viewDisposables.Dispose();
        }

        #endregion

        #region 바인딩 및 초기화

        /// <summary>
        /// [설명]: 외부에서 생성된 ViewModel을 주입받아 데이터 및 이벤트 스트림을 상호 바인딩합니다.
        /// </summary>
        /// <param name="viewModel">팝업 제어 로직을 담고 있는 뷰모델 인스턴스</param>
        public void Bind(ConfirmPopupViewModel viewModel)
        {
            if (viewModel == null)
            {
                return;
            }

            m_viewModel = viewModel;
            m_viewDisposables.Clear(); // 재사용 상황 고려하여 기존 구독 클리어

            BindButtons();
            BindData();
            BindVisibility();
        }

        /// <summary>
        /// [설명]: UI의 초기 시각적 상태(알파 0, 스케일 0)를 강제 설정합니다.
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

            // 루트 객체 우선 종료, 없을 시 자신 종료
            if (m_rootObject != null)
            {
                m_rootObject.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// [설명]: 버튼 클릭 이벤트를 뷰모델의 명령(Command)과 연결합니다.
        /// </summary>
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

        /// <summary>
        /// [설명]: 뷰모델의 문자열 데이터(제목, 내용) 변화를 감지하여 텍스트 UI를 실시간으로 갱신합니다.
        /// </summary>
        private void BindData()
        {
            if (m_viewModel == null)
            {
                return;
            }

            m_viewModel.Title
                .Subscribe(title =>
                {
                    if (m_titleText != null)
                    {
                        m_titleText.text = title;
                    }
                })
                .AddTo(m_viewDisposables);

            m_viewModel.Message
                .Subscribe(msg =>
                {
                    if (m_messageText != null)
                    {
                        m_messageText.text = msg;
                    }
                })
                .AddTo(m_viewDisposables);
        }

        /// <summary>
        /// [설명]: 뷰모델의 노출 상태 변수를 구독하여 열기/닫기 애니메이션을 유도합니다.
        /// </summary>
        private void BindVisibility()
        {
            if (m_viewModel == null)
            {
                return;
            }

            m_viewModel.IsVisible
                .Subscribe(isVisible =>
                {
                    if (isVisible)
                    {
                        OpenAnimation().Forget();
                    }
                    else
                    {
                        CloseAnimation().Forget();
                    }
                })
                .AddTo(m_viewDisposables);
        }

        #endregion

        #region UI 연출 (DOTween)

        /// <summary>
        /// [설명]: 팝업이 부드럽게 커지며 나타나는 오픈 트윈 연출을 수행합니다.
        /// 일시정지 상태에서도 동작하도록 설계되었습니다.
        /// </summary>
        private async UniTaskVoid OpenAnimation()
        {
            if (m_rootObject != null)
            {
                m_rootObject.SetActive(true);
            }
            else
            {
                gameObject.SetActive(true);
            }

            if (m_canvasGroup != null)
            {
                m_canvasGroup.blocksRaycasts = true;
                m_canvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
            }

            if (m_popupTransform != null)
            {
                // 약간의 반동(OutBack)을 활용한 팝업 효과
                await m_popupTransform.DOScale(1f, 0.2f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true)
                    .ToUniTask();
            }
        }

        /// <summary>
        /// [설명]: 팝업이 작아지며 사라지는 클로즈 트윈 연출을 수행한 후 오브젝트를 비활성화합니다.
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
                await m_popupTransform.DOScale(0f, 0.2f)
                    .SetEase(Ease.InBack)
                    .SetUpdate(true)
                    .ToUniTask();
            }

            if (m_rootObject != null)
            {
                m_rootObject.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        #endregion
    }
}