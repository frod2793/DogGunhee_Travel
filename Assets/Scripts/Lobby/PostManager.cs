using System;
using System.Collections.Generic;
using System.Text;
using BackEnd;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Vamser_like.Lobby
{
    /// <summary>
    /// 게임 내 우편 시스템을 관리하는 클래스 (헤더 한글화 적용)
    /// </summary>
    public class PostManager : MonoBehaviour
    {
        #region UI 요소

        [Header("우편함 기본 UI")]
        [Tooltip("우편함 전체 패널")]
        [SerializeField] private GameObject m_postBoxPanel;
        [Tooltip("우편 목록이 생성될 부모 트랜스폼")]
        [SerializeField] private Transform m_postBoxContainer;
        [Tooltip("생성할 우편 아이템 프리팹")]
        [SerializeField] private PostIndex m_postBoxPrefab;

        [Header("우편함 상세 UI")]
        [Tooltip("우편 상세 정보 패널")]
        [SerializeField] private GameObject m_postBoxDetailPanel;
        [Tooltip("우편 내용 텍스트")]
        [SerializeField] private TMP_Text m_postBoxDetailText;
        [Tooltip("보낸 사람 이름 텍스트")]
        [SerializeField] private TMP_Text m_postBoxSenderNameText;
        [Tooltip("보상 아이템 목록 텍스트")]
        [SerializeField] private TMP_Text m_rewardItemNameText;

        #endregion

        #region 내부 데이터 및 풀링

        // 현재 선택된 우편 정보
        private ServerManager.PostInfo m_currentPostInfo;

        // StringBuilder 캐싱
        private readonly StringBuilder m_stringBuilder = new StringBuilder(100);

        // 오브젝트 풀링을 위한 큐
        private readonly Queue<PostIndex> m_pooledItems = new Queue<PostIndex>();

        // 활성화된 아이템 추적 (Key: PostInDate)
        private readonly Dictionary<string, PostIndex> m_activeItems = new Dictionary<string, PostIndex>();

        #endregion

        #region Unity 라이프사이클

        private void Start()
        {
            InitializePostSystemAsync().Forget();
        }

        #endregion

        #region 초기화 및 데이터 로드

        private async UniTaskVoid InitializePostSystemAsync()
        {
            if (m_postBoxPanel != null) m_postBoxPanel.SetActive(false);
            if (m_postBoxDetailPanel != null) m_postBoxDetailPanel.SetActive(false);

            try
            {
                var (adminPosts, couponPosts) = await UniTask.WhenAll(
                    ServerManager.Instance.GetPostListAsync(PostType.Admin),
                    ServerManager.Instance.GetPostListAsync(PostType.Coupon)
                );

                var allPosts = new List<ServerManager.PostInfo>(adminPosts.Count + couponPosts.Count);
                allPosts.AddRange(adminPosts);
                allPosts.AddRange(couponPosts);

                RefreshPostList(allPosts);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"우편 로드 실패: {ex.Message}", LogManager.LogCategory.PostManager);
            }
        }

        private void RefreshPostList(List<ServerManager.PostInfo> postList)
        {
            foreach (var item in m_activeItems.Values)
            {
                item.gameObject.SetActive(false);
                m_pooledItems.Enqueue(item);
            }
            m_activeItems.Clear();

            foreach (var info in postList)
            {
                PostIndex postItem = GetPooledItem();
                
                UnityEvent onClick = new UnityEvent();
                onClick.AddListener(() => OpenPostDetailPanel(info));

                UnityEvent onReward = new UnityEvent();
                onReward.AddListener(() => HandleRewardClaim(info));

                postItem.SetPostIndex(info.Sender, info.Title, info.InDate, onReward, onClick);
                
                if (!m_activeItems.ContainsKey(info.PostInDate))
                {
                    m_activeItems.Add(info.PostInDate, postItem);
                }
            }
        }

        #endregion

        #region 오브젝트 풀링 시스템

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

        private void ReturnToPool(string inDate)
        {
            if (m_activeItems.TryGetValue(inDate, out PostIndex item))
            {
                item.gameObject.SetActive(false);
                m_pooledItems.Enqueue(item);
                m_activeItems.Remove(inDate);
            }
        }

        #endregion

        #region UI 상호작용

        public void OpenPostBoxPanel()
        {
            if (m_postBoxPanel == null) return;
            m_postBoxPanel.SetActive(true);
            LobbyUIManager.AddClosePopUpAction(ClosePostBoxPanel);
        }

        private void ClosePostBoxPanel()
        {
            if (m_postBoxPanel == null) return;
            m_postBoxPanel.SetActive(false);
        }

        private void OpenPostDetailPanel(ServerManager.PostInfo postInfo)
        {
            m_currentPostInfo = postInfo;

            if (m_postBoxDetailText != null) m_postBoxDetailText.text = postInfo.Content;
            if (m_postBoxSenderNameText != null) m_postBoxSenderNameText.text = postInfo.Sender;
            if (m_rewardItemNameText != null) m_rewardItemNameText.text = GenerateRewardString(postInfo.Items);

            if (m_postBoxDetailPanel != null) m_postBoxDetailPanel.SetActive(true);
            LobbyUIManager.AddClosePopUpAction(ClosePostDetailPanel);
        }

        private void ClosePostDetailPanel()
        {
            if (m_postBoxDetailPanel == null) return;
            m_postBoxDetailPanel.SetActive(false);
        }

        #endregion

        #region 보상 처리 로직

        public void OnClickDetailRewardBtn()
        {
            if (m_currentPostInfo == null) return;
            HandleRewardClaim(m_currentPostInfo);
        }

        private void HandleRewardClaim(ServerManager.PostInfo postInfo)
        {
            ProcessRewardAsync(postInfo).Forget();
        }

        private async UniTaskVoid ProcessRewardAsync(ServerManager.PostInfo postInfo)
        {
            if (postInfo.Items == null || postInfo.Items.Count == 0)
            {
                LogManager.LogWarning("수령할 아이템이 없습니다.", LogManager.LogCategory.PostManager);
                return;
            }

            try
            {
                bool isSuccess = await ServerManager.Instance.ReceivePostItemAsync(postInfo.PostType, postInfo.PostInDate);

                if (!isSuccess) return;

                if (InventoryDataManagerDontdestory.Instance != null)
                {
                    foreach (var item in postInfo.Items)
                    {
                        InventoryDataManagerDontdestory.Instance.GetItemByName(item.Key, item.Value);
                    }
                }

                LogManager.Log($"보상 수령 완료: {GenerateRewardString(postInfo.Items)}", LogManager.LogCategory.PostManager);

                ReturnToPool(postInfo.PostInDate); 
                
                if (m_postBoxDetailPanel.activeSelf && m_currentPostInfo == postInfo)
                {
                    ClosePostDetailPanel();
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"보상 수령 중 예외 발생: {ex.Message}", LogManager.LogCategory.PostManager);
            }
        }

        #endregion

        #region 유틸리티

        private string GenerateRewardString(Dictionary<string, int> items)
        {
            if (items == null || items.Count == 0) return "보상 없음";

            m_stringBuilder.Clear();
            foreach (var item in items)
            {
                m_stringBuilder.Append(item.Key).Append(" ").Append(item.Value).Append("개, ");
            }

            if (m_stringBuilder.Length > 2)
            {
                m_stringBuilder.Length -= 2;
            }

            return m_stringBuilder.ToString();
        }

        #endregion
    }
}