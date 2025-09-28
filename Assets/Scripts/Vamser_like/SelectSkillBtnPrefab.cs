using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace DogGuns_Games.vamsir // 네임스페이스를 다른 파일과 일치시킵니다.
{
    /// <summary>
    /// 스킬 선택 UI 버튼의 구성 요소를 관리하는 프리팹 스크립트입니다.
    /// </summary>
    public class SelectSkillBtnPrefab : MonoBehaviour
    {
        [Header("UI 구성 요소")]
        [Tooltip("스킬의 썸네일 이미지를 표시합니다.")]
        [SerializeField] private Image skillThumbnailImage;
        [Tooltip("스킬의 이름을 표시하는 텍스트입니다.")]
        [SerializeField] private TMP_Text skillNameText;
        [Tooltip("스킬의 상세 설명을 표시하는 텍스트입니다.")]
        [SerializeField] private TMP_Text skillDescriptionText;

        private SkillData _currentSkillData;
        private Button _button;
        private Action<SkillData> _onSelectedCallback;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnSkillSelected);
        }

        /// <summary>
        /// SkillData를 기반으로 버튼의 UI를 설정합니다.
        /// </summary>
        /// <param name="skillData">표시할 스킬 데이터</param>
        /// <param name="onSelectedCallback">버튼 클릭 시 호출될 콜백</param>
        public void Setup(SkillData skillData, Action<SkillData> onSelectedCallback)
        {
            _currentSkillData = skillData;
            _onSelectedCallback = onSelectedCallback;

            skillThumbnailImage.sprite = _currentSkillData.skillIcon;
            skillNameText.text = _currentSkillData.skillName;
            skillDescriptionText.text = _currentSkillData.skillDescription;
        }

        /// <summary>
        /// 이 버튼이 클릭되었을 때 호출됩니다.
        /// </summary>
        private void OnSkillSelected()
        {
            if (_currentSkillData == null) return;

            LogManager.Log($"스킬 선택: {_currentSkillData.skillName} (코드: {_currentSkillData.skillCode})", LogManager.LogCategory.VamserLikeUI);

            // VamserLikeUI에 선택 결과를 알립니다.
            _onSelectedCallback?.Invoke(_currentSkillData);
        }
    }
}