using InGame;
using InGame.Core;
using InGame.Core.Interfaces;
using InGame.Data;
using InGame.Data.Managers;
using InGame.Managers;
using InGame.Services;
using InGame.UI;

namespace Lobby
{
    /// <summary>
    /// [설명]: LobbyUIViewManager 초기화 시 주입되어야 할 시스템 및 서비스들의 의존성 묶음 체입니다.
    /// 너무 많은 매개변수로 인한 시그니처 오염을 방지하기 위해 사용됩니다.
    /// </summary>
    public struct LobbyDependencies
    {
        public PlayerDataDTO PlayerData;
        public ServerSessionDTO ServerSession;
        public PlayerDataService PlayerService;
        public ISoundManager SoundManager;
        public ISceneLoader SceneLoader;
        public IPopupService PopupService;
        public IEffectService EffectService;
        public IRemoteDataUpdateService RemoteDataService;
        public IInventoryContext InventoryContext;
    }
}
