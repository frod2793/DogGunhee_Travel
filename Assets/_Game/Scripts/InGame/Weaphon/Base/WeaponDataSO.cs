using UnityEngine;
using System.Collections.Generic;

namespace InGame.Weaphon.Base
{
    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "Game/Weapon/WeaponData")]
    public class WeaponDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string SkillCode;
        public string WeaponName;
        public Sprite Icon;
        [TextArea] public string Description;

        [Header("기본 스탯")]
        public float BaseAttackPower;
        public float BaseCoolTime;
        public float BaseAttackSpeed = 1.0f;
        public float BaseAttackRange;
        public float BaseDuration; // 지속 시간 (버프나 장판형)
        public int BaseProjectileCount = 1;

        [Header("프리팹 참조")]
        public GameObject ProjectilePrefab;
        public GameObject EffectPrefab;

        [Header("성장 테이블")]
        public List<WeaponUpgradeData> Upgrades;
    }

    [System.Serializable]
    public class WeaponUpgradeData
    {
        public int Level;
        public string Description;
        public List<StatModification> Modifications;
    }
}
