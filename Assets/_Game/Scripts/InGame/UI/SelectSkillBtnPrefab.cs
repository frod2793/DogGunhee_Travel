using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
namespace InGame.vamsir // 네임스페이스를 다른 파일과 일치시킵니다.
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
        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        private void Awake()
        {
            _button = GetComponent<Button>();
            
            // R3의 SubscribeAwait를 사용하여 애니메이션이 끝난 후 콜백을 호출합니다.
            _button.OnClickAsObservable()
                .Where(_ => _currentSkillData != null) // 스킬 데이터가 할당된 경우에만 진행
                .SubscribeAwait(async (_, ct) =>
                {
                    // 버튼이 비활성화되거나 파괴될 때 작업이 안전하게 취소되도록 CancellationToken을 전달합니다.
                    await PlaySelectionAnimation(ct);

                    LogManager.Log($"스킬 선택: {_currentSkillData.skillName} (코드: {_currentSkillData.skillCode})", LogManager.LogCategory.VamserLikeUI);
                    _onSelectedCallback?.Invoke(_currentSkillData);
                })
                .AddTo(_disposables);
        }

        private void OnDestroy()
        {
            // DOTween 애니메이션과 R3 구독을 안전하게 정리합니다.
            transform.DOKill();
            _disposables.Dispose();
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
        /// 이 버튼이 선택되었음을 시각적으로 보여주는 애니메이션을 재생합니다.
        /// </summary>
        public async UniTask PlaySelectionAnimation(CancellationToken cancellationToken = default)
        {
            // SetUpdate(true)를 사용하여 Time.timeScale = 0 상태에서도 애니메이션이 동작하도록 합니다.
            await transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true).WithCancellation(cancellationToken);
            await transform.DOScale(1.0f, 0.1f).SetEase(Ease.InQuad).SetUpdate(true).WithCancellation(cancellationToken);
        }

        /// <summary>
        /// 이 버튼에 설정된 현재 SkillData를 반환합니다.
        /// </summary>
        public SkillData GetCurrentSkillData()
        {
            return _currentSkillData;
        }
    }
}
