using System;
using System.Collections.Generic;
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
    /// [설명]: 게임 클리어 시 성공 화면을 표시하는 View 클래스입니다.
    /// MVVM 패턴을 따르며 별 스탬프 연출 및 통계 데이터를 시각화합니다.
    /// </summary>
    public class GameClearPopupView : MonoBehaviour
    {
        #region 에디터 설정
        [Header("텍스트 UI")]
        [SerializeField, Tooltip("획득 코인 텍스트")] private TMP_Text m_coinText;
        [SerializeField, Tooltip("도달 웨이브 텍스트")] private TMP_Text m_waveText;
        [SerializeField, Tooltip("처치 수 텍스트")] private TMP_Text m_killText;

        [Header("버튼 UI")]
        [SerializeField, Tooltip("재시작 버튼")] private Button m_restartButton;
        [SerializeField, Tooltip("로비 이동 버튼")] private Button m_exitButton;

        [Header("별점 UI (스탬프 연출)")]
        [SerializeField, Tooltip("별 이미지 오브젝트들 (인덱스 0~2)")]
        private List<Image> m_starImages = new List<Image>();

        [Header("제어 및 연출")]
        [SerializeField, Tooltip("팝업 루트 게임 오브젝트")] private GameObject m_rootObject;
        [SerializeField, Tooltip("애니메이션 대상 패널")] private RectTransform m_popupPanel;
        [SerializeField, Tooltip("배경 페이드용 캔버스 그룹")] private CanvasGroup m_backgroundFade;
        #endregion

        #region 내부 필드
        private GameClearPopupViewModel m_viewModel;
        private readonly CompositeDisposable m_viewDisposables = new CompositeDisposable();
        #endregion

        #region 유니티 생명주기
        private void Awake()
        {
            if (m_rootObject != null) m_rootObject.SetActive(false);
            foreach (var star in m_starImages)
            {
                if (star != null) star.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            m_viewDisposables.Dispose();
        }
        #endregion

        #region 초기화 및 바인딩
        /// <summary>
        /// [설명]: 뷰모델을 바인딩하고 가시성 상태를 구독합니다.
        /// </summary>
        public void Bind(GameClearPopupViewModel viewModel)
        {
            if (viewModel == null) return;

            m_viewModel = viewModel;
            m_viewDisposables.Clear();

            // 1. 버튼 이벤트 연결
            if (m_restartButton != null)
            {
                m_restartButton.OnClickAsObservable()
                    .Subscribe(_ => m_viewModel.Restart())
                    .AddTo(m_viewDisposables);
            }

            if (m_exitButton != null)
            {
                m_exitButton.OnClickAsObservable()
                    .Subscribe(_ => m_viewModel.ExitToLobby())
                    .AddTo(m_viewDisposables);
            }

            // 2. 가시성 및 연출 구독
            m_viewModel.IsVisible
                .Subscribe(isVisible =>
                {
                    if (isVisible) ShowPopup().Forget();
                    else HidePopup();
                })
                .AddTo(m_viewDisposables);
        }
        #endregion

        #region UI 연출 로직
        /// <summary>
        /// [설명]: 팝업이 나타나는 애니메이션과 별 스탬프 시퀀스를 실행합니다.
        /// </summary>
        private async UniTaskVoid ShowPopup()
        {
            if (m_rootObject != null) m_rootObject.SetActive(true);

            // 데이터 동기화
            if (m_coinText != null) m_coinText.text = m_viewModel.CoinCount.CurrentValue.ToString();
            if (m_waveText != null) m_waveText.text = m_viewModel.WaveCount.CurrentValue.ToString();
            if (m_killText != null) m_killText.text = m_viewModel.KillCount.CurrentValue.ToString();

            // 배경 및 패널 오픈 연출
            if (m_backgroundFade != null)
            {
                m_backgroundFade.alpha = 0f;
                m_backgroundFade.DOFade(1f, 0.3f).SetUpdate(true).ToUniTask().Forget();
            }

            if (m_popupPanel != null)
            {
                m_popupPanel.localScale = Vector3.zero;
                await m_popupPanel.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetUpdate(true).ToUniTask();
            }

            // 별 스탬프 시퀀스 실행 (약간의 대기 후 시작)
            await UniTask.Delay(TimeSpan.FromSeconds(0.3f), ignoreTimeScale: true);
            await PlayStarStampSequence(m_viewModel.StarCount.CurrentValue);
        }

        /// <summary>
        /// [설명]: 획득한 별 개수만큼 "쿵" 찍히는 스탬프 효과를 재생합니다.
        /// </summary>
        private async UniTask PlayStarStampSequence(int starCount)
        {
            for (int i = 0; i < starCount; i++)
            {
                if (i >= m_starImages.Count || m_starImages[i] == null) continue;

                var star = m_starImages[i];
                star.gameObject.SetActive(true);
                star.transform.localScale = Vector3.one * 3f; // 크게 시작
                star.color = new Color(1, 1, 1, 0);

                // 쿵 찍기 연출
                star.DOFade(1f, 0.1f).SetUpdate(true).ToUniTask().Forget();
                await star.transform.DOScale(1f, 0.15f).SetEase(Ease.InQuad).SetUpdate(true).ToUniTask();
                
                // 타격 반동 (Punch / Shake)
                star.transform.DOPunchScale(Vector3.one * 0.2f, 0.2f).SetUpdate(true).ToUniTask().Forget();
                
                // 사운드 연출이 있다면 여기서 호출 가능
                // SoundManager.PlaySound(Sound.SFX, SoundKeys.Stamp, false);

                await UniTask.Delay(TimeSpan.FromSeconds(0.2f), ignoreTimeScale: true);
            }
        }

        private void HidePopup()
        {
            if (m_rootObject != null) m_rootObject.SetActive(false);
            
            // 별 상태 리셋
            foreach (var star in m_starImages)
            {
                if (star != null) star.gameObject.SetActive(false);
            }
        }
        #endregion
    }
}
