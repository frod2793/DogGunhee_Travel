using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using InGame.Services;
using R3;
using UnityEngine;

namespace InGame.Lobby.ViewModels
{
    /// <summary>
    /// [설명]: 로비의 우편함 시스템과 관련된 비즈니스 로직과 데이터 상태를 관리하는 ViewModel 클래스입니다.
    /// 서버로부터 우편 목록을 호출하고 보상 수령 처리를 수행합니다.
    /// </summary>
    public class PostViewModel : IDisposable
    {
        #region 반응형 프로퍼티

        /// <summary> [설명]: 서버로부터 수신한 우편 목록 데이터 </summary>
        public ReadOnlyReactiveProperty<List<PostService.PostInfo>> Posts => m_posts;

        private readonly ReactiveProperty<List<PostService.PostInfo>> m_posts =
            new ReactiveProperty<List<PostService.PostInfo>>(new List<PostService.PostInfo>());

        /// <summary> [설명]: 현재 유저가 상세 보기를 위해 선택한 우편 </summary>
        public ReadOnlyReactiveProperty<PostService.PostInfo> CurrentSelectedPost => m_currentSelectedPost;

        private readonly ReactiveProperty<PostService.PostInfo> m_currentSelectedPost =
            new ReactiveProperty<PostService.PostInfo>();

        /// <summary> [설명]: 통신 중 여부를 나타내는 로딩 상태 </summary>
        public ReadOnlyReactiveProperty<bool> IsLoading => m_loading;

        private readonly ReactiveProperty<bool> m_loading = new ReactiveProperty<bool>(false);

        #endregion

        #region 이벤트 발행

        /// <summary> [설명]: 서버 통신 등 로직 수행 중 발생한 에러 알림 </summary>
        public Observable<string> OnError => m_errorSubject;

        private readonly Subject<string> m_errorSubject = new Subject<string>();

        /// <summary> [설명]: 우편 보상 수령이 성공적으로 완료되었을 때의 알림 (수령 아이템 정보 포함) </summary>
        public Observable<string> OnRewardClaimed => m_rewardClaimedSubject;

        private readonly Subject<string> m_rewardClaimedSubject = new Subject<string>();

        #endregion

        #region 내부 변수 및 생성자

        private readonly CompositeDisposable m_disposables = new CompositeDisposable();
        private readonly IPostService m_postService;

        /// <summary>
        /// [설명]: PostViewModel의 기본 생성자입니다.
        /// </summary>
        public PostViewModel(IPostService postService)
        {
            m_postService = postService;
        }

        #endregion

        #region 비즈니스 로직

        /// <summary>
        /// [설명]: 서버로부터 관리자 우편과 쿠폰 우편 목록을 비동기로 로드하여 통합합니다.
        /// </summary>
        public async UniTask LoadPostsAsync()
        {
            if (m_loading.Value)
            {
                return;
            }

            m_loading.Value = true;
            try
            {
                if (m_postService == null) return;

                // 두 종류의 우편 목록을 요청
                var adminPosts = await m_postService.GetPostListAsync(BackEnd.PostType.Admin);
                var couponPosts = await m_postService.GetPostListAsync(BackEnd.PostType.Coupon);

                var allPosts = new List<PostService.PostInfo>(adminPosts.Count + couponPosts.Count);
                allPosts.AddRange(adminPosts);
                allPosts.AddRange(couponPosts);

                // 필요한 경우 여기서 정렬 로직 추가 (현재는 병합만 수행)
                m_posts.Value = allPosts;
            }
            catch (Exception ex)
            {
                string errorMsg = $"우편 로드 실패: {ex.Message}";
                m_errorSubject.OnNext(errorMsg);
                LogManager.LogError($"[PostViewModel] {errorMsg}", LogManager.LogCategory.PostManager);
            }
            finally
            {
                m_loading.Value = false;
            }
        }

        /// <summary>
        /// [설명]: 특정 우편 정보를 '현재 선택된 대상'으로 지정합니다.
        /// </summary>
        public void SelectPost(PostService.PostInfo postInfo)
        {
            m_currentSelectedPost.Value = postInfo;
        }

        /// <summary>
        /// [설명]: 상세 창에 노출된 현재 우편의 보상을 수령합니다.
        /// </summary>
        public void ClaimReward()
        {
            if (m_currentSelectedPost.Value != null)
            {
                ClaimRewardInternalAsync(m_currentSelectedPost.Value).Forget();
            }
        }

        /// <summary>
        /// [설명]: 목록에서 즉시 특정 우편의 보상을 수령합니다.
        /// </summary>
        public void ClaimReward(PostService.PostInfo postInfo)
        {
            if (postInfo != null)
            {
                ClaimRewardInternalAsync(postInfo).Forget();
            }
        }

        #endregion

        #region 내부 처리 로직

        /// <summary>
        /// [설명]: 서버에 보상 수령을 요청하고 성공 시 인벤토리 데이터를 갱신합니다.
        /// </summary>
        private async UniTaskVoid ClaimRewardInternalAsync(PostService.PostInfo postInfo)
        {
            if (postInfo.Items == null || postInfo.Items.Count == 0)
            {
                m_errorSubject.OnNext("수령할 아이템이 포함되어 있지 않은 우편입니다.");
                return;
            }

            if (m_loading.Value)
            {
                return;
            }

            m_loading.Value = true;

            try
            {
                if (m_postService == null) return;

                // 서버 연동: 실제 아이템 수령 처리
                bool isSuccess =
                    await m_postService.ReceivePostItemAsync(postInfo.PostType, postInfo.PostInDate);

                if (isSuccess)
                {
                    // 1. 로컬 인벤토리 데이터 매니저 갱신
                    // 1. 로컬 인벤토리 데이터 매니저 갱신
                    if (InventoryManager.Instance != null)
                    {
                        foreach (var item in postInfo.Items)
                        {
                            // [변경] 이름으로 아이템 데이터 조회 후 추가
                            var itemData = InventoryManager.Instance.ItemDatabase.GetItemDataByName(item.Key);
                            if (itemData != null)
                            {
                                InventoryManager.Instance.System.AddItem(itemData, item.Value);
                            }
                        }

                        InventoryManager.Instance.SaveInventory();
                    }

                    // 2. 현재 목록에서 해당 우편 제거 (ReactiveProperty 반응 유도)
                    var currentList = m_posts.Value;
                    if (currentList.Remove(postInfo))
                    {
                        m_posts.Value = new List<PostService.PostInfo>(currentList);
                    }

                    // 3. 현재 선택 정보가 수령된 우편이라면 초기화
                    if (m_currentSelectedPost.Value == postInfo)
                    {
                        m_currentSelectedPost.Value = null;
                    }

                    // 4. 성공 알림 발행
                    string rewardSummary = FormatRewardSummary(postInfo.Items);
                    m_rewardClaimedSubject.OnNext(rewardSummary);
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"보상 수령 실패: {ex.Message}";
                m_errorSubject.OnNext(errorMsg);
                LogManager.LogError($"[PostViewModel] {errorMsg}", LogManager.LogCategory.PostManager);
            }
            finally
            {
                m_loading.Value = false;
            }
        }

        /// <summary>
        /// [설명]: 획득한 아이템 딕셔너리를 안내용 문자열로 변환합니다.
        /// </summary>
        private string FormatRewardSummary(Dictionary<string, int> items)
        {
            if (items == null || items.Count == 0)
            {
                return "보상 없음";
            }

            return string.Join(", ", items.Select(x => $"{x.Key} {x.Value}개"));
        }

        #endregion

        #region 리소스 해제

        /// <summary>
        /// [설명]: 뷰모델 파기 시 모든 구독과 반응형 프로퍼티를 해제합니다.
        /// </summary>
        public void Dispose()
        {
            m_disposables.Dispose();

            m_posts.Dispose();
            m_currentSelectedPost.Dispose();
            m_loading.Dispose();

            m_errorSubject.Dispose();
            m_rewardClaimedSubject.Dispose();
        }

        #endregion
    }
}
