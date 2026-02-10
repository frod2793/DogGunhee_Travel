using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Logic
{
    /// <summary>
    /// 진주(Pearl) 무기의 튜닝 데이터 구조체입니다.
    /// </summary>
    public struct PearlTuningData
    {
        public float HitCooldown;
    }

    /// <summary>
    /// 진주 무기의 비즈니스 로직(쿨타임, 스탯 연동)을 담당하는 클래스입니다.
    /// </summary>
    public class PearlWeaponLogic
    {
        #region 1. 내부 변수 (Internal State)

        private readonly WeaponRuntimeStats m_stats;

        #endregion

        #region 2. 프로퍼티 (Properties)

        public float AttackPower => m_stats.CurrentAttackPower;
        public float AttackSpeed => (m_stats.CurrentAttackSpeed > 0) ? m_stats.CurrentAttackSpeed : 1f;
        public float StunTime => m_stats.MobStunTime;
        public bool IsEvolved => m_stats.IsEvolved;
        
        #endregion

        #region 3. 생성자 및 초기화 (Constructor & Init)

        public PearlWeaponLogic(WeaponRuntimeStats stats )
        {
            m_stats = stats;
        }

        /// <summary>
        /// 런타임에 무기 스탯이 변경되었을 때 호출하여 내부 상태를 갱신합니다.
        /// </summary>
        public void UpdateStats(WeaponRuntimeStats stats)
        {
            // 현재는 WeaponRuntimeStats가 참조형이므로 자동 반영되지만,
            // 추가적인 계산 로직이 필요할 경우 이곳에 작성합니다.
        }

        #endregion
    }
}