using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace InGame
{
    /// <summary>
    /// 단순 안내 메시지나 알림창 표시를 관리하는 클래스입니다.
    /// <br/>텍스트와 배경의 투명도를 이용한 애니메이션 효과를 담당합니다.
    /// </summary>
    public class MessageManager : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [SerializeField, Tooltip("메시지를 표시할 캔버스 오브젝트"), FormerlySerializedAs("messageCanvas")]
        private Canvas m_messageCanvas;

        [SerializeField, Tooltip("메시지 배경 이미지"), FormerlySerializedAs("message_BG")]
        private Image m_messageBackground;

        [SerializeField, Tooltip("메시지 내용 텍스트"), FormerlySerializedAs("message_Text")]
        private Text m_messageText;

        #endregion

        #region 2. 내부 변수

        private Coroutine m_fadeCoroutine;

        #endregion

        #region 3. 공개 메서드

        /// <summary>
        /// 구상 중인 내용이거나 빈 항목인 경우 안내 메시지를 출력합니다.
        /// </summary>
        public void OnEmptyGameMessage()
        {
            if (m_messageText != null)
            {
                m_messageText.text = "구상 중인 항목입니다.";
            }

            ShowMessage();
        }

        /// <summary>
        /// 로비 씬으로 되돌아가는 기능을 수행합니다.
        /// </summary>
        public void Func_Continue()
        {
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene("LobbyScene");
            }
        }

        #endregion

        #region 4. 메시지 연출 로직

        /// <summary>
        /// 메시지 창을 활성화하고 페이드 아웃 연출을 시작합니다.
        /// </summary>
        private void ShowMessage()
        {
            if (m_messageCanvas == null || m_messageBackground == null || m_messageText == null)
            {
                return;
            }

            m_messageCanvas.gameObject.SetActive(true);

            // 초기 불투명 상태 설정
            m_messageBackground.color = Color.black;
            m_messageText.color = Color.white;

            // 진행 중인 연출이 있다면 중지 후 새로 시작
            if (m_fadeCoroutine != null)
            {
                StopCoroutine(m_fadeCoroutine);
            }

            m_fadeCoroutine = StartCoroutine(FadeOutCoroutine());
        }

        /// <summary>
        /// 시간이 지남에 따라 배경과 텍스트의 투명도를 낮추어 사라지게 합니다.
        /// </summary>
        private IEnumerator FadeOutCoroutine()
        {
            float fadeCount = 1f;

            while (fadeCount > 0.0f)
            {
                fadeCount -= 0.01f;
                yield return new WaitForSeconds(0.01f);

                if (m_messageBackground != null)
                {
                    m_messageBackground.color = new Color(0, 0, 0, fadeCount);
                }

                if (m_messageText != null)
                {
                    m_messageText.color = new Color(1, 1, 1, fadeCount);
                }
            }

            // 완전히 사라진 후 정리
            if (m_messageCanvas != null)
            {
                m_messageCanvas.gameObject.SetActive(false);
            }

            m_fadeCoroutine = null;
        }

        #endregion
    }
}