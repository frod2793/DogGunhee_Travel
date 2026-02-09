using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// 플레이어를 따라다니며 지속되는 오라(Aura)형 이펙트 인터페이스입니다.
    /// 초기화 후 스탯 변경(레벨업 등) 시 UpdateStats를 통해 갱신됩니다.
    /// </summary>
    public interface IAuraEffect
    {
        /// <summary>
        /// 최초 생성 시 초기화합니다.
        /// </summary>
        void Init(WeaponRuntimeStats stats);

        /// <summary>
        /// 런타임 중 스탯이 변경되었을 때 호출됩니다.
        /// </summary>
        void UpdateStats(WeaponRuntimeStats stats);

        /// <summary>
        /// 무기가 해제되거나 비활성화될 때 호출됩니다.
        /// </summary>
        void Deactivate();
    }
}
