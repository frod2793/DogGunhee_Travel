using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace InGame.Lobby
{
    /// <summary>
    /// [설명]: 캐릭터 선택 목록의 개별 항목을 관리하는 클래스입니다.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, null, null, "CharactorSelectIndex")]
    public class CharacterSelectIndex : MonoBehaviour
    {
        #region 에디터 설정

        [SerializeField, Tooltip("캐릭터 선택 버튼"), FormerlySerializedAs("openCharacterSelectButton")]
        private Button m_openCharacterSelectButton;

        [SerializeField, Tooltip("캐릭터 썸네일 이미지"), FormerlySerializedAs("thumbNail")]
        private Image m_thumbNail;

        [SerializeField, Tooltip("캐릭터 이름 텍스트"), FormerlySerializedAs("characterName")]
        private TMP_Text m_characterName;

        #endregion

        #region 공개 프로퍼티

        /// <summary>
        /// [설명]: 캐릭터 선택 버튼 참조
        /// </summary>
        public Button OpenCharacterSelectButton => m_openCharacterSelectButton;

        /// <summary>
        /// [설명]: 캐릭터 썸네일 이미지 참조
        /// </summary>
        public Image ThumbNail => m_thumbNail;

        /// <summary>
        /// [설명]: 캐릭터 이름 텍스트 참조
        /// </summary>
        public TMP_Text CharacterName => m_characterName;

        #endregion
    }
}
