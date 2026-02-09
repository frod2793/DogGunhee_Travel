using System.Collections.Generic;
using UnityEngine;

namespace InGame.Weapon.Logic
{
    /// <summary>
    /// 고양이 냥냥펀치(CatPunch)의 비즈니스 로직을 담당하는 POCO 클래스입니다.
    /// </summary>
    public class CatPunchWeaponLogic
    {
        #region 내부 상태 및 변수

        private readonly float m_attackPower;
        private readonly float m_mobStunTime;
        private readonly HashSet<int> m_hitMobInstanceIDs = new HashSet<int>();

        #endregion

        #region 프로퍼티

        public float AttackPower => m_attackPower;
        public float MobStunTime => m_mobStunTime;

        #endregion

        #region 생성자 및 상태 관리

        public CatPunchWeaponLogic(float attackPower, float mobStunTime)
        {
            m_attackPower = attackPower;
            m_mobStunTime = mobStunTime;
        }

        /// <summary>
        /// 새로운 공격 시작 시 히트 기록을 초기화합니다.
        /// </summary>
        public void ResetHitHistory()
        {
            m_hitMobInstanceIDs.Clear();
        }

        #endregion

        #region 로직 메서드

        /// <summary>
        /// 해당 대상이 이번 공격에서 이미 타격되었는지 확인하고 등록합니다.
        /// </summary>
        /// <param name="instanceID">대상 오브젝트의 Instance ID</param>
        /// <returns>최초 타격이면 true, 이미 타격했으면 false</returns>
        public bool RegisterHit(int instanceID)
        {
            if (m_hitMobInstanceIDs.Contains(instanceID))
            {
                return false;
            }

            m_hitMobInstanceIDs.Add(instanceID);
            return true;
        }

        #endregion
    }
}
