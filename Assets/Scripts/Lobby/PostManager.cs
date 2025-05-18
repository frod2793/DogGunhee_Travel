using TMPro;
using UnityEngine;

namespace DogGuns_Games.Lobby
{
    /// <summary>
    /// 게임 내 우편 시스템을 관리하는 클래스
    /// </summary>
    public class PostManager : MonoBehaviour
    {
        #region 데이터 구조

        /// <summary>
        /// 우편 메시지 데이터 구조체
        /// </summary>
        private struct PostMessage
        {
            public string Message;
            public string Sender;
            public string Date;
            public int ItemCode;
            public string RewardItemName;
        }

        #endregion

        #region 우편함 UI 요소

        [Header("<color=green>우편함 기본 UI")]
        [SerializeField] private GameObject postBoxPanel;
        [SerializeField] private GameObject postboxContainer;
        [SerializeField] private PostIndex postboxPrefab;

        [Header("<color=green>우편함 상세 UI")]
        [SerializeField] private GameObject postBoxPanelExtension;
        [SerializeField] private TMP_Text postBoxPanelExtensionText;
        [SerializeField] private TMP_Text postBoxSendernameText;
        [SerializeField] private TMP_Text rewardItemNameText;

        #endregion

        #region 데이터 필드

        /// <summary>
        /// 현재 선택된 아이템 코드
        /// </summary>
        private int _currentItemCode;

        #endregion

        #region Unity 라이프사이클

        private void Start()
        {
            InitializePostSystem();
        }

        #endregion

        #region 초기화

        /// <summary>
        /// 우편 시스템 초기화
        /// </summary>
        private void InitializePostSystem()
        {
            // UI 초기 상태 설정
            if (postBoxPanel != null)
                postBoxPanel.SetActive(false);
            
            if (postBoxPanelExtension != null)
                postBoxPanelExtension.SetActive(false);
            
            // 테스트용 우편 생성
            AddPostSample();
        }

        /// <summary>
        /// 샘플 우편 데이터 추가 (개발/테스트용)
        /// </summary>
        private void AddPostSample()
        {
            // 샘플 우편 추가
            AddPostItem("게임 출시 기념 선물", "운영팀", "2023-11-01", "골드 1000개", 1001);
            AddPostItem("이벤트 보상", "이벤트팀", "2023-11-02", "다이아몬드 50개", 1002);
        }

        #endregion

        #region 우편함 UI 관리

        /// <summary>
        /// 우편함 메인 패널 열기
        /// </summary>
        public void OpenPostBoxPanel()
        {
            if (postBoxPanel == null)
            {
                Debug.LogError("우편함 패널이 설정되지 않았습니다.");
                return;
            }

            postBoxPanel.SetActive(true);
            LobbyUIManager.AddClosePopUpAction(ClosePostBoxPanel);
            Debug.Log("우편함 패널 열림");
        }

        /// <summary>
        /// 우편함 메인 패널 닫기
        /// </summary>
        private void ClosePostBoxPanel()
        {
            if (postBoxPanel == null) return;
            
            postBoxPanel.SetActive(false);
            Debug.Log("우편함 패널 닫힘");
        }

        /// <summary>
        /// 우편 상세 패널 열기
        /// </summary>
        private void OpenPostBoxPanel_Extension(string message, string sender, string rewardItemName, int itemCode)
        {
            if (postBoxPanelExtension == null)
            {
                Debug.LogError("우편함 상세 패널이 설정되지 않았습니다.");
                return;
            }

            // 상세 패널 UI 업데이트
            postBoxPanelExtension.SetActive(true);
            
            if (postBoxPanelExtensionText != null)
                postBoxPanelExtensionText.text = message;
            
            if (postBoxSendernameText != null)
                postBoxSendernameText.text = sender;
            
            if (rewardItemNameText != null)
                rewardItemNameText.text = rewardItemName;
            
            // 아이템 코드 저장 및 팝업 닫기 액션 등록
            _currentItemCode = itemCode;
            LobbyUIManager.AddClosePopUpAction(ClosePostBoxPanel_Extension);
            
            Debug.Log($"우편 상세 열림: {sender}로부터의 메시지, 아이템 코드: {itemCode}");
        }

        /// <summary>
        /// 우편 상세 패널 닫기
        /// </summary>
        private void ClosePostBoxPanel_Extension()
        {
            if (postBoxPanelExtension == null) return;
            
            postBoxPanelExtension.SetActive(false);
            Debug.Log("우편 상세 패널 닫힘");
        }

        #endregion

        #region 우편 데이터 관리
        
        /// <summary>
        /// 우편 아이템 추가 및 UI 이벤트 설정
        /// </summary>
        /// <param name="message">우편 내용</param>
        /// <param name="sender">발신자</param>
        /// <param name="date">발송 날짜</param>
        /// <param name="rewardItemName">보상 아이템 이름</param>
        /// <param name="itemCode">보상 아이템 코드</param>
        private void AddPostItem(string message, string sender, string date, string rewardItemName, int itemCode)
        {
            // 필수 컴포넌트 확인
            if (postboxPrefab == null || postboxContainer == null)
            {
                Debug.LogError("우편함 프리팹 또는 컨테이너가 설정되지 않았습니다.");
                return;
            }

            // 우편 인덱스 생성
            PostIndex postIndex = Instantiate(postboxPrefab, postboxContainer.transform);
            if (postIndex == null)
            {
                Debug.LogError("우편함 프리팹 생성 실패!");
                return;
            }

            // 이벤트 설정
            UnityEngine.Events.UnityEvent clickEvent = new UnityEngine.Events.UnityEvent();
            clickEvent.AddListener(() => OpenPostBoxPanel_Extension(message, sender, rewardItemName, itemCode));

            UnityEngine.Events.UnityEvent rewardEvent = new UnityEngine.Events.UnityEvent();
            rewardEvent.AddListener(() => ConfiromReword(itemCode));

            // 우편 인덱스 초기화
            postIndex.SetPostIndex(sender, message, date, rewardEvent, clickEvent);
    
            Debug.Log($"우편 추가됨: {sender}로부터 {date}에 받은 메시지, 보상: {rewardItemName}");
        }

        /// <summary>
        /// 보상 수령 처리
        /// </summary>
        public void Getreward()
        {
            if (_currentItemCode <= 0)
            {
                Debug.LogWarning("유효하지 않은 아이템 코드입니다.");
                return;
            }

            ConfiromReword(_currentItemCode);
            Debug.Log($"보상 수령: 아이템 코드 {_currentItemCode}");
        }

        /// <summary>
        /// 보상 수령 확인 및 처리
        /// </summary>
        /// <param name="itemCode">수령할 아이템 코드</param>
        private void ConfiromReword(int itemCode)
        {
            if (InventoryDataManagerDontdestory.Instance == null)
            {
                Debug.LogError("인벤토리 데이터 매니저가 설정되지 않았습니다.");
                return;
            }

            InventoryDataManagerDontdestory.Instance.GetItemByItemCode(itemCode);
            Debug.Log($"아이템 코드 {itemCode} 아이템이 인벤토리에 추가되었습니다.");
        }

        #endregion
    }
}