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
    /// 인게임 HUD(상단 정보, 플레이어 상태)를 전담하는 View 클래스입니다.
    /// </summary>
    public class InGameHUDView : MonoBehaviour
    {
        #region UI 컴포넌트 (인스펙터)

        [Header("<color=cyan>플레이어 상태 (상단/중앙)</color>")]
        [SerializeField] private TMP_Text m_playerLevelText;
        [SerializeField] private Slider m_playerLevelSlider;
        [SerializeField] private TMP_Text m_playerLevelText_InGame;
        [SerializeField] private Slider m_expSlider;

        [Header("<color=cyan>게임 정보 (HUD)</color>")]
        [SerializeField] private TMP_Text m_mobWaveText;
        [SerializeField] private TMP_Text m_coinText;
        [SerializeField] private TMP_Text m_mobCountText;

        [Header("<color=cyan>인벤토리 아이콘</color>")]
        [SerializeField] private List<Image> m_weaponUIList = new List<Image>();
        [SerializeField] private List<Image> m_accessoryUIList = new List<Image>();

        #endregion

        #region 상태 및 ViewModel

        private InGameViewModel m_viewModel;
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();
        private Tween m_expTween;

        #endregion

        public void Bind(InGameViewModel viewModel)
        {
            m_viewModel = viewModel;
            m_disposables.Clear();

            // 1. 플레이어 상태 바인딩
            m_viewModel.PlayerLevel
                .Subscribe(level => UpdateLevelText(level))
                .AddTo(m_disposables);

            m_viewModel.ExpProgress
                .Subscribe(progress => UpdateExpUI(progress))
                .AddTo(m_disposables);

            m_viewModel.OnLevelUp
                .Subscribe(_ => PlayLevelUpEffect())
                .AddTo(m_disposables);

            // 2. 게임 정보 바인딩
            m_viewModel.CurrentWave
                .Subscribe(wave => UpdateWaveText(wave))
                .AddTo(m_disposables);

            m_viewModel.CoinCount
                .Subscribe(coin => m_coinText.SetSafeText("{0}", coin))
                .AddTo(m_disposables);

            m_viewModel.KillCount
                .Subscribe(kills => m_mobCountText.SetSafeText("{0}", kills))
                .AddTo(m_disposables);

            // 3. 인벤토리 아이콘 바인딩
            m_viewModel.WeaponSprites
                .Subscribe(sprites => RefreshIcons(m_weaponUIList, sprites))
                .AddTo(m_disposables);

            m_viewModel.AccessorySprites
                .Subscribe(sprites => RefreshIcons(m_accessoryUIList, sprites))
                .AddTo(m_disposables);
        }

        private void OnDestroy()
        {
            m_disposables.Dispose();
            m_expTween?.Kill();
        }

        #region UI 업데이트 상세

        private void UpdateLevelText(int level)
        {
            m_playerLevelText.SetSafeText("Lv. {0}", level);
            m_playerLevelText_InGame.SetSafeText("Lv. {0}", level);
        }

        private void UpdateExpUI(float progress)
        {
            m_expTween?.Kill();
            if (m_playerLevelSlider != null)
                m_expTween = m_playerLevelSlider.DOValue(progress, 0.2f).SetEase(Ease.OutQuad);
            if (m_expSlider != null)
                m_expSlider.DOValue(progress, 0.2f).SetEase(Ease.OutQuad);
        }

        private void UpdateWaveText(int wave)
        {
            // 웨이브 텍스트는 연출이 포함될 수 있으므로 별도 처리 가능
            // 기존 UIManager의 ShowWaveTextEffect 로직은 UIManager나 별도 Controller가 담당하는 것이 좋음.
            // 여기서는 단순 텍스트 갱신만 수행.
            m_mobWaveText.SetSafeText("웨이브 {0}", wave);
        }

        private void RefreshIcons(List<Image> slots, IReadOnlyList<Sprite> sprites)
        {
            for (int i = 0; i < slots.Count; i++)
            {
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

        private void PlayLevelUpEffect()
        {
            if (m_playerLevelText != null)
            {
                m_playerLevelText.transform.DOScale(Vector3.one * 1.2f, 0.15f).SetLoops(2, LoopType.Yoyo);
            }
        }

        #endregion
    }

    /// <summary>
    /// TMP_Text 확장 메서드 (Null 체크 포함)
    /// </summary>
    public static class TextExtensions
    {
        public static void SetSafeText(this TMP_Text text, string format, object arg)
        {
            if (text != null) text.text = string.Format(format, arg);
        }
    }
}
