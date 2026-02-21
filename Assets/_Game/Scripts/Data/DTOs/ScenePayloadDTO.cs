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
        public Services.ISoundManager SoundService { get; set; }
        public ISceneLoader SceneLoader { get; set; }
        public UI.IPopupService PopupService { get; set; }
        public InGame.Managers.IEffectService EffectService { get; set; }
        // RemoteDataService 인터페이스를 임시로 object 선언 하거나 실제 타입을 넣음
        public Managers.IRemoteDataUpdateService RemoteDataService { get; set; }
        public IInventoryContext InventoryContext { get; set; }

        /// <summary> [설명]: 타이틀에서 최초로 로그인하여 로비로 진입하는 상황인지 여부입니다. </summary>
        public bool IsFirstLogin { get; set; } = false;

        #endregion

        #region 생성자

        public ScenePayloadDTO(PlayerDataDTO playerData, ServerSessionDTO serverSession, Services.ISoundManager soundService = null)
        {
            PlayerData = playerData;
            ServerSession = serverSession;
            SoundService = soundService;
        }

        #endregion
    }
}
