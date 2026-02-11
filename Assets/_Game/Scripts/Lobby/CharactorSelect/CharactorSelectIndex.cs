using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace InGame.Lobby
{
    /// <summary>
    /// 캐릭터 선택 목록의 개별 항목을 관리하는 클래스입니다.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, null, null, "CharactorSelectIndex")]
    public class CharacterSelectIndex : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [SerializeField, Tooltip("캐릭터 선택 버튼"), FormerlySerializedAs("openCharacterSelectButton")]
        private Button m_openCharacterSelectButton;

        [SerializeField, Tooltip("캐릭터 썸네일 이미지"), FormerlySerializedAs("thumbNail")]
        private Image m_thumbNail;

        [SerializeField, Tooltip("캐릭터 이름 텍스트"), FormerlySerializedAs("characterName")]
        private TMP_Text m_characterName;

        #endregion

        #region 2. 프로퍼티 (공개 필드 대체)

        public Button openCharacterSelectButton => m_openCharacterSelectButton;
        public Image thumbNail => m_thumbNail;
        public TMP_Text characterName => m_characterName;

        #endregion
    }
}