using System;

namespace Lobby.Core
{
    /// <summary>
    /// [설명]: 로비의 씬 전환, 팝업 오픈/클로즈 등 네비게이션을 추상화한 인터페이스입니다.
    /// </summary>
    public interface ILobbyNavigator
    {
        /// <summary> [설명]: 지정된 이름의 씬을 로드합니다. </summary>
        void LoadScene(string sceneName, object payload = null);

        /// <summary> [설명]: 옵션 설정 팝업을 엽니다. </summary>
        void OpenOptionPopup();

        /// <summary> [설명]: 현재 가장 위에 떠 있는 팝업을 닫습니다. </summary>
        void CloseTopPopup();

        /// <summary> [설명]: 팝업을 활성화하고 닫기 액션을 등록합니다. </summary>
        void RegisterPopup(Action closeAction);
    }
}
