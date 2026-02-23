using InGame.ObjectPool;
using InGame.Weapon.Base;

namespace InGame.Weapon.Strategies
{
    /// <summary>
    /// [설명]: 오라(Aura) 타입 이펙트가 구현해야 할 인터페이스입니다.
    /// 초기화, 실시간 스탯 갱신, 비활성화 로직을 포함합니다.
    /// </summary>
    public interface IAuraEffect
    {
        /// <summary>
        /// 최초 활성화 시 초기화합니다.
        /// </summary>
        void Init(WeaponRuntimeStats stats, WeaponPoolManager poolManager);

        /// <summary>
        /// 런타임에 스탯이 변경되었을 때(레벨업 등) 호출됩니다.
        /// </summary>
        void UpdateStats(WeaponRuntimeStats stats);

        /// <summary>
        /// 오라를 비활성화하거나 제거할 때 호출됩니다.
        /// </summary>
        void Deactivate();
    }
}