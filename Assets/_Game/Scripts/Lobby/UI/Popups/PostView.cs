using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using R3;
using InGame.Services;
using InGame.Lobby.ViewModels;

namespace InGame.UI.Popups
{
    /// <summary>
    /// 우편함 UI를 관리하는 View 클래스
    /// PostViewModel과 바인딩되어 데이터를 표시합니다.
    /// </summary>
    public class PostView : MonoBehaviour
    {
        #region UI 컴포넌트

        [Header("우편함 기본 UI")]
        [SerializeField] private GameObject m_postBoxPanel;
        [SerializeField] private Transform m_postBoxContainer;
        [SerializeField] private InGame.Lobby.PostIndex m_postBoxPrefab;

        [Header("우편함 상세 UI")]
        [SerializeField] private GameObject m_postBoxDetailPanel;
        [SerializeField] private TMP_Text m_postBoxDetailText;
        [SerializeField] private TMP_Text m_postBoxSenderNameText;
        [SerializeField] private TMP_Text m_rewardItemNameText;

        #endregion

        #region ViewModel & 상태

        private PostViewModel m_viewModel;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        // 오브젝트 풀링
        private readonly Queue<InGame.Lobby.PostIndex> m_pooledItems = new Queue<InGame.Lobby.PostIndex>();
        private readonly List<InGame.Lobby.PostIndex> m_activeItems = new List<InGame.Lobby.PostIndex>();

        #endregion

        #region Unity 라이프사이클

        private void Start()
        {
            // ViewModel 초기화
            m_viewModel = new PostViewModel();

            // 데이터 바인딩
            BindViewModel();

            // 초기 데이터 로드 요청
            m_viewModel.LoadPostsAsync().Forget();
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
            // 우편 리스트 갱신 구독
            m_viewModel.Posts
                .Subscribe(RefreshPostList)
                .AddTo(m_disposables);

            // 로딩 상태 구독 (필요 시 로딩 UI 표시)
            m_viewModel.IsLoading
                .Subscribe(isLoading => 
                {
                    // TODO: 로딩 인디케이터 제어
                })
                .AddTo(m_disposables);

            // 에러 메시지 구독
            m_viewModel.OnError
                .Subscribe(error => 
                {
                    LogManager.LogError(error, LogManager.LogCategory.PostManager);
                    // TODO: 에러 팝업 표시
                })
                .AddTo(m_disposables);

            // 보상 수령 완료 알림 구독
            m_viewModel.OnRewardClaimed
                .Subscribe(rewardString => 
                {
                    LogManager.Log($"보상 수령: {rewardString}", LogManager.LogCategory.PostManager);
                    // 상세 패널 닫기 (현재 선택된 우편을 수령했다면)
                    ClosePostDetailPanel();
                })
                .AddTo(m_disposables);
        }

        #endregion

        #region UI 업데이트 메서드

        private void RefreshPostList(List<PostService.PostInfo> postList)
        {
            // 1. 기존 아이템 모두 풀로 반환
            foreach (var item in m_activeItems)
            {
                item.gameObject.SetActive(false);
                m_pooledItems.Enqueue(item);
            }
            m_activeItems.Clear();

            // 2. 새 리스트 생성
            if (postList == null) return;

            foreach (var info in postList)
            {
                var postItem = GetPooledItem();
                
                // 클릭 이벤트: 상세 보기
                UnityEngine.Events.UnityEvent onClick = new UnityEngine.Events.UnityEvent();
                onClick.AddListener(() => OpenPostDetailPanel(info));

                // 수령 이벤트: 바로 받기
                UnityEngine.Events.UnityEvent onReward = new UnityEngine.Events.UnityEvent();
                onReward.AddListener(() => m_viewModel.ClaimReward(info));

                postItem.SetPostIndex(info.Sender, info.Title, info.InDate, onReward, onClick);
                m_activeItems.Add(postItem);
            }
        }

        #endregion

        #region 패널 제어 (Public)

        public void OpenPostBoxPanel()
        {
            if (m_postBoxPanel == null) return;
            
            m_postBoxPanel.SetActive(true);
            PopupManager.Instance.RegisterPopup(ClosePostBoxPanel);

            // 열릴 때 최신 데이터 로드 시도
            m_viewModel.LoadPostsAsync().Forget();
        }

        private void ClosePostBoxPanel()
        {
            if (m_postBoxPanel == null) return;
            m_postBoxPanel.SetActive(false);
        }

        private void OpenPostDetailPanel(PostService.PostInfo postInfo)
        {
            m_viewModel.SelectPost(postInfo);

            // UI 갱신
            if (m_postBoxDetailText != null) m_postBoxDetailText.text = postInfo.Content;
            if (m_postBoxSenderNameText != null) m_postBoxSenderNameText.text = postInfo.Sender;
            if (m_rewardItemNameText != null) m_rewardItemNameText.text = GenerateRewardString(postInfo.Items);

            if (m_postBoxDetailPanel != null)
            {
                m_postBoxDetailPanel.SetActive(true);
                PopupManager.Instance.RegisterPopup(ClosePostDetailPanel);
            }
        }

        private void ClosePostDetailPanel()
        {
            if (m_postBoxDetailPanel == null || !m_postBoxDetailPanel.activeSelf) return;
            
            m_postBoxDetailPanel.SetActive(false);
            // 팝업 매니저 스택에서도 제거해야 함 (만약 ESC로 닫힌게 아니라면)
            // 하지만 PopupManager는 '닫기 동작'을 수행하는 것이므로, 
            // 여기서 직접 닫았으면 스택 처리가 꼬일 수 있음.
            // PopupManager 구조상 CloseTopPopup()을 호출하는 것이 안전함.
            // 다만 여기서는 '특정 패널'을 닫는 것이라 로직이 약간 복잡해짐.
            // 일단 단순화: ESC로 닫히거나 버튼으로 닫힐 때 CloseTopPopup()을 호출하도록 유도.
        }

        /// <summary>
        /// 상세 창에서 보상 수령 버튼 클릭 시
        /// </summary>
        public void OnClickDetailRewardBtn()
        {
            m_viewModel.ClaimReward();
        }

        #endregion

        #region 오브젝트 풀링 (Private)

        private InGame.Lobby.PostIndex GetPooledItem()
        {
            InGame.Lobby.PostIndex item;
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

        #endregion

        #region 유틸리티

        // ViewModel에도 있지만 View 표시용으로 간단히 구현 (또는 ViewModel에서 문자열을 받아도 됨)
        private string GenerateRewardString(Dictionary<string, int> items)
        {
             if (items == null || items.Count == 0) return "보상 없음";
             // 간단 구현
             var sb = new System.Text.StringBuilder();
             foreach (var kv in items)
             {
                 sb.Append(kv.Key).Append(" ").Append(kv.Value).Append("개, ");
             }
             if (sb.Length > 2) sb.Length -= 2;
             return sb.ToString();
        }

        #endregion
    }
}