using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Logic
{
    /// <summary>
    /// [설명]: 뼈다귀(Bone) 무기의 에디터 조정 데이터를 담는 구조체입니다.
    /// </summary>
    public struct BoneWeaponTuningData
    {
        public float BoneSpeed;
    }

    /// <summary>
    /// [설명]: 뼈다귀 무기의 비즈니스 로직(속도, 지속시간 등)을 담당하는 클래스입니다.
    /// </summary>
    public class BoneWeaponLogic
    {
        #region 내부 변수

        private WeaponRuntimeStats m_runtimeStats;

        #endregion

        #region 프로퍼티

        public float BoneSpeed { get; private set; } = 10f;
        
        public float AttackPower => m_runtimeStats.AttackPower;
        public float Duration => m_runtimeStats.CurrentDuration;
        public bool IsEvolved => m_runtimeStats.CurrentLevel >= 6; // 6레벨 진화 가정

        #endregion

        #region 생성자 및 초기화

        public BoneWeaponLogic(WeaponRuntimeStats stats, BoneWeaponTuningData? tuningData = null)
        {
            UpdateStats(stats, tuningData);
        }

        /// <summary>
        /// 무기 스탯 및 튜닝 데이터를 갱신합니다.
        /// </summary>
        public void UpdateStats(WeaponRuntimeStats stats, BoneWeaponTuningData? tuningData = null)
        {
            m_runtimeStats = stats;

            if (tuningData.HasValue)
            {
                BoneSpeed = tuningData.Value.BoneSpeed;
            }
        }

        #endregion
    }
}