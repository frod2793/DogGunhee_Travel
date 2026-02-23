using System.Collections.Generic;
using InGame.Mob.MobBase;
using UnityEngine;

namespace InGame.Weapon.Logic
{
    /// <summary>
    /// [설명]: 불기둥(Flame Pillar)의 비즈니스 로직(데미지 계산, 중복 피격 관리)을 담당하는 클래스입니다.
    /// </summary>
    public class FlamePillarLogic
    {
        #region 내부 변수
        
        // 피격된 몹 관리
        private readonly HashSet<MobBase> m_hitMobs = new HashSet<MobBase>();
        
        #endregion

        #region 프로퍼티
        
        public float DirectDamage { get; private set; }
        public float DotDamage { get; private set; }
        public float Duration { get; private set; }
        public int TickCount { get; private set; }
        public Color HitFlashColor { get; private set; }

        #endregion

        #region 생성자 및 초기화

        public FlamePillarLogic(float directDamage, float dotDamage, float duration, int tickCount, Color hitFlashColor)
        {
            DirectDamage = directDamage;
            DotDamage = dotDamage;
            Duration = duration;
            TickCount = tickCount;
            HitFlashColor = hitFlashColor;
        }

        /// <summary>
        /// 오브젝트가 활성화될 때 피격 목록을 초기화합니다.
        /// </summary>
        public void Reset()
        {
            m_hitMobs.Clear();
        }

        #endregion

        #region 로직 메서드

        /// <summary>
        /// 특정 몹이 이미 피격되었는지 확인하고, 아직이라면 등록합니다.
        /// </summary>
        /// <param name="mob">대상 몬스터</param>
        /// <returns>신규 피격이면 true, 이미 맞았다면 false</returns>
        public bool TryHit(MobBase mob)
        {
            if (mob == null || mob.IsDead)
            {
                return false;
            }
            
            if (m_hitMobs.Contains(mob))
            {
                return false;
            }

            m_hitMobs.Add(mob);
            return true;
        }

        /// <summary>
        /// 피격 목록에서 소멸된 몹을 정리합니다. (메모리 관리용)
        /// </summary>
        public void Cleanup()
        {
            m_hitMobs.RemoveWhere(mob => mob == null || mob.IsDead);
        }

        #endregion
    }
}