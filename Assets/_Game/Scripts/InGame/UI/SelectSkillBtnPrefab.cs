using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace InGame.vamsir
{
    /// <summary>
    /// [설명]: 스킬 선택 팝업 내 개별 스킬 정보를 시각화하는 위젯 프리팹 클래스입니다.
    /// 스킬의 아이콘, 명칭, 설명 등을 바인딩하며 R3 기반의 반응형 클릭 이벤트 및 DOTween 애니메이션 처리를 담당합니다.
    /// </summary>
    public class SelectSkillBtnPrefab : MonoBehaviour
    {
        #region 에디터 설정

        [Header("UI 구성 요소")]
        [SerializeField, Tooltip("스킬의 고유 외형을 표시할 썸네일 이미지")]
        private Image m_skillThumbnailImage;

        [SerializeField, Tooltip("스킬의 명칭을 표시할 텍스트 컴포넌트")]
        private TMP_Text m_skillNameText;

        [SerializeField, Tooltip("스킬의 성능이나 효과를 설명하는 텍스트 컴포넌트")]
        private TMP_Text m_skillDescriptionText;

        #endregion

        #region 내부 필드

        /// <summary> 현재 버튼에 할당된 원본 스킬 데이터 정보 </summary>
        private SkillData m_currentSkillData;

        /// <summary> 클릭 상호작용을 담당하는 Button 컴포넌트 캐시 </summary>
        private Button m_button;

        /// <summary> 스킬이 최종 선택되었을 때 실행될 외부 로직 콜백 대리자 </summary>
        private Action<SkillData> m_onSelectedCallback;

        /// <summary> R3 이벤트 구독을 일괄 관리하여 메모리 누수를 방지하는 디스포저블 </summary>
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        #endregion

        #region 유니티 생명주기

        /// <summary>
        /// [설명]: 초기 기동 시 버튼의 참조를 확보하고 이벤트 루틴을 기동합니다.
        /// </summary>
        private void Awake()
        {
            InitializeButton();
        }

        /// <summary>
        /// [설명]: 객체 파기 시 모든 트윈 애니메이션을 중단하고 R3 구독 관계를 안전하게 해제합니다.
        /// </summary>
        private void OnDestroy()
        {
            // DOTween 애니메이션 즉시 중단 
            transform.DOKill();

            // R3 구독 자동 해제
            m_disposables.Dispose();
        }

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 버튼 컴포넌트를 초기화하고 클릭 스트림을 구축합니다. 중복 클릭 방지 처리를 포함합니다.
        /// </summary>
        private void InitializeButton()
        {
            m_button = GetComponent<Button>();
            if (m_button == null)
            {
                return;
            }

            m_button.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(0.5f)) // 과도한 반복 클릭 차단
                .Where(_ => m_currentSkillData != null)      // 유효 데이터 존재 시에만 동작
                .SubscribeAwait(async (_, ct) =>
                {
                    // 1. 선택 시각 피드백 애니메이션 재생 
                    await PlaySelectionAnimation(ct);

                    // 2. 로그 기록 및 전달된 콜백 호출
                    LogManager.Log($"스킬 선택됨: {m_currentSkillData.skillName} (ID: {m_currentSkillData.skillCode})", LogManager.LogCategory.VamserLikeUI);
                    
                    if (m_onSelectedCallback != null)
                    {
                        m_onSelectedCallback.Invoke(m_currentSkillData);
                    }
                })
                .AddTo(m_disposables);
        }

        /// <summary>
        /// [설명]: 외부에서 전달된 스킬 데이터를 UI 요소들에 바인딩하고 선택 시 콜백을 설정합니다.
        /// </summary>
        /// <param name="skillData">버튼에 표시할 스킬 원본 정보</param>
        /// <param name="onSelectedCallback">선택 완료 시 통지받을 대리자</param>
        public void Setup(SkillData skillData, Action<SkillData> onSelectedCallback)
        {
            m_currentSkillData = skillData;
            m_onSelectedCallback = onSelectedCallback;

            // UI 구성 요소 갱신 (Unity Native Null Check 활용)
            if (m_skillThumbnailImage != null)
            {
                m_skillThumbnailImage.sprite = m_currentSkillData.skillIcon;
            }

            if (m_skillNameText != null)
            {
                m_skillNameText.text = m_currentSkillData.skillName;
            }

            if (m_skillDescriptionText != null)
            {
                m_skillDescriptionText.text = m_currentSkillData.skillDescription;
            }
        }

        #endregion

        #region 내부 비즈니스 로직 및 연출

        /// <summary>
        /// [설명]: 버튼이 선택되었을 때 크기가 확대되었다가 복귀하는 시각적 피드백 트윈을 재생합니다.
        /// 일시정지 상태(TimeScale=0)에서도 정상 작동하도록 처리되어 있습니다.
        /// </summary>
        public async UniTask PlaySelectionAnimation(CancellationToken cancellationToken = default)
        {
            // 1. 역동적인 확대 효과 (Independent Update 설정으로 일시정지 무시)
            await transform.DOScale(1.2f, 0.2f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .WithCancellation(cancellationToken);

            // 2. 기본 크기로 부드러운 복귀 
            await transform.DOScale(1.0f, 0.1f)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .WithCancellation(cancellationToken);
        }

        /// <summary>
        /// [설명]: 현재 위젯에 바인딩되어 있는 스킬 데이터를 반환합니다.
        /// </summary>
        public SkillData GetCurrentSkillData()
        {
            return m_currentSkillData;
        }

        #endregion
    }
}