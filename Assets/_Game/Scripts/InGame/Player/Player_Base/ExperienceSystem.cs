using System;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 경험치 획득, 레벨업 계산 및 성장을 관리하는 POCO 클래스입니다.
    /// </summary>
    public class ExperienceSystem
    {
        #region 프로퍼티

        public int Level { get; private set; } = 1;
        public float CurrentExp { get; private set; } = 0f;
        public float MaxExp { get; private set; } = 100f;

        #endregion

        #region 이벤트

        public event Action<int> OnLevelUp;
        public event Action<float, float> OnExpChanged;

        #endregion

        #region 초기화 및 제어 로직

        /// <summary>
        /// 경험치 시스템을 초기 레벨로 재설정합니다.
        /// </summary>
        public void Init(int initialLevel = 1)
        {
            Level = initialLevel;
            CurrentExp = 0f;
            MaxExp = CalculateMaxExp(Level);
        }

        /// <summary>
        /// 경험치를 추가하고 필요 시 레벨업 처리를 수행합니다.
        /// </summary>
        /// <param name="amount">추가할 경험치 양</param>
        public void AddExperience(float amount)
        {
            CurrentExp += amount;
            
            // 누적 경험치가 요구량을 넘는 동안 연속 레벨업 처리
            while (CurrentExp >= MaxExp)
            {
                CurrentExp -= MaxExp;
                Level++;
                MaxExp = CalculateMaxExp(Level);
                OnLevelUp?.Invoke(Level);
            }

            OnExpChanged?.Invoke(CurrentExp, MaxExp);
        }

        #endregion

        #region 유틸리티

        /// <summary>
        /// 현재 레벨에서 다음 레벨로 가기 위한 진행률(0~1)을 반환합니다.
        /// </summary>
        public float GetProgress() => MaxExp > 0 ? CurrentExp / MaxExp : 0f;

        /// <summary>
        /// 레벨에 따른 최대 경험치 요구량을 계산합니다.
        /// </summary>
        private float CalculateMaxExp(int level)
        {
            // 성장 곡선 공식
            return (level + 1) * 10f;
        }

        #endregion
    }
}
