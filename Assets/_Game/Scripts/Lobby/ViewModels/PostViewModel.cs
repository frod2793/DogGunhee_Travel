using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using InGame.Services;
using R3;
using UnityEngine;
using BackEnd;

namespace InGame.Lobby.ViewModels
{
    /// <summary>
    /// 우편 시스템의 비즈니스 로직과 상태를 관리하는 ViewModel
    /// </summary>
    public class PostViewModel : IDisposable
    {
        #region 상태 프로퍼티 (View 바인딩용)

        // 우편 리스트
        public ReadOnlyReactiveProperty<List<PostService.PostInfo>> Posts => m_posts;
        private readonly ReactiveProperty<List<PostService.PostInfo>> m_posts = new ReactiveProperty<List<PostService.PostInfo>>(new List<PostService.PostInfo>());

        // 현재 선택된 우편
        public ReadOnlyReactiveProperty<PostService.PostInfo> CurrentSelectedPost => m_currentSelectedPost;
        private readonly ReactiveProperty<PostService.PostInfo> m_currentSelectedPost = new ReactiveProperty<PostService.PostInfo>();

        // 로딩 상태
        public ReadOnlyReactiveProperty<bool> IsLoading => m_loading;
        private readonly ReactiveProperty<bool> m_loading = new ReactiveProperty<bool>(false);

        // 에러 메시지 이벤트
        public Observable<string> OnError => m_errorSubject;
        private readonly Subject<string> m_errorSubject = new Subject<string>();

        // 보상 수령 완료 이벤트 (수령한 아이템 목록 문자열 반환)
        public Observable<string> OnRewardClaimed => m_rewardClaimedSubject;
        private readonly Subject<string> m_rewardClaimedSubject = new Subject<string>();

        #endregion

        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        public PostViewModel()
        {
            // 초기화 시 데이터 로드 ? (선택사항, View에서 호출하도록 함)
        }

        /// <summary>
        /// 우편 데이터 로드
        /// </summary>
        public async UniTask LoadPostsAsync()
        {
            if (m_loading.Value) return;

            m_loading.Value = true;
            try
            {
                var results = await UniTask.WhenAll(
                    ServerManager.Instance.GetPostListAsync(PostType.Admin),
                    ServerManager.Instance.GetPostListAsync(PostType.Coupon)
                );

                var adminPosts = results.Item1;
                var couponPosts = results.Item2;

                var allPosts = new List<PostService.PostInfo>(adminPosts.Count + couponPosts.Count);
                allPosts.AddRange(adminPosts);
                allPosts.AddRange(couponPosts);

                // 날짜순 정렬 등 필요시 추가
                m_posts.Value = allPosts;
            }
            catch (Exception ex)
            {
                m_errorSubject.OnNext($"우편 로드 실패: {ex.Message}");
                LogManager.LogError($"우편 로드 실패: {ex.Message}", LogManager.LogCategory.PostManager);
            }
            finally
            {
                m_loading.Value = false;
            }
        }

        /// <summary>
        /// 우편 선택
        /// </summary>
        public void SelectPost(PostService.PostInfo postInfo)
        {
            m_currentSelectedPost.Value = postInfo;
        }

        /// <summary>
        /// 현재 선택된 우편 보상 수령
        /// </summary>
        public void ClaimReward()
        {
            if (m_currentSelectedPost.Value == null) return;
            ClaimRewardAsync(m_currentSelectedPost.Value).Forget();
        }

        /// <summary>
        /// 특정 우편 보상 수령
        /// </summary>
        public void ClaimReward(PostService.PostInfo postInfo)
        {
            ClaimRewardAsync(postInfo).Forget();
        }

        private async UniTaskVoid ClaimRewardAsync(PostService.PostInfo postInfo)
        {
            if (postInfo.Items == null || postInfo.Items.Count == 0)
            {
                m_errorSubject.OnNext("수령할 아이템이 없습니다.");
                return;
            }

            if (m_loading.Value) return;
            m_loading.Value = true;

            try
            {
                bool isSuccess = await ServerManager.Instance.ReceivePostItemAsync(postInfo.PostType, postInfo.PostInDate);

                if (isSuccess)
                {
                    // 인벤토리 업데이트
                    if (InventoryDataManager.Instance != null)
                    {
                        foreach (var item in postInfo.Items)
                        {
                            InventoryDataManager.Instance.GetItemByName(item.Key, item.Value);
                        }
                    }

                    // 수령 완료 목록 갱신 (로컬 리스트에서 제거)
                    var currentList = m_posts.Value;
                    if (currentList.Remove(postInfo))
                    {
                        // 리스트 갱신 알림 (새로운 리스트 인스턴스로 교체해야 ReactiveProperty가 반응함)
                        // 또는 R3 컬렉션 사용 고려. 여기서는 단순 리스트 교체.
                        m_posts.Value = new List<PostService.PostInfo>(currentList);
                    }
                    
                    // 현재 선택된 우편이었다면 선택 해제
                    if (m_currentSelectedPost.Value == postInfo)
                    {
                        m_currentSelectedPost.Value = null;
                    }

                    // 보상 문자열 생성
                    string rewardString = GenerateRewardString(postInfo.Items);
                    m_rewardClaimedSubject.OnNext(rewardString);
                }
            }
            catch (Exception ex)
            {
                m_errorSubject.OnNext($"보상 수령 실패: {ex.Message}");
                LogManager.LogError($"보상 수령 실패: {ex.Message}", LogManager.LogCategory.PostManager);
            }
            finally
            {
                m_loading.Value = false;
            }
        }

        private string GenerateRewardString(Dictionary<string, int> items)
        {
            if (items == null || items.Count == 0) return "보상 없음";
            return string.Join(", ", items.Select(x => $"{x.Key} {x.Value}개"));
        }

        public void Dispose()
        {
            m_disposables.Dispose();
            m_posts.Dispose();
            m_currentSelectedPost.Dispose();
            m_loading.Dispose();
            m_errorSubject.Dispose();
            m_rewardClaimedSubject.Dispose();
        }
    }
}
