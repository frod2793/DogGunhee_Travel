using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DogGuns_Games.Lobby
{
    public class PostIndex : MonoBehaviour
    {
        [FormerlySerializedAs("postprofile")]
        [SerializeField] private Image m_senderProfileImage;
        [FormerlySerializedAs("itemprofile")]
        [SerializeField] private Image m_itemProfileImage;
        [FormerlySerializedAs("postname")]
        [SerializeField] private TMP_Text m_senderNameText;
        [FormerlySerializedAs("posttitle")]
        [SerializeField] private TMP_Text m_titleText;
        [FormerlySerializedAs("postdate")]
        [SerializeField] private TMP_Text m_dateText;
        [FormerlySerializedAs("getReiwordbutton")]
        [SerializeField] private Button m_getRewardButton;
        [FormerlySerializedAs("postexpendbutton")]
        [SerializeField] private Button m_detailButton;

        /// <summary>
        /// 우편 목록의 UI 요소를 초기화하고 이벤트를 설정합니다.
        /// </summary>
        /// <param name="senderName">보낸 사람 이름</param>
        /// <param name="title">우편 제목</param>
        /// <param name="date">받은 날짜</param>
        /// <param name="getRewardAction">보상 받기 버튼에 연결할 이벤트</param>
        /// <param name="openDetailAction">상세 보기 버튼에 연결할 이벤트</param>
        public void SetPostIndex(string senderName, string title, string date, UnityEvent getRewardAction, UnityEvent openDetailAction)
        {
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
    }
}