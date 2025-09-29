using System;
using Cysharp.Threading.Tasks;
using TMPro;
using DG.Tweening;
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

        /// <summary>
        /// 외부(타이머 등)에서 이 버튼의 선택 로직을 호출하기 위한 메서드입니다.
        /// </summary>
        [Obsolete("Use PlaySelectionAnimation and TriggerSelectionCallback instead.")]
        public void InvokeSelection()
        {
            OnSkillSelected();
        }

        /// <summary>
        /// 이 버튼이 선택되었음을 시각적으로 보여주는 애니메이션을 재생합니다.
        /// </summary>
        public async UniTask PlaySelectionAnimation()
        {
            // DOTween을 사용하여 버튼이 커졌다가 원래 크기로 돌아오는 애니메이션을 비동기적으로 실행합니다.
            await transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutQuad).AsyncWaitForCompletion();
            await transform.DOScale(1.0f, 0.1f).SetEase(Ease.InQuad).AsyncWaitForCompletion();
        }

        /// <summary>
        /// VamserLikeUI에 설정된 선택 콜백을 호출합니다.
        /// </summary>
        public void TriggerSelectionCallback()
        {
            // OnSkillSelected와 동일한 로직이지만, 이름으로 역할을 명확히 합니다.
            _onSelectedCallback?.Invoke(_currentSkillData);
        }
    }
}