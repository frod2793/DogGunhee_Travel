using System;
using R3;

namespace InGame.UI.ViewModels
{
    /// <summary>
    /// [설명]: 시스템 확인(Confirm) 및 취소(Cancel) 팝업의 데이터 바인딩과 비즈니스 로직을 관리하는 ViewModel입니다.
    /// View는 이 클래스의 ReactiveProperty 상태를 구독하여 화면을 갱신하며, 사용자의 선택 결과를 대리자(Action)를 통해 외부로 전달합니다.
    /// </summary>
    public class ConfirmPopupViewModel : IDisposable
    {
        #region 내부 필드

        /// <summary> [설명]: 팝업의 현재 가시성(노출 여부) 상태를 관리하는 리액티브 속성 </summary>
        private readonly ReactiveProperty<bool> m_isVisible = new(false);

        /// <summary> [설명]: 팝업 상단에 표시될 제목 문자열을 담는 리액티브 속성 </summary>
        private readonly ReactiveProperty<string> m_title = new(string.Empty);

        /// <summary> [설명]: 팝업 중앙에 표시될 본문 메시지 문자열을 담는 리액티브 속성 </summary>
        private readonly ReactiveProperty<string> m_message = new(string.Empty);

        /// <summary> [설명]: 사용자가 확인 버튼을 눌렀을 때 실행될 외부 요청 로직 </summary>
        private Action m_onConfirmAction;

        /// <summary> [설명]: 사용자가 취소 혹은 닫기 버튼을 눌렀을 때 실행될 외부 요청 로직 </summary>
        private Action m_onCancelAction;

        #endregion

        #region 공개 프로퍼티

        /// <summary> [설명]: View에서 구독 가능한 팝업 활성화 여부 (읽기 전용) </summary>
        public ReadOnlyReactiveProperty<bool> IsVisible => m_isVisible;

        /// <summary> [설명]: View에서 구독 가능한 팝업 제목 (읽기 전용) </summary>
        public ReadOnlyReactiveProperty<string> Title => m_title;

        /// <summary> [설명]: View에서 구독 가능한 팝업 메시지 (읽기 전용) </summary>
        public ReadOnlyReactiveProperty<string> Message => m_message;

        #endregion

        #region 초기화 및 정리

        /// <summary>
        /// [설명]: ViewModel이 소멸될 때 모든 리액티브 자원을 해제하여 메모리 누수를 방지합니다.
        /// </summary>
        public void Dispose()
        {
            m_isVisible.Dispose();
            m_title.Dispose();
            m_message.Dispose();

            // 상호 참조 해제
            m_onConfirmAction = null;
            m_onCancelAction = null;
        }

        #endregion

        #region 공개 인터페이스

        /// <summary>
        /// [설명]: 새로운 확인 팝업 요청 데이터를 주입하고 화면 노출 상태를 활성화합니다.
        /// </summary>
        /// <param name="title">UI에 표시할 제목</param>
        /// <param name="message">UI에 표시할 상세 설명 메시지</param>
        /// <param name="onConfirm">확인 선택 시 수행할 동작</param>
        /// <param name="onCancel">취소 선택 시 수행할 동작 (생략 가능)</param>
        public void ShowPopup(string title, string message, Action onConfirm, Action onCancel = null)
        {
            // 데이터 할당 및 업데이트 알림
            m_title.Value = title;
            m_message.Value = message;

            // 실행 대리자 캐싱
            m_onConfirmAction = onConfirm;
            m_onCancelAction = onCancel;

            // 가시성 활성화
            m_isVisible.Value = true;
        }

        /// <summary>
        /// [설명]: 사용자가 '확인' 버튼을 클릭했음을 알리고 등록된 액션을 실행합니다. (View에서 호출)
        /// </summary>
        public void Confirm()
        {
            if (m_onConfirmAction != null)
            {
                m_onConfirmAction.Invoke();
            }
            
            ClosePopup();
        }

        /// <summary>
        /// [설명]: 사용자가 '취소' 버튼을 클릭했음을 알리고 등록된 액션을 실행합니다. (View에서 호출)
        /// </summary>
        public void Cancel()
        {
            if (m_onCancelAction != null)
            {
                m_onCancelAction.Invoke();
            }
            
            ClosePopup();
        }

        #endregion

        #region 내부 비즈니스 로직

        /// <summary>
        /// [설명]: 팝업의 가시성 상태를 비활성화하고 사용된 일회성 콜백 참조를 명시적으로 제거합니다.
        /// </summary>
        private void ClosePopup()
        {
            m_isVisible.Value = false;

            // 중복 실행 및 메모리 누수 방지를 위한 초기화
            m_onConfirmAction = null;
            m_onCancelAction = null;
        }

        #endregion
    }
}