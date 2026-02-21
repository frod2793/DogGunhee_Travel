using InGame.Core.Interfaces;
using System;

namespace InGame.Data
{
    /// <summary>
    /// [설명]: 씬 전환 시 필요한 모든 데이터를 통합하여 전달하는 페이로드 DTO입니다.
    /// 플레이어 데이터와 서버 세션 정보를 포함합니다.
    /// </summary>
    [Serializable]
    public class ScenePayloadDTO
    {
        #region 데이터 필드

        public PlayerDataDTO PlayerData { get; set; }
        public ServerSessionDTO ServerSession { get; set; }
        public InGame.Services.ISoundManager SoundService { get; set; }
        public InGame.ISceneLoader SceneLoader { get; set; }
        public InGame.UI.IPopupService PopupService { get; set; }
        public InGame.Managers.IEffectService EffectService { get; set; }
        // RemoteDataService 인터페이스를 임시로 object 선언 하거나 실제 타입을 넣음
        public InGame.Data.Managers.IRemoteDataUpdateService RemoteDataService { get; set; }
        public InGame.Core.Interfaces.IInventoryContext InventoryContext { get; set; }

        #endregion

        #region 생성자

        public ScenePayloadDTO(PlayerDataDTO playerData, ServerSessionDTO serverSession, InGame.Services.ISoundManager soundService = null)
        {
            PlayerData = playerData;
            ServerSession = serverSession;
            SoundService = soundService;
        }

        #endregion
    }
}
