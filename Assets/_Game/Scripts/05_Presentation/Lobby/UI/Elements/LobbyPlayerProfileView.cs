using UnityEngine;
using UnityEngine.UI;
using TMPro;
using R3;

namespace Lobby.UI.Elements
{
    /// <summary>
    /// [설명]: 로비에서 플레이어의 프로필 정보(닉네임, 레벨, 경험치)를 표시하는 뷰 컴포넌트입니다.
    /// </summary>
    public class LobbyPlayerProfileView : MonoBehaviour
    {
        #region 에디터 설정
        [SerializeField, Tooltip("플레이어 썸네일 이미지")]
        private Image m_playerProfileImage;

        [SerializeField, Tooltip("플레이어 닉네임 텍스트")]
        private TMP_Text m_playerNameText;

        [SerializeField, Tooltip("플레이어 현재 레벨 텍스트")]
        private TMP_Text m_playerLevelText;

        [SerializeField, Tooltip("경험치 진행 바")]
        private Slider m_playerLevelSlider;
        #endregion

        #region 내부 변수
        private readonly CompositeDisposable m_disposables = new CompositeDisposable();
        #endregion

        #region 초기화 및 바인딩
        /// <summary>
        /// [설명]: ViewModel의 데이터를 기반으로 UI 요소들을 바인딩합니다.
        /// </summary>
        /// <param name="viewModel">로비 뷰모델</param>
        public void Bind(LobbyViewModel viewModel)
        {
            if (viewModel == null) return;

            m_disposables.Clear();

            // 닉네임 동기화
            viewModel.Nickname.Subscribe(nick =>
            {
                if (m_playerNameText != null) m_playerNameText.SetText(nick);
            }).AddTo(m_disposables);

            // 레벨 동기화
            viewModel.Level.Subscribe(level =>
            {
                if (m_playerLevelText != null) m_playerLevelText.SetText("Lv. {0}", level);
            }).AddTo(m_disposables);

            // 경험치 진행도 동기화
            viewModel.Experience.Subscribe(exp =>
            {
                if (m_playerLevelSlider != null) m_playerLevelSlider.value = exp;
            }).AddTo(m_disposables);
        }
        #endregion

        #region 유니티 생명주기
        private void OnDestroy()
        {
            m_disposables.Dispose();
        }
        #endregion
    }
}
