using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Logic
{
    /// <summary>
    /// [설명]: 히어로 랜딩(방패) 무기의 에디터 조정 데이터를 담는 구조체입니다.
    /// </summary>
    public struct ShieldWeaponTuningData
    {
        public float ImpactTriggerTime;
        public float FollowThroughDelay;
        public float BoomerangSpeed;
        public float ReturnDelay;
        public float RotationsPerSecond;
        public Vector3 ShockwaveOffset;
    }

    /// <summary>
    /// [설명]: 히어로 랜딩 무기의 비즈니스 로직(타이밍 계산, 부메랑 궤적 등)을 담당하는 클래스입니다.
    /// </summary>
    public class ShieldWeaponLogic
    {
        #region 내부 변수

        private WeaponRuntimeStats m_runtimeStats;

        #endregion

        #region 프로퍼티

        // 애니메이션 및 타이밍 설정
        public float ImpactTriggerTime { get; private set; } = 1.07f;
        public float FollowThroughDelay { get; private set; } = 0.5f;
        public Vector3 ShockwaveOffset { get; private set; } = Vector3.zero;

        // 진화(부메랑) 설정
        public int BoomerangCount { get; private set; }
        public float BoomerangSpeed { get; private set; } = 5f;
        public float BoomerangDistance { get; private set; }
        public float ReturnDelay { get; private set; } = 0.1f;
        public float RotationsPerSecond { get; private set; } = 2.5f;

        // 런타임 스탯 연동
        public bool IsEvolved => m_runtimeStats.IsEvolved;
        public float AttackSpeed => m_runtimeStats.AttackSpeed > 0 ? m_runtimeStats.AttackSpeed : 1.0f;
        public float AttackPower => m_runtimeStats.AttackPower;
        public float MobStunTime => m_runtimeStats.MobStunTime;

        #endregion

        #region 생성자 및 초기화

        public ShieldWeaponLogic(WeaponRuntimeStats stats, ShieldWeaponTuningData? tuningData = null)
        {
            UpdateStats(stats, tuningData);
        }

        /// <summary>
        /// 무기 스탯이나 튜닝 데이터를 기반으로 로직 수치를 갱신합니다.
        /// </summary>
        public void UpdateStats(WeaponRuntimeStats stats, ShieldWeaponTuningData? tuningData = null)
        {
            m_runtimeStats = stats;
            
            // 데이터 기반 수치 계산
            BoomerangCount = m_runtimeStats.ProjectileCount > 0 ? m_runtimeStats.ProjectileCount : 5;
            BoomerangDistance = m_runtimeStats.AttackRange > 0 ? m_runtimeStats.AttackRange : 3f;

            // 튜닝 데이터 적용 (하드코딩 방지)
            if (tuningData.HasValue)
            {
                var data = tuningData.Value;
                ImpactTriggerTime = data.ImpactTriggerTime;
                FollowThroughDelay = data.FollowThroughDelay;
                BoomerangSpeed = data.BoomerangSpeed;
                ReturnDelay = data.ReturnDelay;
                RotationsPerSecond = data.RotationsPerSecond;
                ShockwaveOffset = data.ShockwaveOffset;
            }
        }

        #endregion

        #region 로직 메서드

        /// <summary>
        /// 인덱스에 따른 부메랑의 발사 방향과 초기 회전값을 계산합니다.
        /// </summary>
        /// <param name="index">부메랑 인덱스</param>
        /// <returns>계산된 방향 벡터와 회전 쿼터니언</returns>
        public (Vector3 direction, Quaternion rotation) CalculateBoomerangLaunchInfo(int index)
        {
            float angleStep = 360f / BoomerangCount;
            float currentAngle = index * angleStep;
            
            Quaternion rotation = Quaternion.Euler(0, 0, currentAngle);
            Vector3 direction = rotation * Vector3.up;
            
            return (direction, rotation);
        }

        /// <summary>
        /// 기본 대기 시간을 현재 공격 속도에 맞춰 보정합니다.
        /// </summary>
        public float GetAdjustedWaitTime(float baseTime)
        {
            return baseTime / AttackSpeed;
        }

        #endregion
    }
}