using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    public interface IAreaAttackEffect
    {
        void Initialize(WeaponRuntimeStats stats);
    }
}
