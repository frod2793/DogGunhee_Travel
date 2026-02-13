using System.Collections.Generic;
using UnityEngine;

namespace InGame.Mob.Systems
{
    /// <summary>
    /// 현재 활성화된 모든 타겟(몹 등)을 관리하고 효율적인 타겟 탐색 기능을 제공하는 시스템 클래스입니다.
    /// <br/> Singleton 대신 DI 방식으로 GameManager에 의해 생성 및 주입됩니다.
    /// </summary>
    public class MobManager
    {
        #region 1. 필드 및 초기화

        // 활성화된 타겟 목록 (탐색 성능을 위해 List 사용, 가비지 발생 최소화)
        private readonly List<ITargetable> m_activeTargets = new List<ITargetable>(100);

        public MobManager()
        {
            // 필요한 초기화 로직
        }

        #endregion

        #region 2. 등록 및 해제

        /// <summary>
        /// 타겟을 관리 목록에 등록합니다.
        /// </summary>
        public void Register(ITargetable target)
        {
            if (target == null) return;
            if (!m_activeTargets.Contains(target))
            {
                m_activeTargets.Add(target);
            }
        }

        /// <summary>
        /// 타겟을 관리 목록에서 제거합니다.
        /// </summary>
        public void Unregister(ITargetable target)
        {
            if (target == null) return;
            m_activeTargets.Remove(target);
        }

        #endregion

        #region 3. 타겟 탐색 (Targeting Logic)

        /// <summary>
        /// 특정 위치를 기준으로 일정 반경 내에서 가장 가까운 타겟을 찾습니다.
        /// </summary>
        /// <param name="origin">탐색 시작 위치</param>
        /// <param name="range">탐색 반경</param>
        /// <returns>가장 가까운 타겟 (없을 경우 null)</returns>
        public ITargetable GetClosestTarget(Vector3 origin, float range)
        {
            ITargetable closest = null;
            float minDistanceSqr = range * range; // 거리 제곱 비교로 연산 최적화

            // LINQ를 지양하고 for 루프를 사용하여 Zero Allocation 유지
            for (int i = 0; i < m_activeTargets.Count; i++)
            {
                var target = m_activeTargets[i];

                // 유효성 검사 (사망했거나 비활성화된 경우 제외)
                if (target == null || !target.IsActive || target.IsDead)
                {
                    continue;
                }

                float distanceSqr = (target.Position - origin).sqrMagnitude;
                if (distanceSqr < minDistanceSqr)
                {
                    minDistanceSqr = distanceSqr;
                    closest = target;
                }
            }

            return closest;
        }

        /// <summary>
        /// 현재 관리 중인 모든 활성 타겟 목록을 반환합니다.
        /// </summary>
        public IReadOnlyList<ITargetable> GetAllActiveTargets() => m_activeTargets;

        #endregion
    }
}
