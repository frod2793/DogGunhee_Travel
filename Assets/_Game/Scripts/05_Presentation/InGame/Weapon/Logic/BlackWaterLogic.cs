using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Logic
{
    /// <summary>
    /// [설명]: 블랙워터(Ink/먹물) 무기의 에디터 조정 데이터를 담는 구조체입니다.
    /// </summary>
    public struct BlackWaterTuningData
    {
        public float DamageTickInterval;
        public float SlowAmount;
        public float SlowDuration;
    }

    /// <summary>
    /// [설명]: 블랙워터 무기의 비즈니스 로직(데미지 주기 계산, 슬로우 수치 등)을 담당하는 클래스입니다.
    /// MonoBehaviour 의존성 없이 순수 C# 로직만 처리합니다.
    /// </summary>
    public class BlackWaterLogic
    {
        #region 내부 변수

        private WeaponRuntimeStats m_stats;

        #endregion

        #region 프로퍼티

        // 튜닝 데이터
        public float DamageTickInterval { get; private set; } = 0.5f;
        public float SlowAmount { get; private set; } = 0.3f;
        public float SlowDuration { get; private set; } = 1.0f;

        // 런타임 스탯 연동
        public float AttackPower => m_stats.CurrentAttackPower;
        public bool IsEvolved => m_stats.IsEvolved;
        public float AttackSpeed => m_stats.CurrentAttackSpeed > 0 ? m_stats.CurrentAttackSpeed : 1f;

        #endregion

        #region 생성자 및 초기화

        public BlackWaterLogic(WeaponRuntimeStats stats, BlackWaterTuningData? tuningData = null)
        {
            UpdateStats(stats, tuningData);
        }

        /// <summary>
        /// 무기 스탯이나 튜닝 데이터를 기반으로 로직 수치를 갱신합니다.
        /// </summary>
        public void UpdateStats(WeaponRuntimeStats stats, BlackWaterTuningData? tuningData = null)
        {
            m_stats = stats;

            if (tuningData.HasValue)
            {
                var data = tuningData.Value;
                DamageTickInterval = data.DamageTickInterval;
                SlowAmount = data.SlowAmount;
                SlowDuration = data.SlowDuration;
            }
        }

        #endregion

        #region 로직 메서드

        /// <summary>
        /// 공격 속도가 반영된 최종 틱 간격을 계산합니다.
        /// </summary>
        public float GetAdjustedTickDelay()
        {
            return DamageTickInterval / AttackSpeed;
        }

        #endregion
    }
}