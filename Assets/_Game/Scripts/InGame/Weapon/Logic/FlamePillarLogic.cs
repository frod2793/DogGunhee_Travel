using System.Collections.Generic;
using InGame.Mob.MobBase;
using UnityEngine;

namespace InGame.Weapon.Logic
{
    /// <summary>
    /// 불기둥(Flame Pillar)의 비즈니스 로직(데미지 계산, 피격 상태 관리)을 담당하는 POCO 클래스입니다.
    /// MonoBehaviour 의존성 없이 순수 C# 로직만 포함합니다.
    /// </summary>
    public class FlamePillarLogic
    {
        #region 내부 상태 및 변수
        
        private readonly HashSet<MobBase> m_hitMobs = new HashSet<MobBase>();
        
        #endregion

        #region 프로퍼티
        
        public float DirectDamage { get; private set; }
        public float DotDamage { get; private set; }
        public float Duration { get; private set; }
        public int TickCount { get; private set; }
        public Color HitFlashColor { get; private set; }

        #endregion

        #region 생성자 및 상태 관리

        public FlamePillarLogic(float directDamage, float dotDamage, float duration, int tickCount, Color hitFlashColor)
        {
            DirectDamage = directDamage;
            DotDamage = dotDamage;
            Duration = duration;
            TickCount = tickCount;
            HitFlashColor = hitFlashColor;
        }

        /// <summary>
        /// 씬에 배치될 때 상태를 초기화합니다.
        /// </summary>
        public void Reset()
        {
            m_hitMobs.Clear();
        }

        #endregion

        #region 로직 메서드

        /// <summary>
        /// 특정 몹이 이미 피격되었는지 확인하고, 안 되었다면 등록합니다.
        /// </summary>
        /// <param name="mob">대상 몹</param>
        /// <returns>이미 피격된 적이면 false, 새로 피격된 적이면 true</returns>
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
        /// 현재 피격된 몹 목록에서 죽은 몹 등을 정리합니다. (선택사항)
        /// </summary>
        public void Cleanup()
        {
            m_hitMobs.RemoveWhere(mob => mob == null || mob.IsDead);
        }

        #endregion
    }
}
