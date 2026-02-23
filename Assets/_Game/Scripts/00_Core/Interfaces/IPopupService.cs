using System;

namespace InGame.UI
{
    /// <summary>
    /// [설명]: UI 팝업 스택을 관리하는 서비스 인터페이스입니다.
    /// </summary>
    public interface IPopupService
    {
        void RegisterPopup(Action closeAction);
        void CloseTopPopup();
        void RemoveLastPopupAction();
        void ClearAllPopups();
    }
}
