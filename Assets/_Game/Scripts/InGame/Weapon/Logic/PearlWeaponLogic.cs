using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Logic
{
    /// <summary>
    /// PearlWeaponView에서 추출한 튜닝 데이터 구조체입니다.
    /// </summary>
    public struct PearlTuningData
    {
        public float HitCooldown;
    }

    /// <summary>
    /// 싸구려 진주 무기(Pearl)의 비즈니스 로직을 담당하는 POCO 클래스입니다.
    /// </summary>
    public class PearlWeaponLogic
    {
        #region 내부 상태 및 변수

        private readonly WeaponRuntimeStats m_stats;
        private readonly PearlTuningData m_tuningData;

        #endregion

        #region 프로퍼티

        public float AttackPower => m_stats.CurrentAttackPower;
        public float AttackSpeed => (m_stats.CurrentAttackSpeed > 0) ? m_stats.CurrentAttackSpeed : 1f;
        public float StunTime => m_stats.MobStunTime;
        public bool IsEvolved => m_stats.IsEvolved;

        public float HitCooldown => m_tuningData.HitCooldown;

        #endregion

        #region 생성자 및 초기화

        public PearlWeaponLogic(WeaponRuntimeStats stats, PearlTuningData tuningData)
        {
            m_stats = stats;
            m_tuningData = tuningData;
        }

        /// <summary>
        /// 필요 시 무기 스탯을 업데이트합니다.
        /// </summary>
        public void UpdateStats(WeaponRuntimeStats stats)
        {
            // 참조형이므로 필요 시 여기에 추가 로직 작성
        }

        #endregion
    }
}
