using System;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 경험치와 레벨업 시스템을 관리하는 POCO 클래스입니다.
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

        #region 로직
        public void Init(int initialLevel = 1)
        {
            Level = initialLevel;
            CurrentExp = 0f;
            MaxExp = CalculateMaxExp(Level);
        }

        public void AddExperience(float amount)
        {
            CurrentExp += amount;
            
            while (CurrentExp >= MaxExp)
            {
                CurrentExp -= MaxExp;
                Level++;
                MaxExp = CalculateMaxExp(Level);
                OnLevelUp?.Invoke(Level);
            }

            OnExpChanged?.Invoke(CurrentExp, MaxExp);
        }

        public float GetProgress() => MaxExp > 0 ? CurrentExp / MaxExp : 0f;

        private float CalculateMaxExp(int level)
        {
            return (level + 1) * 10f; // 기존 공식 유지
        }
        #endregion
    }
}
