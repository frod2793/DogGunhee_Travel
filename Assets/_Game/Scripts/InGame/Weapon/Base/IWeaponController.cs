using UnityEngine;
using InGame;

namespace InGame.Weapon.Base
{
    public interface IWeaponController
    {
        #region 식별자 및 데이터
        string SkillCode { get; }
        string WeaponName { get; }
        SkillData SkillData { get; set; }
        Sprite Thumbnail { get; }
        #endregion

        #region 레벨 및 상태
        int CurrentLevel { get; }
        int MaxLevel { get; }
        bool IsEvolved { get; }
        #endregion

        void Init(WeaponDataSO data, Transform owner, System.Func<Vector3> getTargetDirection);
        void OnUpdate(float deltaTime);
        void OnLateUpdate();
        void LevelUp();
        void Dispose();
        void Attack(Vector3 direction);
    }
}
