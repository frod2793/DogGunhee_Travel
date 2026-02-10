using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using R3;
using DG.Tweening;
using InGame.UI.ViewModels;

namespace InGame.UI.Views
{
    /// <summary>
    /// 인게임 HUD(상단 정보, 플레이어 상태, 인벤토리)를 전담하는 View 클래스입니다.
    /// <br/> ViewModel의 데이터를 구독하여 UI를 갱신하고, DOTween을 사용한 연출을 담당합니다.
    /// </summary>
    public class InGameHUDView : MonoBehaviour
    {
        #region 1. 에디터 설정 (Inspector)

        [Header("1. 플레이어 상태 (상단/중앙)")] 
        [SerializeField, Tooltip("플레이어 레벨 텍스트 (상단)")]
        private TMP_Text m_playerLevelText;

        [SerializeField, Tooltip("플레이어 경험치 슬라이더 (상단)")] 
        private Slider m_playerLevelSlider;
        
        [SerializeField, Tooltip("인게임 캐릭터 하단 레벨 텍스트")] 
        private TMP_Text m_playerLevelText_InGame;
        
        [SerializeField, Tooltip("추가 경험치 슬라이더 (옵션)")] 
        private Slider m_expSlider;

        [Header("2. 게임 정보 (HUD)")] 
        [SerializeField, Tooltip("웨이브 정보 텍스트 (중앙 알림용)")]
        private TMP_Text m_mobWaveText;

        [SerializeField, Tooltip("획득 코인 표시 텍스트")] 
        private TMP_Text m_coinText;
        
        [SerializeField, Tooltip("처치 수 표시 텍스트")] 
        private TMP_Text m_mobCountText;

        [Header("3. 인벤토리 아이콘")] 
        [SerializeField, Tooltip("무기 아이콘 UI 리스트")]
        private List<Image> m_weaponUIList = new List<Image>();

        [SerializeField, Tooltip("장신구 아이콘 UI 리스트")] 
        private List<Image> m_accessoryUIList = new List<Image>();

        #endregion

        #region 2. 내부 변수 및 상태 관리
        // ViewModel 참조
        private InGameViewModel m_viewModel;
        
        // R3 구독 관리자
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();
        
        // DOTween 참조 (중복 실행 방지용)
        private Tween m_expTween;
        private Sequence m_waveSequence;
        #endregion

        #region 3. 유니티 생명주기
        private void OnDestroy()
        {
            // 1. 리액티브 구독 해제
            m_disposables.Dispose();
            
            // 2. 실행 중인 트윈/시퀀스 종료 (메모리 누수 및 에러 방지)
            if (m_expTween != null && m_expTween.IsActive()) m_expTween.Kill();
            if (m_waveSequence != null && m_waveSequence.IsActive()) m_waveSequence.Kill();
        }
        #endregion

        #region 4. 초기화 및 바인딩
        /// <summary>
        /// ViewModel과 View를 연결합니다. 기존 구독을 정리하고 새로운 데이터 흐름을 연결합니다.
        /// </summary>
        public void Bind(InGameViewModel viewModel)
        {
            m_viewModel = viewModel;
            m_disposables.Clear();

            BindPlayerStatus();
            BindGameInfo();
            BindInventory();
        }

        private void BindPlayerStatus()
        {
            // 레벨 표시
            m_viewModel.PlayerLevel
                .Subscribe(level => UpdateLevelText(level))
                .AddTo(m_disposables);

            // 경험치 바 (애니메이션 포함)
            m_viewModel.ExpProgress
                .Subscribe(progress => UpdateExpUI(progress))
                .AddTo(m_disposables);

            // 레벨업 효과
            m_viewModel.OnLevelUp
                .Subscribe(_ => PlayLevelUpEffect())
                .AddTo(m_disposables);
        }

        private void BindGameInfo()
        {
            // 웨이브 시작 알림
            m_viewModel.OnWaveStarted
                .CombineLatest(m_viewModel.CurrentStageId, (waveData, stage) => (waveData, stage))
                .Subscribe(data => ShowWaveInfo($"Stage {data.stage} - {data.waveData.waveName}"))
                .AddTo(m_disposables);

            // 웨이브 종료 알림
            m_viewModel.OnWaveCompleted
                .CombineLatest(m_viewModel.CurrentStageId, (waveData, stage) => (waveData, stage))
                .Subscribe(data => ShowWaveInfo($"Stage {data.stage} - {data.waveData.waveName} 클리어"))
                .AddTo(m_disposables);

            // 재화 및 킬 수
            m_viewModel.CoinCount
                .Subscribe(coin => 
                {
                    if (m_coinText != null) m_coinText.SetSafeText("{0}", coin);
                })
                .AddTo(m_disposables);

            m_viewModel.KillCount
                .Subscribe(kills => 
                {
                    if (m_mobCountText != null) m_mobCountText.SetSafeText("{0}", kills);
                })
                .AddTo(m_disposables);
        }

        private void BindInventory()
        {
            // 무기 슬롯 갱신
            m_viewModel.WeaponSprites
                .Subscribe(sprites => RefreshIcons(m_weaponUIList, sprites))
                .AddTo(m_disposables);

            // 장신구 슬롯 갱신
            m_viewModel.AccessorySprites
                .Subscribe(sprites => RefreshIcons(m_accessoryUIList, sprites))
                .AddTo(m_disposables);
        }
        #endregion

        #region 5. UI 업데이트 및 연출 로직

        /// <summary>
        /// 플레이어 레벨 텍스트를 갱신합니다.
        /// </summary>
        private void UpdateLevelText(int level)
        {
            if (m_playerLevelText != null)
                m_playerLevelText.SetSafeText("Lv. {0}", level);
            
            if (m_playerLevelText_InGame != null)
                m_playerLevelText_InGame.SetSafeText("Lv. {0}", level);
        }

        /// <summary>
        /// 경험치 슬라이더를 부드럽게 갱신합니다.
        /// </summary>
        private void UpdateExpUI(float progress)
        {
            // 기존 트윈 제거 (중복 실행 방지)
            if (m_expTween != null && m_expTween.IsActive())
                m_expTween.Kill();

            if (m_playerLevelSlider != null)
            {
                m_expTween = m_playerLevelSlider.DOValue(progress, 0.2f).SetEase(Ease.OutQuad);
            }

            if (m_expSlider != null)
            {
                // 보조 슬라이더도 동일하게 처리 (별도 트윈 변수가 없다면 즉시 적용하거나 별도 관리 필요)
                // 여기서는 DOValue를 사용하되 메인 트윈 변수에 할당하지 않고 Fire-and-Forget 방식으로 처리
                m_expSlider.DOValue(progress, 0.2f).SetEase(Ease.OutQuad);
            }
        }

        /// <summary>
        /// 웨이브 정보를 화면 중앙에 페이드 효과와 함께 표시합니다.
        /// </summary>
        private void ShowWaveInfo(string message)
        {
            if (m_mobWaveText == null) return;

            // 기존 연출 즉시 종료
            if (m_waveSequence != null && m_waveSequence.IsActive())
                m_waveSequence.Kill();

            // 1. 텍스트 설정 및 초기화
            m_mobWaveText.text = message;
            m_mobWaveText.gameObject.SetActive(true);

            Color color = m_mobWaveText.color;
            color.a = 0f;
            m_mobWaveText.color = color;

            // 2. 시퀀스 구성 (FadeIn -> Wait -> FadeOut)
            m_waveSequence = DOTween.Sequence()
                .Append(m_mobWaveText.DOFade(1f, 0.5f))
                .AppendInterval(2.0f)
                .Append(m_mobWaveText.DOFade(0f, 1.0f))
                .OnComplete(() => 
                {
                    if (m_mobWaveText != null) m_mobWaveText.gameObject.SetActive(false);
                })
                .SetUpdate(true); // 게임 일시정지 시에도 UI 알림이 보여야 한다면 true
        }

        /// <summary>
        /// 인벤토리 아이콘 리스트를 갱신합니다.
        /// </summary>
        private void RefreshIcons(List<Image> slots, IReadOnlyList<Sprite> sprites)
        {
            if (slots == null) return;

            for (int i = 0; i < slots.Count; i++)
            {
                // 슬롯 UI 존재 여부 확인 (Unity Object Check)
                if (slots[i] == null) continue;

                if (i < sprites.Count && sprites[i] != null)
                {
                    slots[i].gameObject.SetActive(true);
                    slots[i].sprite = sprites[i];
                }
                else
                {
                    slots[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 레벨업 시 텍스트 강조 연출을 재생합니다.
        /// </summary>
        private void PlayLevelUpEffect()
        {
            if (m_playerLevelText != null)
            {
                // 기존 스케일 트윈이 있다면 제거 (DOTween은 동일 타겟 트윈을 자동 관리하기도 하지만 명시적이 안전)
                m_playerLevelText.transform.DOKill(); 
                m_playerLevelText.transform.DOScale(Vector3.one * 1.2f, 0.15f).SetLoops(2, LoopType.Yoyo);
            }
        }

        #endregion
    }
}