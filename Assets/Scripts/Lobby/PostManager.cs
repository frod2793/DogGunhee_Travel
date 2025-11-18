using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace DogGuns_Games.Lobby
{
    /// <summary>
    /// 게임 내 우편 시스템을 관리하는 클래스
    /// </summary>
    public class PostManager : MonoBehaviour
    {
        #region 우편함 UI 요소

        [Header("<color=green>우편함 기본 UI")]
        [SerializeField] private GameObject m_postBoxPanel;
        [SerializeField] private GameObject m_postboxContainer;
        [SerializeField] private PostIndex m_postboxPrefab;

        [Header("<color=green>우편함 상세 UI")]
        [SerializeField] private GameObject m_postBoxDetailPanel;
        [SerializeField] private TMP_Text m_postBoxDetailText;
        [SerializeField] private TMP_Text m_postBoxSenderNameText;
        [SerializeField] private TMP_Text m_rewardItemNameText;

        #endregion

        #region 데이터 필드

        /// <summary>
        /// 현재 선택된 아이템 코드
        /// </summary>
        private ServerManager.PostInfo m_currentPostInfo;

        #endregion

        #region Unity 라이프사이클

        private async void Start()
        {
            try
            {
                await InitializePostSystem();
            }
            catch (System.Exception ex)
            {
                LogManager.LogError($"우편 시스템 초기화 중 오류 발생: {ex.Message}", LogManager.LogCategory.PostManager);
            }
        }

        #endregion

        #region 초기화

        /// <summary>
        /// 우편 시스템 초기화
        /// </summary>
        private async UniTask InitializePostSystem()
        {
            // UI 초기 상태 설정
            if (m_postBoxPanel != null)
                m_postBoxPanel.SetActive(false);
            
            if (m_postBoxDetailPanel != null)
                m_postBoxDetailPanel.SetActive(false);
            
            // 서버에서 우편 목록 불러오기
            var postList = await ServerManager.Instance.GetPostListAsync(BackEnd.PostType.Admin);
            postList.AddRange(await ServerManager.Instance.GetPostListAsync(BackEnd.PostType.Coupon));
            
            // 기존 우편 목록 초기화
            foreach (Transform child in m_postboxContainer.transform)
            {
                Destroy(child.gameObject);
            }

            // 새 우편 목록 추가
            foreach (var postInfo in postList)
            {
                AddPostItem(postInfo);
            }
        }
        #endregion

        #region 우편함 UI 관리

        /// <summary>
        /// 우편함 메인 패널 열기
        /// </summary>
        public void OpenPostBoxPanel()
        {
            if (m_postBoxPanel == null)
            {
                LogManager.LogError("우편함 패널이 설정되지 않았습니다.", LogManager.LogCategory.PostManager);
                return;
            }

            m_postBoxPanel.SetActive(true);
            LobbyUIManager.AddClosePopUpAction(ClosePostBoxPanel);
            LogManager.Log("우편함 패널 열림", LogManager.LogCategory.PostManager);
        }

        /// <summary>
        /// 우편함 메인 패널 닫기
        /// </summary>
        private void ClosePostBoxPanel()
        {
            if (m_postBoxPanel == null) return;
            m_postBoxPanel.SetActive(false);
            LogManager.Log("우편함 패널 닫힘", LogManager.LogCategory.PostManager);
        }

        /// <summary>
        /// 우편 상세 패널 열기
        /// </summary>
        private void OpenPostDetailPanel(ServerManager.PostInfo postInfo)
        {
            if (m_postBoxDetailPanel == null)
            {
                LogManager.LogError("우편함 상세 패널이 설정되지 않았습니다.", LogManager.LogCategory.PostManager);
                return;
            }

            m_postBoxDetailPanel.SetActive(true);
            string rewardItemsString = GetRewardItemsString(postInfo.items);
            if (m_postBoxDetailText != null)
                m_postBoxDetailText.text = postInfo.content;
            if (m_postBoxSenderNameText != null)
                m_postBoxSenderNameText.text = postInfo.sender;
            if (m_rewardItemNameText != null)
                m_rewardItemNameText.text = rewardItemsString;
            m_currentPostInfo = postInfo;
            LobbyUIManager.AddClosePopUpAction(ClosePostDetailPanel);
            LogManager.Log($"우편 상세 열림: {postInfo.sender}로부터의 메시지", LogManager.LogCategory.PostManager);
        }

        /// <summary>
        /// 우편 상세 패널 닫기
        /// </summary>
        private void ClosePostDetailPanel()
        {
            if (m_postBoxDetailPanel == null) return;
            m_postBoxDetailPanel.SetActive(false);
            LogManager.Log("우편 상세 패널 닫힘", LogManager.LogCategory.PostManager);
        }

        #endregion

        #region 우편 데이터 관리
        
        /// <summary>
        /// 우편 아이템 추가 및 UI 이벤트 설정
        /// </summary>
        /// <param name="postInfo">서버에서 받아온 우편 정보</param>
        private void AddPostItem(ServerManager.PostInfo postInfo)
        {
            if (m_postboxPrefab == null || m_postboxContainer == null)
            {
                LogManager.LogError("우편함 프리팹 또는 컨테이너가 설정되지 않았습니다.", LogManager.LogCategory.PostManager);
                return;
            }

            PostIndex postIndex = Instantiate(m_postboxPrefab, m_postboxContainer.transform);
            if (postIndex == null)
            {
                LogManager.LogError("우편함 프리팹 생성 실패!", LogManager.LogCategory.PostManager);
                return;
            }

            // 이벤트 설정
            UnityEngine.Events.UnityEvent clickEvent = new UnityEngine.Events.UnityEvent();
            clickEvent.AddListener(() => OpenPostDetailPanel(postInfo));

            UnityEngine.Events.UnityEvent rewardEvent = new UnityEngine.Events.UnityEvent();
            rewardEvent.AddListener(() => ConfirmReward(postInfo, postIndex.gameObject));

            // 우편 인덱스 초기화
            postIndex.SetPostIndex(postInfo.sender, postInfo.title, postInfo.inDate, rewardEvent, clickEvent);
    
            LogManager.Log($"우편 추가됨: {postInfo.sender}로부터 {postInfo.inDate}에 받은 메시지, 보상: {GetRewardItemsString(postInfo.items)}", LogManager.LogCategory.PostManager);
        }

        /// <summary>
        /// 보상 수령 처리
        /// </summary>
        public void GetReward()
        {
            if (m_currentPostInfo.items == null || m_currentPostInfo.items.Count == 0)
            {
                LogManager.LogWarning("수령할 유효한 아이템이 없습니다.", LogManager.LogCategory.PostManager);
                return;
            }

            // 상세 패널에서 보상 수령 시, 해당 UI 오브젝트가 없으므로 null 전달
            ConfirmReward(m_currentPostInfo, null);
            LogManager.Log($"보상 수령: {GetRewardItemsString(m_currentPostInfo.items)}", LogManager.LogCategory.PostManager);
        }

        /// <summary>
        /// 보상 수령 확인 및 처리
        /// </summary>
        /// <param name="postInfo">수령할 우편 정보</param>
        /// <param name="postObject">파괴할 우편 UI 오브젝트 (선택 사항)</param>
        private async void ConfirmReward(ServerManager.PostInfo postInfo, GameObject postObject)
        {
            try
            {
                if (InventoryDataManagerDontdestory.Instance == null)
                {
                    LogManager.LogError("인벤토리 데이터 매니저가 설정되지 않았습니다.", LogManager.LogCategory.PostManager);
                    return;
                }

                bool isSuccess = await ServerManager.Instance.ReceivePostItemAsync(postInfo.postType, postInfo.postInDate);

                if (!isSuccess)
                {
                    LogManager.LogError("우편 수령에 실패했습니다.", LogManager.LogCategory.PostManager);
                    return;
                }

                foreach (var item in postInfo.items)
                {
                    //TODO: 현재는 아이템 이름으로 지급, 추후 아이템 코드로 변경 필요
                    InventoryDataManagerDontdestory.Instance.GetItemByName(item.Key, item.Value);
                    LogManager.Log($"아이템 '{item.Key}' {item.Value}개가 인벤토리에 추가되었습니다.", LogManager.LogCategory.PostManager);
                }

                // UI 오브젝트가 있으면 파괴하여 목록에서 제거
                if (postObject != null)
                {
                    Destroy(postObject);
                }
            }
            catch (System.Exception ex)
            {
                LogManager.LogError($"보상 수령 중 오류 발생: {ex.Message}", LogManager.LogCategory.PostManager);
                // 사용자에게 오류 팝업을 띄우는 등의 추가적인 처리를 할 수 있습니다.
            }
        }

        /// <summary>
        /// 보상 아이템 목록을 UI에 표시할 문자열로 변환합니다.
        /// </summary>
        /// <param name="items">아이템 목록</param>
        /// <returns>변환된 문자열</returns>
        private string GetRewardItemsString(Dictionary<string, int> items)
        {
            if (items == null || items.Count == 0)
            {
                return "보상 없음";
            }

            StringBuilder sb = new StringBuilder();
            foreach (var item in items)
            {
                sb.Append($"{item.Key} {item.Value}개, ");
            }
            return sb.ToString().TrimEnd(',', ' ');
        }

        #endregion
    }
}