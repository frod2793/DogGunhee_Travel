namespace Lobby.Core
{
    /// <summary>
    /// [설명]: 우편, 퀘스트, 상점 등 로비의 개별 서브 시스템들의 기능을 통합 제공하는 인터페이스입니다.
    /// </summary>
    public interface ILobbySubSystemService
    {
        /// <summary> [설명]: 우편함 패널을 엽니다. </summary>
        void OpenPostBox();

        /// <summary> [설명]: 우편 보상을 수령합니다. </summary>
        void GetPostReward();

        /// <summary> [설명]: 퀘스트 정보 패널을 엽니다. </summary>
        void OpenQuestPanel();

        /// <summary> [설명]: 상점 패널을 엽니다. </summary>
        void OpenStore();

        /// <summary> [설명]: 인벤토리(아이템 선택) 팝업을 엽니다. </summary>
        void OpenInventory();
    }
}
