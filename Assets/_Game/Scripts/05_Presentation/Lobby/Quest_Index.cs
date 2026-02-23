using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace InGame.Lobby
{
    /// <summary>
    /// [설명]: 로비 퀘스트 리스트에 표시되는 개별 퀘스트 항목을 관리하는 클래스입니다.
    /// </summary>
    public class Quest_Index : MonoBehaviour
    {
        #region 에디터 설정

        [SerializeField, Tooltip("퀘스트 완료/진행 토글"), FormerlySerializedAs("questToggle")]
        private Toggle m_questToggle;

        [SerializeField, Tooltip("퀘스트 이름 표시용 텍스트"), FormerlySerializedAs("questName")]
        private TMP_Text m_questName;

        [SerializeField, Tooltip("퀘스트 상세 버튼"), FormerlySerializedAs("questButton")]
        private Button m_questButton;

        #endregion

        #region 프로퍼티

        /// <summary>
        /// [설명]: 외부에서 클릭 이벤트를 등록하기 위한 퀘스트 버튼 참조입니다.
        /// </summary>
        public Button QuestButton => m_questButton;

        #endregion

        #region 정보 설정

        /// <summary>
        /// [설명]: 퀘스트의 이름을 표시하고 초기화합니다.
        /// </summary>
        /// <param name="name">퀘스트 명칭</param>
        public void SetQuestIndex(string name)
        {
            if (m_questName != null)
            {
                m_questName.text = name;
            }
        }

        #endregion
    }
}