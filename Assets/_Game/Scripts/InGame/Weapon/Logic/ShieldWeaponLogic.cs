using System;
using UnityEngine;
using InGame.Weapon.Base;

namespace InGame.Weapon.Logic
{
    /// <summary>
    /// 에디터에서 설정 가능한 히어로 랜딩 무기의 튜닝 데이터입니다. (POCO)
    /// </summary>
    public struct ShieldWeaponTuningData
    {
        public float ImpactTriggerTime;
        public float FollowThroughDelay;
        public float BoomerangSpeed;
        public float ReturnDelay;
        public float RotationsPerSecond;
    }

    /// <summary>
    /// 히어로 랜딩(방패) 무기(Shield)의 비즈니스 로직을 담당하는 POCO 클래스입니다.
    /// 계산 로직과 상태 관리를 MonoBehaviour와 분리합니다.
    /// </summary>
    public class ShieldWeaponLogic
    {
        #region 내부 상태 및 변수

        private WeaponRuntimeStats m_runtimeStats;
        
        #endregion

        #region 프로퍼티 (타이밍 및 설정)

        // 애니메이션 및 타이밍 설정
        public float ImpactTriggerTime { get; private set; } = 1.07f;
        public float FollowThroughDelay { get; private set; } = 0.5f;

        // 부메랑(진화) 설정
        public int BoomerangCount { get; private set; }
        public float BoomerangSpeed { get; private set; } = 5f;
        public float BoomerangDistance { get; private set; }
        public float ReturnDelay { get; private set; } = 0.1f;
        public float RotationsPerSecond { get; private set; } = 2.5f;

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
        /// 무기 스탯이나 튜닝 데이터를 기반으로 수치를 갱신합니다.
        /// </summary>
        public void UpdateStats(WeaponRuntimeStats stats, ShieldWeaponTuningData? tuningData = null)
        {
            m_runtimeStats = stats;
            
            // 데이터 기반 수치 갱신
            BoomerangCount = m_runtimeStats.ProjectileCount > 0 ? m_runtimeStats.ProjectileCount : 5;
            BoomerangDistance = m_runtimeStats.AttackRange > 0 ? m_runtimeStats.AttackRange : 3f;

            // Tuning 데이터가 있는 경우 하드코딩된 기본값 대신 사용
            if (tuningData.HasValue)
            {
                var data = tuningData.Value;
                ImpactTriggerTime = data.ImpactTriggerTime;
                FollowThroughDelay = data.FollowThroughDelay;
                BoomerangSpeed = data.BoomerangSpeed;
                ReturnDelay = data.ReturnDelay;
                RotationsPerSecond = data.RotationsPerSecond;
            }
        }

        #endregion

        #region 로직 메서드

        /// <summary>
        /// 인덱스에 따른 부메랑 발사 방향과 회전값을 계산합니다.
        /// </summary>
        public (Vector3 direction, Quaternion rotation) CalculateBoomerangLaunchInfo(int index)
        {
            float angleStep = 360f / BoomerangCount;
            float currentAngle = index * angleStep;
            Quaternion rotation = Quaternion.Euler(0, 0, currentAngle);
            Vector3 direction = rotation * Vector3.up;
            
            return (direction, rotation);
        }

        /// <summary>
        /// 실제 대기 시간을 공격 속도에 맞춰 계산합니다.
        /// </summary>
        public float GetAdjustedWaitTime(float baseTime)
        {
            return baseTime / AttackSpeed;
        }

        #endregion
    }
}
