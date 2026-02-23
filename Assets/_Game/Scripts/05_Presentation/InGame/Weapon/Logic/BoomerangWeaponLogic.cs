using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Logic
{
    /// <summary>
    /// [설명]: 부메랑(Boomerang) 무기의 에디터 조정 데이터를 담는 구조체입니다.
    /// </summary>
    public struct BoomerangWeaponTuningData
    {
        public float StartAngle;
        public float AngleStep;
        public int BurstDelayMs;
    }

    /// <summary>
    /// [설명]: 부메랑 무기의 비즈니스 로직(발사 각도 계산, 연사 설정 등)을 담당하는 클래스입니다.
    /// </summary>
    public class BoomerangWeaponLogic
    {
        #region 내부 변수

        private WeaponRuntimeStats m_stats;

        #endregion

        #region 프로퍼티

        // 튜닝 데이터
        public float StartAngle { get; private set; } = -15f;
        public float AngleStep { get; private set; } = 30f;
        public int BurstDelayMs { get; private set; } = 50;

        // 런타임 스탯 연동
        public float AttackPower => m_stats.CurrentAttackPower;
        public float StunTime => m_stats.MobStunTime;
        public float Speed => m_stats.CurrentAttackSpeed > 0 ? m_stats.CurrentAttackSpeed : 1f;
        public float Range => m_stats.CurrentAttackRange;

        public int MaxProjectiles => Mathf.Max(1, m_stats.CurrentProjectileCount);
        public int BurstCount => m_stats.IsEvolved ? 3 : 1; // 기본 1발, 진화 시 3발 연사

        #endregion

        #region 생성자 및 초기화

        public BoomerangWeaponLogic(WeaponRuntimeStats stats, BoomerangWeaponTuningData? tuningData = null)
        {
            UpdateStats(stats, tuningData);
        }

        /// <summary>
        /// 무기 스탯 및 튜닝 데이터를 기반으로 수치를 갱신합니다.
        /// </summary>
        public void UpdateStats(WeaponRuntimeStats stats, BoomerangWeaponTuningData? tuningData = null)
        {
            m_stats = stats;

            if (tuningData.HasValue)
            {
                var data = tuningData.Value;
                StartAngle = data.StartAngle;
                AngleStep = data.AngleStep;
                BurstDelayMs = data.BurstDelayMs;
            }
        }

        #endregion

        #region 로직 메서드

        /// <summary>
        /// 발사 인덱스에 따른 각도를 계산합니다.
        /// </summary>
        /// <param name="index">현재 발사체 인덱스</param>
        /// <param name="totalCount">총 발사체 개수</param>
        /// <param name="baseAngle">기준 각도 (보통 플레이어 정면)</param>
        public float CalculateAngle(int index, int totalCount, float baseAngle)
        {
            float startAngleOffset = StartAngle * (totalCount - 1);
            float step = (totalCount > 1) ? AngleStep : 0f;
            
            return baseAngle + startAngleOffset + (step * index);
        }

        #endregion
    }
}