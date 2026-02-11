using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using R3;
using InGame.Services;
using InGame.Lobby.ViewModels;
using InGame.Lobby;

namespace InGame.UI.Popups
{
    /// <summary>
    /// 로비의 우편함 시스템을 시각화하고 제어하는 View 클래스입니다.
    /// <br/>비동기 데이터 로딩과 오브젝트 풀링을 통해 대량의 우편 목록을 효율적으로 처리합니다.
    /// </summary>
    public class PostView : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("<color=green>우편 목록 설정</color>")] [SerializeField, Tooltip("우편함 메인 패널 오브젝트")]
        private GameObject m_postBoxPanel;

        [SerializeField, Tooltip("우편 항목들이 생성될 부모 컨테이너")]
        private Transform m_postBoxContainer;

        [SerializeField, Tooltip("우편 인덱스 프리팹")]
        private PostIndex m_postBoxPrefab;

        [Header("<color=green>우편 상세 내용창</color>")] [SerializeField, Tooltip("상세 내용 표시 패널")]
        private GameObject m_postBoxDetailPanel;

        [SerializeField, Tooltip("본문 내용 텍스트")] private TMP_Text m_postBoxDetailText;

        [SerializeField, Tooltip("보낸 사람 이름 텍스트")]
        private TMP_Text m_postBoxSenderNameText;

        [SerializeField, Tooltip("보상 아이템 요약 텍스트")]
        private TMP_Text m_rewardItemNameText;

        #endregion

        #region 2. 내부 변수 및 상태

        private PostViewModel m_viewModel;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        // 성능 최적화를 위한 오브젝트 풀링 자료구조
        private readonly Queue<PostIndex> m_pooledItems = new Queue<PostIndex>();
        private readonly List<PostIndex> m_activeItems = new List<PostIndex>();

        #endregion

        #region 3. 유니티 생명주기

        private void Start()
        {
            InitializeViewModel();
            BindViewModel();

            // 진입 시 데이터 자동 갱신
            m_viewModel?.LoadPostsAsync().Forget();
        }

        private void OnDestroy()
        {
            m_disposables.Dispose();
            m_viewModel?.Dispose();
        }

        #endregion

        #region 4. MVVM 데이터 바인딩

        /// <summary>
        /// 우편 로직을 담당하는 뷰모델을 생성합니다.
        /// </summary>
        private void InitializeViewModel()
        {
            m_viewModel = new PostViewModel();
        }

        /// <summary>
        /// 뷰모델의 상태 변화에 따라 UI 갱신 시퀀스를 작동시킵니다.
        /// </summary>
        private void BindViewModel()
        {
            if (m_viewModel == null) return;

            // 1. 우편 목록 데이터 갱신 시 UI 리스트 리프레시
            m_viewModel.Posts
                .Subscribe(RefreshPostList)
                .AddTo(m_disposables);

            // 2. 에러 로그 출력
            m_viewModel.OnError
                .Subscribe(error => { LogManager.LogError($"[PostView] {error}", LogManager.LogCategory.PostManager); })
                .AddTo(m_disposables);

            // 3. 보상을 성공적으로 수령했을 때의 처리
            m_viewModel.OnRewardClaimed
                .Subscribe(rewardStr =>
                {
                    LogManager.Log($"[PostView] 보상 수령 완료: {rewardStr}", LogManager.LogCategory.PostManager);
                    ClosePostDetailPanel();
                })
                .AddTo(m_disposables);
        }

        #endregion

        #region 5. UI 리스트 갱신 (오브젝트 풀링 연동)

        /// <summary>
        /// 전달받은 우편 정보 목록을 기반으로 인스턴스를 하나씩 재사용하거나 생성합니다.
        /// </summary>
        private void RefreshPostList(List<PostService.PostInfo> postList)
        {
            // 1. 현재 사용 중인 모든 아이템을 풀로 회수
            foreach (var item in m_activeItems)
            {
                if (item != null)
                {
                    item.gameObject.SetActive(false);
                    m_pooledItems.Enqueue(item);
                }
            }

            m_activeItems.Clear();

            if (postList == null) return;

            // 2. 새로운 리스트 정보로 아이템 활성화
            foreach (var info in postList)
            {
                var postItem = GetPooledItem();

                // 상세 보기 람다 이벤트 구성
                UnityEngine.Events.UnityEvent onClickEvent = new UnityEngine.Events.UnityEvent();
                onClickEvent.AddListener(() => OpenPostDetailPanel(info));

                // 즉각 수령 람다 이벤트 구성
                UnityEngine.Events.UnityEvent onRewardEvent = new UnityEngine.Events.UnityEvent();
                onRewardEvent.AddListener(() => m_viewModel.ClaimReward(info));

                // UI에 데이터 주입
                postItem.SetPostIndex(info.Sender, info.Title, info.InDate, onRewardEvent, onClickEvent);
                m_activeItems.Add(postItem);
            }
        }

        #endregion

        #region 6. 패널 제어 및 상호작용 (Public/Private)

        /// <summary>
        /// 우편함 패널을 활성화하고 팝업 스택에 관리 동작을 등록합니다.
        /// </summary>
        public void OpenPostBoxPanel()
        {
            if (m_postBoxPanel == null) return;

            m_postBoxPanel.SetActive(true);
            PopupManager.Instance.RegisterPopup(ClosePostBoxPanel);

            // 매번 최신 데이터를 서버로부터 불러옴
            m_viewModel?.LoadPostsAsync().Forget();
        }

        /// <summary>
        /// 우편함 패널을 닫습니다.
        /// </summary>
        private void ClosePostBoxPanel()
        {
            if (m_postBoxPanel != null)
            {
                m_postBoxPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 특정 우편의 본문과 보상 내용을 포함한 상세 패널을 엽니다.
        /// </summary>
        private void OpenPostDetailPanel(PostService.PostInfo postInfo)
        {
            if (postInfo == null) return;

            m_viewModel?.SelectPost(postInfo);

            // 텍스트 정보 동기화
            if (m_postBoxDetailText != null) m_postBoxDetailText.SetText(postInfo.Content);
            if (m_postBoxSenderNameText != null) m_postBoxSenderNameText.SetText(postInfo.Sender);
            if (m_rewardItemNameText != null) m_rewardItemNameText.SetText(FormatRewardList(postInfo.Items));

            if (m_postBoxDetailPanel != null)
            {
                m_postBoxDetailPanel.SetActive(true);
                PopupManager.Instance.RegisterPopup(ClosePostDetailPanel);
            }
        }

        /// <summary>
        /// 우편 상세 정보 패널을 닫습니다.
        /// </summary>
        private void ClosePostDetailPanel()
        {
            if (m_postBoxDetailPanel != null && m_postBoxDetailPanel.activeSelf)
            {
                m_postBoxDetailPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 상세 정보창 내의 보상 수령 버튼 클릭 시 호출됩니다.
        /// </summary>
        public void OnClickDetailRewardBtn()
        {
            m_viewModel?.ClaimReward();
        }

        #endregion

        #region 7. 내부 풀링 유틸리티

        /// <summary>
        /// 비활성화된 우편 아이템을 풀에서 하나 가져오거나, 없으면 새로 생성합니다.
        /// </summary>
        private PostIndex GetPooledItem()
        {
            PostIndex item;
            if (m_pooledItems.Count > 0)
            {
                item = m_pooledItems.Dequeue();
                item.gameObject.SetActive(true);
            }
            else
            {
                item = Instantiate(m_postBoxPrefab, m_postBoxContainer);
            }

            return item;
        }

        /// <summary>
        /// 우편 데이터로부터 보상 목록을 읽기 좋은 문자열로 가공합니다.
        /// </summary>
        private string FormatRewardList(Dictionary<string, int> items)
        {
            if (items == null || items.Count == 0) return "지급 보상 없음";

            var stringBuilder = new System.Text.StringBuilder();
            foreach (var kvp in items)
            {
                stringBuilder.Append(kvp.Key).Append(" x ").Append(kvp.Value).Append(", ");
            }

            // 마지막 콤마 제거
            if (stringBuilder.Length > 2)
            {
                stringBuilder.Length -= 2;
            }

            return stringBuilder.ToString();
        }

        #endregion
    }
}