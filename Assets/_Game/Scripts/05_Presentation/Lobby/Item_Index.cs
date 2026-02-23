using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace InGame.Lobby
{
    /// <summary>
    /// [설명]: 아이템 선택 목록의 개별 항목을 관리하는 클래스입니다.
    /// </summary>
    public class Item_Index : MonoBehaviour
    {
        #region 에디터 설정 (Inspector)

        [SerializeField, Tooltip("캐릭터 선택 버튼"), FormerlySerializedAs("openCharacterSelectButton")]
        private Button m_openCharacterSelectButton;

        [SerializeField, Tooltip("아이템 썸네일 이미지"), FormerlySerializedAs("thumbNail")]
        private Image m_thumbNail;

        [SerializeField, Tooltip("아이템/캐릭터 이름 텍스트"), FormerlySerializedAs("characterName")]
        private TMP_Text m_characterName;

        #endregion

        #region 프로퍼티

        /// <summary> [설명]: 캐릭터 선택 창을 여는 버튼 </summary>
        public Button openCharacterSelectButton => m_openCharacterSelectButton;

        /// <summary> [설명]: 아이템의 썸네일 이미지 </summary>
        public Image thumbNail => m_thumbNail;

        /// <summary> [설명]: 캐릭터 혹은 아이템의 이름 텍스트 </summary>
        public TMP_Text characterName => m_characterName;

        #endregion
    }
}