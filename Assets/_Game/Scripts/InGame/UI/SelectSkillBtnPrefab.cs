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
    /// 스킬 선택 팝업 내 개별 스킬 버튼을 관리하는 프리팹 클래스입니다.
    /// <br/> 데이터 바인딩, 클릭 이벤트 처리(R3), 선택 애니메이션(DOTween)을 담당합니다.
    /// </summary>
    public class SelectSkillBtnPrefab : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("UI 구성 요소")]
        [SerializeField, Tooltip("스킬 아이콘을 표시할 이미지")] 
        private Image m_skillThumbnailImage;
        
        [SerializeField, Tooltip("스킬 이름을 표시할 텍스트")] 
        private TMP_Text m_skillNameText;
        
        [SerializeField, Tooltip("스킬 상세 설명을 표시할 텍스트")] 
        private TMP_Text m_skillDescriptionText;

        #endregion

        #region 2. 내부 변수 및 상태
        // 데이터
        private SkillData m_currentSkillData;
        
        // 컴포넌트 캐싱
        private Button m_button;
        
        // 콜백
        private Action<SkillData> m_onSelectedCallback;
        
        // 리소스 관리
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();
        #endregion

        #region 3. 유니티 생명주기
        private void Awake()
        {
            InitializeButton();
        }

        private void OnDestroy()
        {
            // DOTween 애니메이션 중단 
            transform.DOKill();
            
            // R3 구독 해제
            m_disposables.Dispose();
        }
        #endregion

        #region 4. 초기화 및 설정
        /// <summary>
        /// 버튼 컴포넌트를 가져오고 클릭 이벤트를 구독합니다.
        /// </summary>
        private void InitializeButton()
        {
            m_button = GetComponent<Button>();
            if (m_button == null) return;

            m_button.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(0.5f)) // 중복 클릭 방지
                .Where(_ => m_currentSkillData != null)      // 데이터가 있을 때만 반응
                .SubscribeAwait(async (_, ct) =>
                {
                    // 1. 선택 애니메이션 재생 
                    await PlaySelectionAnimation(ct);

                    // 2. 로그 및 콜백 호출
                    LogManager.Log($"스킬 선택됨: {m_currentSkillData.skillName} (ID: {m_currentSkillData.skillCode})", LogManager.LogCategory.VamserLikeUI);
                    m_onSelectedCallback?.Invoke(m_currentSkillData);
                })
                .AddTo(m_disposables);
        }

        /// <summary>
        /// SkillData를 기반으로 버튼의 UI(이미지, 텍스트)를 갱신하고 콜백을 설정합니다.
        /// </summary>
        /// <param name="skillData">표시할 스킬 데이터</param>
        /// <param name="onSelectedCallback">버튼 클릭 시 호출될 외부 콜백</param>
        public void Setup(SkillData skillData, Action<SkillData> onSelectedCallback)
        {
            m_currentSkillData = skillData;
            m_onSelectedCallback = onSelectedCallback;

            // UI 갱신 (Null 안전성 체크 포함)
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

        #region 5. 내부 로직 및 애니메이션
        /// <summary>
        /// 버튼 선택 시 시각적 피드백(확대/축소) 애니메이션을 재생합니다.
        /// </summary>
        public async UniTask PlaySelectionAnimation(CancellationToken cancellationToken = default)
        {
            // Time.timeScale = 0 (일시정지) 상태에서도 애니메이션이 작동하도록 SetUpdate(true) 설정
            // 1. 확대 
            await transform.DOScale(1.2f, 0.2f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .WithCancellation(cancellationToken);

            // 2. 복귀 
            await transform.DOScale(1.0f, 0.1f)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .WithCancellation(cancellationToken);
        }

        /// <summary>
        /// 현재 할당된 스킬 데이터를 반환합니다. (View에서 상태 확인용)
        /// </summary>
        public SkillData GetCurrentSkillData()
        {
            return m_currentSkillData;
        }
        #endregion
    }
}