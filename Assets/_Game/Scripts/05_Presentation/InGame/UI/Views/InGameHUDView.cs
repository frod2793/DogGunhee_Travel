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
    /// [설명]: 게임 플레이 중 화면에 항상 노출되는 HUD(상단 정보 바, 경험치, 코인, 인벤토리 아이콘 등)를 관리하는 View 클래스입니다.
    /// InGameViewModel의 실시간 데이터를 구독하여 시각적으로 동기화하며, 레벨업이나 웨이브 전환 등 주요 시점에 DOTween 연출을 수행합니다.
    /// </summary>
    public class InGameHUDView : MonoBehaviour
    {
        #region 에디터 설정

        [Header("1. 플레이어 상태 (상단/중앙)")]
        [SerializeField, Tooltip("플레이어의 현재 레벨을 숫자로 표시하는 상단 텍스트")]
        private TMP_Text m_playerLevelText;

        [SerializeField, Tooltip("현재 레벨 내에서의 경험치 진행 비율을 나타내는 상단 슬라이더")]
        private Slider m_playerLevelSlider;

        [SerializeField, Tooltip("인게임 캐릭터 발치 또는 머리 위에 위치하여 즉각적으로 확인 가능한 레벨 텍스트")]
        private TMP_Text m_playerLevelText_InGame;

        [SerializeField, Tooltip("필요시 추가로 배치할 수 있는 경험치 보조 슬라이더")]
        private Slider m_expSlider;

        [Header("2. 게임 흐름 정보")]
        [SerializeField, Tooltip("새로운 웨이브가 시작될 때 중앙에 페이드인/아웃으로 노출될 알림용 텍스트")]
        private TMP_Text m_mobWaveText;

        [SerializeField, Tooltip("이번 판에서 지금까지 획득한 누적 코인 수량을 표시하는 텍스트")]
        private TMP_Text m_coinText;

        [SerializeField, Tooltip("이번 판에서 지금까지 처치한 누적 몬스터 수량을 표시하는 텍스트")]
        private TMP_Text m_mobCountText;

        [Header("3. 인벤토리 및 장비 슬롯")]
        [SerializeField, Tooltip("획득한 무기들의 대표 아이콘을 순서대로 정렬하여 보여줄 이미지 슬롯 리스트")]
        private List<Image> m_weaponUIList = new List<Image>();

        [SerializeField, Tooltip("획득한 장신구/악세서리들의 대표 아이콘을 보여줄 이미지 슬롯 리스트")]
        private List<Image> m_accessoryUIList = new List<Image>();

        #endregion

        #region 내부 필드

        /// <summary> 인게임의 모든 핵심 상태 데이터를 보유하고 있는 뷰모델 참조 </summary>
        private InGameViewModel m_viewModel;

        /// <summary> 뷰 생명주기에 종속된 R3 이벤트 구독 해제 티켓 모음 </summary>
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();

        /// <summary> 경험치 바의 부드러운 증감을 담당하는 트윈 핸들 </summary>
        private Tween m_expTween;

        /// <summary> 웨이브 알림의 등장-대기-퇴장을 제어하는 시퀀스 핸들 </summary>
        private Sequence m_waveSequence;

        #endregion

        #region 유니티 생명주기

        /// <summary>
        /// [설명]: 객체 파기 시 모든 활성 이벤트 스트림과 애니메이션(Tween) 리소스를 즉시 해제하여 메모리 예외를 방지합니다.
        /// </summary>
        private void OnDestroy()
        {
            // 리액티브 구독 일괄 해제
            m_disposables.Dispose();

            // 유니티 엔진 객체 존재 여부 확인 후 트윈 강제 제거
            if (m_expTween != null && m_expTween.IsActive())
            {
                m_expTween.Kill();
            }

            if (m_waveSequence != null && m_waveSequence.IsActive())
            {
                m_waveSequence.Kill();
            }
        }

        #endregion

        #region 데이터 바인딩

        /// <summary>
        /// [설명]: 인게임 뷰모델의 속성들과 HUD의 개별 UI 요소들을 일대일 매칭하여 실시간 동기화 채널을 구축합니다.
        /// </summary>
        /// <param name="viewModel">전달받은 인게임 통합 뷰모델</param>
        public void Bind(InGameViewModel viewModel)
        {
            if (viewModel == null)
            {
                return;
            }

            m_viewModel = viewModel;
            m_disposables.Clear(); // 재활용 시 기존 구독 이력 초기화

            BindPlayerStatus();
            BindGameInfo();
            BindInventory();
        }

        /// <summary>
        /// [설명]: 플레이어의 레벨, 경험치, 레벨업 신호 스트림을 바인딩합니다.
        /// </summary>
        private void BindPlayerStatus()
        {
            if (m_viewModel == null)
            {
                return;
            }

            // 실시간 레벨 갱신
            m_viewModel.PlayerLevel
                .Subscribe(level => UpdateLevelText(level))
                .AddTo(m_disposables);

            // 실시간 경험치 게이지 갱신 (트리거 발생 시 애니메이션 유도)
            m_viewModel.ExpProgress
                .Subscribe(progress => UpdateExpUI(progress))
                .AddTo(m_disposables);

            // 레벨업 시각 연출 트리거
            m_viewModel.OnLevelUp
                .Subscribe(_ => PlayLevelUpEffect())
                .AddTo(m_disposables);
        }

        /// <summary>
        /// [설명]: 웨이브 진행 상황, 코인, 킬 카운트 등 게임 전역 정보 스트림을 바인딩합니다.
        /// </summary>
        private void BindGameInfo()
        {
            if (m_viewModel == null)
            {
                return;
            }

            // 웨이브 시작 정보 알림 (스테이지 ID와 조합)
            m_viewModel.OnWaveStarted
                .CombineLatest(m_viewModel.CurrentStageId, (waveData, stage) => (waveData, stage))
                .Subscribe(data => ShowWaveInfo($"Stage {data.stage} - {data.waveData.waveName}"))
                .AddTo(m_disposables);

            // 웨이브 완료 정보 알림
            m_viewModel.OnWaveCompleted
                .CombineLatest(m_viewModel.CurrentStageId, (waveData, stage) => (waveData, stage))
                .Subscribe(data => ShowWaveInfo($"Stage {data.stage} - {data.waveData.waveName} 클리어"))
                .AddTo(m_disposables);

            // 재화 획득 텍스트 동기화
            m_viewModel.CoinCount
                .Subscribe(coin =>
                {
                    if (m_coinText != null)
                    {
                        m_coinText.text = coin.ToString();
                    }
                })
                .AddTo(m_disposables);

            // 몬스터 처치 수 텍스트 동기화
            m_viewModel.KillCount
                .Subscribe(kills =>
                {
                    if (m_mobCountText != null)
                    {
                        m_mobCountText.text = kills.ToString();
                    }
                })
                .AddTo(m_disposables);
        }

        /// <summary>
        /// [설명]: 현재 장착 중인 무기와 아이템 스프라이트 리스트를 인벤토리 슬롯과 동기화합니다.
        /// </summary>
        private void BindInventory()
        {
            if (m_viewModel == null)
            {
                return;
            }

            m_viewModel.WeaponSprites
                .Subscribe(sprites => RefreshIcons(m_weaponUIList, sprites))
                .AddTo(m_disposables);

            m_viewModel.AccessorySprites
                .Subscribe(sprites => RefreshIcons(m_accessoryUIList, sprites))
                .AddTo(m_disposables);
        }

        #endregion

        #region 시각 업데이트 및 애니메이션

        /// <summary>
        /// [설명]: 인게임 상단 및 캐릭터 근처의 레벨 표시 텍스트를 최신값으로 업데이트합니다.
        /// </summary>
        private void UpdateLevelText(int level)
        {
            if (m_playerLevelText != null)
            {
                m_playerLevelText.text = $"Lv. {level}";
            }

            if (m_playerLevelText_InGame != null)
            {
                m_playerLevelText_InGame.text = $"Lv. {level}";
            }
        }

        /// <summary>
        /// [설명]: 경험치 슬라이더의 Value를 부드럽게 채우는 트윈 애니메이션을 실행합니다.
        /// </summary>
        private void UpdateExpUI(float progress)
        {
            // 연속적인 변화 대응을 위해 진행 중인 트윈은 즉시 킬
            if (m_expTween != null && m_expTween.IsActive())
            {
                m_expTween.Kill();
            }

            if (m_playerLevelSlider != null)
            {
                m_expTween = m_playerLevelSlider.DOValue(progress, 0.2f).SetEase(Ease.OutQuad);
            }

            if (m_expSlider != null)
            {
                m_expSlider.DOValue(progress, 0.2f).SetEase(Ease.OutQuad);
            }
        }

        /// <summary>
        /// [설명]: 화면 중앙에 웨이브 메시지를 서서히 띄운 후 일정 시간 뒤에 사라지게 하는 시퀀스 로직입니다.
        /// </summary>
        private void ShowWaveInfo(string message)
        {
            if (m_mobWaveText == null)
            {
                return;
            }

            if (m_waveSequence != null && m_waveSequence.IsActive())
            {
                m_waveSequence.Kill();
            }

            m_mobWaveText.text = message;
            m_mobWaveText.gameObject.SetActive(true);

            Color color = m_mobWaveText.color;
            color.a = 0f;
            m_mobWaveText.color = color;

            // 시퀀스 구성: 나타남(0.5s) -> 유지(2s) -> 사라짐(1s)
            m_waveSequence = DOTween.Sequence()
                .Append(m_mobWaveText.DOFade(1f, 0.5f))
                .AppendInterval(2.0f)
                .Append(m_mobWaveText.DOFade(0f, 1.0f))
                .OnComplete(() =>
                {
                    if (m_mobWaveText != null)
                    {
                        m_mobWaveText.gameObject.SetActive(false);
                    }
                })
                .SetUpdate(true);
        }

        /// <summary>
        /// [설명]: 사전에 정의된 아이콘 슬롯(배열)에 획득한 데이터가 있는 만큼 스프라이트를 채우고 나머지는 비활성화합니다.
        /// </summary>
        private void RefreshIcons(List<Image> slots, IReadOnlyList<Sprite> sprites)
        {
            if (slots == null || sprites == null)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }

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
        /// [설명]: 레벨업 시 해당 텍스트를 위아래로 튕겨 강조하는 펌핑 시각 효과를 수동 재생합니다.
        /// </summary>
        private void PlayLevelUpEffect()
        {
            if (m_playerLevelText != null)
            {
                m_playerLevelText.transform.DOKill();
                m_playerLevelText.transform.DOScale(Vector3.one * 1.2f, 0.15f).SetLoops(2, LoopType.Yoyo);
            }
        }

        #endregion
    }
}