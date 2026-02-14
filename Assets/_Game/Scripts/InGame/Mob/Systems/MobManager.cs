using System.Collections.Generic;
using UnityEngine;

namespace InGame.Mob.Systems
{
    /// <summary>
    /// [설명]: 현재 활성화된 모든 타겟(몬스터 등)을 관리하고 효율적인 타겟 탐색 기능을 제공하는 시스템 클래스입니다.
    /// Singleton 대신 DI 방식으로 GameManager에 의해 생성 및 주입되어 유지보수성을 높입니다.
    /// </summary>
    public class MobManager
    {
        #region 내부 필드

        /// <summary> 활성화된 타겟 목록 (탐색 성능을 위해 미리 용량 확보) </summary>
        private readonly List<ITargetable> m_activeTargets = new List<ITargetable>(100);

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 몬스터 관리자 객체를 초기화합니다.
        /// </summary>
        public MobManager()
        {
        }

        #endregion

        #region 등록 및 해제

        /// <summary>
        /// [설명]: 타겟을 관리 목록에 등록합니다.
        /// </summary>
        /// <param name="target">등록할 타겟 객체</param>
        public void Register(ITargetable target)
        {
            if (target == null)
            {
                return;
            }

            if (!m_activeTargets.Contains(target))
            {
                m_activeTargets.Add(target);
            }
        }

        /// <summary>
        /// [설명]: 타겟을 관리 목록에서 제거합니다.
        /// </summary>
        /// <param name="target">해제할 타겟 객체</param>
        public void Unregister(ITargetable target)
        {
            if (target == null)
            {
                return;
            }

            m_activeTargets.Remove(target);
        }

        #endregion

        #region 타겟 탐색 로직

        /// <summary>
        /// [설명]: 특정 위치를 기준으로 일정 반경 내에서 가장 가까운 타겟을 찾습니다.
        /// (sqrMagnitude를 활용하여 연산 속도를 최적화합니다.)
        /// </summary>
        /// <param name="origin">탐색 중심 위치</param>
        /// <param name="range">최대 탐색 반경</param>
        /// <returns>가장 인접한 타겟 객체 (없을 경우 null)</returns>
        public ITargetable GetClosestTarget(Vector3 origin, float range)
        {
            ITargetable closest = null;
            float minDistanceSqr = range * range; // 거리 제곱 비교로 연산 최적화

            // Zero Allocation을 위해 for 루프 사용
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
        /// [설명]: 현재 관리 중인 모든 활성 타겟 목록을 읽기 전용으로 반환합니다.
        /// </summary>
        public IReadOnlyList<ITargetable> GetAllActiveTargets()
        {
            return m_activeTargets;
        }

        #endregion
    }
}
