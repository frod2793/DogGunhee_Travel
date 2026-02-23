using System;
using UnityEngine;
using InGame.Player.Player_Base;

namespace InGame.Core.Interfaces
{
    /// <summary>
    /// [설명]: 씬 내의 활성화된 플레이어 참조, 컨트롤러, 조이스틱 등 플레이어와 관련된 컨텍스트 정보를 제공합니다.
    /// </summary>
    public interface IPlayerContext
    {
        PlayerBase SpawnedPlayer { get; }
        PlayerController PlayerController { get; }
        VariableJoystick Joystick { get; }
        Transform PlayerTransform { get; }
        
        /// <summary> 플레이어 캐릭터가 변경될 때 발생하는 이벤트 </summary>
        event Action<PlayerBase> OnPlayerChanged;
    }
}
