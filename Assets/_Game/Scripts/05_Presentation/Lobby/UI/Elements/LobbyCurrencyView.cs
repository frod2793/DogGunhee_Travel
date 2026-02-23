using UnityEngine;
using TMPro;
using R3;

namespace Lobby.UI.Elements
{
    /// <summary>
    /// [설명]: 로비에서 보유 재화(골드, 다이아몬드) 정보를 표시하는 뷰 컴포넌트입니다.
    /// </summary>
    public class LobbyCurrencyView : MonoBehaviour
    {
        #region 에디터 설정
        [SerializeField, Tooltip("보유 골드 텍스트")]
        private TMP_Text m_goldText;

        [SerializeField, Tooltip("보유 다이아 텍스트")]
        private TMP_Text m_diaText;
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

            // 재화(골드) 동기화
            viewModel.Gold.Subscribe(gold =>
            {
                if (m_goldText != null) m_goldText.SetText("{0}", gold);
            }).AddTo(m_disposables);

            // 재화(다이아) 동기화
            viewModel.Diamond.Subscribe(dia =>
            {
                if (m_diaText != null) m_diaText.SetText("{0}", dia);
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
