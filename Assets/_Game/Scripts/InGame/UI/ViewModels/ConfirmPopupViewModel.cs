using System;
using R3;

namespace InGame.UI.ViewModels
{
    /// <summary>
    /// 확인/취소 팝업의 데이터와 상태를 관리하는 ViewModel입니다.
    /// <br/> View(UI)는 이 클래스의 상태(ReadOnlyReactiveProperty)를 구독하여 화면을 갱신합니다.
    /// </summary>
    public class ConfirmPopupViewModel : IDisposable
    {
        #region 1. 내부 상태 (Fields)
        // 상태 관리를 위한 ReactiveProperty (쓰기 가능, 내부용)
        private readonly ReactiveProperty<bool> m_isVisible = new(false);
        private readonly ReactiveProperty<string> m_title = new(string.Empty);
        private readonly ReactiveProperty<string> m_message = new(string.Empty);

        // 콜백 액션 (팝업 결과 처리)
        private Action m_onConfirmAction;
        private Action m_onCancelAction;
        #endregion

        #region 2. 공개 프로퍼티 (Properties)
        // View 바인딩용 ReadOnly 프로퍼티 (읽기 전용, 외부용)
        // ReactiveProperty는 ReadOnlyReactiveProperty로 암시적 변환되거나 상속 관계를 가질 수 있음 (R3 버전에 따라 상이할 수 있으나 일반적 패턴 유지)
        public ReadOnlyReactiveProperty<bool> IsVisible => m_isVisible;
        public ReadOnlyReactiveProperty<string> Title => m_title;
        public ReadOnlyReactiveProperty<string> Message => m_message;
        #endregion

        #region 3. 초기화 및 정리 (Lifecycle)
        /// <summary>
        /// ViewModel이 파괴될 때 호출됩니다. 리액티브 프로퍼티를 해제합니다.
        /// </summary>
        public void Dispose()
        {
            m_isVisible.Dispose();
            m_title.Dispose();
            m_message.Dispose();
            
            // 참조 해제
            m_onConfirmAction = null;
            m_onCancelAction = null;
        }
        #endregion

        #region 4. 공개 메서드 (Public Logic)
        /// <summary>
        /// 팝업을 표시하고 데이터를 설정합니다.
        /// </summary>
        /// <param name="title">팝업 제목</param>
        /// <param name="message">팝업 본문 메시지</param>
        /// <param name="onConfirm">확인 버튼 클릭 시 실행할 액션</param>
        /// <param name="onCancel">취소 버튼 클릭 시 실행할 액션 (기본값: null)</param>
        public void ShowPopup(string title, string message, Action onConfirm, Action onCancel = null)
        {
            // 데이터 설정
            m_title.Value = title;
            m_message.Value = message;
            
            // 콜백 연결
            m_onConfirmAction = onConfirm;
            m_onCancelAction = onCancel;
            
            // UI 표시 트리거
            m_isVisible.Value = true;
        }

        /// <summary>
        /// 확인 버튼 로직을 실행합니다. (View에서 호출)
        /// </summary>
        public void Confirm()
        {
            m_onConfirmAction?.Invoke();
            ClosePopup();
        }

        /// <summary>
        /// 취소 버튼 로직을 실행합니다. (View에서 호출)
        /// </summary>
        public void Cancel()
        {
            m_onCancelAction?.Invoke();
            ClosePopup();
        }
        #endregion

        #region 5. 내부 로직 (Private Helpers)
        /// <summary>
        /// 팝업을 닫고 상태를 초기화합니다.
        /// </summary>
        private void ClosePopup()
        {
            m_isVisible.Value = false;
            
            // 팝업이 닫힌 후 불필요한 참조가 남지 않도록 액션 초기화
            // (재사용 시 ShowPopup에서 다시 할당됨)
            m_onConfirmAction = null;
            m_onCancelAction = null;
        }
        #endregion
    }
}