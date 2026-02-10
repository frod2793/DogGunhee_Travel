using System.Collections.Generic;
using UnityEngine;

namespace InGame.Weapon.Logic
{
    /// <summary>
    /// 고양이 냥냥펀치(CatPunch)의 비즈니스 로직(타격 중복 방지 등)을 담당하는 클래스입니다.
    /// </summary>
    public class CatPunchWeaponLogic
    {
        #region 1. 내부 변수 (Internal State)

        private readonly float m_attackPower;
        private readonly float m_mobStunTime;
        
        // 중복 타격 방지를 위한 ID셋
        private readonly HashSet<int> m_hitMobInstanceIDs = new HashSet<int>();

        #endregion

        #region 2. 프로퍼티 (Properties)

        public float AttackPower => m_attackPower;
        public float MobStunTime => m_mobStunTime;

        #endregion

        #region 3. 생성자 및 초기화 (Constructor & Init)

        public CatPunchWeaponLogic(float attackPower, float mobStunTime)
        {
            m_attackPower = attackPower;
            m_mobStunTime = mobStunTime;
        }

        /// <summary>
        /// 새로운 공격 애니메이션 시작 시 히트 기록을 초기화합니다.
        /// </summary>
        public void ResetHitHistory()
        {
            m_hitMobInstanceIDs.Clear();
        }

        #endregion

        #region 4. 로직 메서드 (Logic Methods)

        /// <summary>
        /// 해당 대상이 이번 공격 프레임에서 이미 타격되었는지 확인하고 등록합니다.
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