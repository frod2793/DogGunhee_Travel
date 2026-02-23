using Cysharp.Threading.Tasks;
using InGame.Managers;

namespace InGame.Core.Interfaces
{
    /// <summary>
    /// [설명]: 게임의 실행 상태(시작, 정지, 종료) 및 플레이 상태 정보를 제공하고 제어하는 인터페이스입니다.
    /// </summary>
    public interface IGameStateService
    {
        PlayStateManager State { get; }
        bool IsPlaying { get; }
        bool IsCleared { get; }
        IEffectService EffectService { get; }
        InGame.Services.ISoundManager SoundService { get; }
        void SetMenuPopupState(bool isPause);
        void OpenOptionPopup();
        UniTask SaveGameResult();
    }
}
