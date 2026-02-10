using System;
using UnityEngine; // Mathf 사용을 위해 추가

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// 플레이어의 경험치 획득, 레벨업 계산 및 성장을 관리하는 순수 로직 클래스입니다.
    /// <br/> PlayerBase나 GameManager에 의해 소유됩니다.
    /// </summary>
    public class ExperienceSystem
    {
        #region 1. 프로퍼티 (Properties)

        /// <summary>현재 레벨</summary>
        public int Level { get; private set; } = 1;

        /// <summary>현재 보유 경험치</summary>
        public float CurrentExp { get; private set; } = 0f;

        /// <summary>다음 레벨까지 필요한 총 경험치</summary>
        public float MaxExp { get; private set; } = 100f;

        #endregion

        #region 2. 이벤트 (Events)

        /// <summary>레벨업 시 호출됩니다. (변경된 레벨 전달)</summary>
        public event Action<int> OnLevelUp;

        /// <summary>경험치가 변경될 때 호출됩니다. (현재 경험치, 최대 경험치 전달)</summary>
        public event Action<float, float> OnExpChanged;

        #endregion

        #region 3. 초기화 (Initialization)

        /// <summary>
        /// 경험치 시스템을 초기화합니다.
        /// </summary>
        /// <param name="initialLevel">시작 레벨 (기본값: 1)</param>
        public void Init(int initialLevel = 1)
        {
            Level = Mathf.Max(1, initialLevel);
            CurrentExp = 0f;
            MaxExp = CalculateMaxExp(Level);
            
            // 초기 상태 알림
            OnExpChanged?.Invoke(CurrentExp, MaxExp);
        }

        #endregion

        #region 4. 핵심 로직 (Core Logic)

        /// <summary>
        /// 경험치를 획득하고 레벨업 여부를 체크합니다.
        /// </summary>
        /// <param name="amount">획득한 경험치 양</param>
        public void AddExperience(float amount)
        {
            if (amount <= 0) return;

            CurrentExp += amount;
            

            // 누적 경험치가 요구량을 초과하는 동안 반복 (한 번에 여러 레벨업 가능)
            while (CurrentExp >= MaxExp && MaxExp > 0)
            {
                CurrentExp -= MaxExp;
                Level++;
                MaxExp = CalculateMaxExp(Level);

                // 레벨업 이벤트 발생
                OnLevelUp?.Invoke(Level);
            }

            // 경험치 변경 알림 (레벨업 후 남은 경험치 반영)
            // UI 갱신을 위해 레벨업 여부와 관계없이 호출
            OnExpChanged?.Invoke(CurrentExp, MaxExp);
        }

        #endregion

        #region 5. 유틸리티 및 계산 (Helpers)

        /// <summary>
        /// 현재 레벨의 진행률(0.0 ~ 1.0)을 반환합니다. (UI 슬라이더용)
        /// </summary>
        public float GetProgress()
        {
            if (MaxExp <= 0) return 0f;
            return Mathf.Clamp01(CurrentExp / MaxExp);
        }

        /// <summary>
        /// 특정 레벨의 필요 경험치량을 계산합니다.
        /// </summary>
        private float CalculateMaxExp(int level)
        {
            // 성장 곡선 공식 (선형 예시: 레벨 * 10)
            // 기획에 따라 지수 함수 등으로 변경 가능
            float requiredExp = (level + 1) * 10f;
            
            // 0으로 나누기 방지를 위한 최소값 보장
            return Mathf.Max(10f, requiredExp);
        }

        #endregion
    }
}