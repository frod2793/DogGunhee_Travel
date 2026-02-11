using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace InGame.Lobby
{
    /// <summary>
    /// 로비의 우편 목록 팝업 내 개별 우편 항목을 표시하고 제어하는 클래스입니다.
    /// </summary>
    public class PostIndex : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("<color=green>이미지 설정</color>")]
        [SerializeField, Tooltip("보낸 사람 프로필 이미지"), FormerlySerializedAs("postprofile")]
        private Image m_senderProfileImage;

        [SerializeField, Tooltip("아이템 미리보기 이미지"), FormerlySerializedAs("itemprofile")]
        private Image m_itemProfileImage;

        [Header("<color=green>텍스트 설정</color>")]
        [SerializeField, Tooltip("발신자 이름 텍스트"), FormerlySerializedAs("postname")]
        private TMP_Text m_senderNameText;

        [SerializeField, Tooltip("우편 제목 텍스트"), FormerlySerializedAs("posttitle")]
        private TMP_Text m_titleText;

        [SerializeField, Tooltip("수신 날짜 텍스트"), FormerlySerializedAs("postdate")]
        private TMP_Text m_dateText;

        [Header("<color=green>버튼 설정</color>")]
        [SerializeField, Tooltip("보상 획득 버튼"), FormerlySerializedAs("getReiwordbutton")]
        private Button m_getRewardButton;

        [SerializeField, Tooltip("우편 상세보기 버튼"), FormerlySerializedAs("postexpendbutton")]
        private Button m_detailButton;

        #endregion

        #region 2. 데이터 초기화

        /// <summary>
        /// 제공된 우편 정보를 기반으로 UI를 갱신하고 버튼 이벤트를 연결합니다.
        /// </summary>
        /// <param name="senderName">발신자 명칭</param>
        /// <param name="title">우편 제목</param>
        /// <param name="date">수신 일자</param>
        /// <param name="getRewardAction">보상 획득 시 실행할 이벤트</param>
        /// <param name="openDetailAction">상세 보기 클릭 시 실행할 이벤트</param>
        public void SetPostIndex(string senderName, string title, string date, UnityEvent getRewardAction,
            UnityEvent openDetailAction)
        {
            // 텍스트 정보 반영
            if (m_senderNameText != null)
            {
                m_senderNameText.SetText(senderName);
            }

            if (m_titleText != null)
            {
                m_titleText.SetText(title);
            }

            if (m_dateText != null)
            {
                m_dateText.SetText(date);
            }

            // 버튼 리스너 초기화 및 재등록
            if (m_getRewardButton != null)
            {
                m_getRewardButton.onClick.RemoveAllListeners();
                m_getRewardButton.onClick.AddListener(getRewardAction.Invoke);
            }

            if (m_detailButton != null)
            {
                m_detailButton.onClick.RemoveAllListeners();
                m_detailButton.onClick.AddListener(openDetailAction.Invoke);
            }
        }

        #endregion
    }
}