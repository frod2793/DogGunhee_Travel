using Cysharp.Threading.Tasks;

namespace InGame.Core.Interfaces
{
    /// <summary>
    /// [설명]: 게임의 진행 흐름 제어(캐릭터 스폰, 무기 장장/제거 등)를 위한 인터페이스입니다.
    /// </summary>
    public interface IGameFlowController
    {
        UniTask EquipNewWeapon(SkillData skillData, bool playEffect = true, int startLevel = 1, bool startEvolved = false);
        UniTask ChangeCharacterAndWeapon_Spawn();
        void RemoveWeaponForTest(string skillCode);
        void ClearStageForTest();
    }
}
