using System;
using UnityEngine;

namespace InGame.Player.Player_Base
{
    /// <summary>
    /// [설명]: 플레이어의 경험치 획득, 레벨업 계산 및 성장을 관리하는 순수 로직 클래스입니다.
    /// PlayerBase나 GameManager에 의해 소유되며 MonoBehaviour를 상속받지 않습니다.
    /// </summary>
    public class ExperienceSystem
    {
        #region 공개 프로퍼티

        /// <summary>
        /// [설명]: 플레이어의 현재 레벨입니다.
        /// </summary>
        public int Level { get; private set; } = 1;

        /// <summary>
        /// [설명]: 현재 보유 중인 경험치량입니다.
        /// </summary>
        public float CurrentExp { get; private set; } = 0f;

        /// <summary>
        /// [설명]: 다음 레벨업을 위해 필요한 총 경험치량입니다.
        /// </summary>
        public float MaxExp { get; private set; } = 100f;

        #endregion

        #region 이벤트

        /// <summary>
        /// [설명]: 레벨업 시 호출되며, 변경된 새로운 레벨을 전달합니다.
        /// </summary>
        public event Action<int> OnLevelUp;

        /// <summary>
        /// [설명]: 경험치가 변경될 때 호출되며, 현재 경험치와 최대 경험치를 전달합니다.
        /// </summary>
        public event Action<float, float> OnExpChanged;

        #endregion

        #region 초기화

        /// <summary>
        /// [설명]: 경험치 시스템을 초기화하고 첫 상태 이벤트를 발생시킵니다.
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

        #region 핵심 비즈니스 로직

        /// <summary>
        /// [설명]: 경험치를 추가하고 요구량을 초과할 경우 레벨업 처리를 수행합니다.
        /// </summary>
        /// <param name="amount">획득한 경험치 양</param>
        public void AddExperience(float amount)
        {
            if (amount <= 0)
            {
                return;
            }

            CurrentExp += amount;

            // 누적 경험치가 요구량을 초과하는 동안 반복 (연속 레벨업 대응)
            while (CurrentExp >= MaxExp && MaxExp > 0)
            {
                CurrentExp -= MaxExp;
                Level++;
                MaxExp = CalculateMaxExp(Level);

                // 레벨업 이벤트 알림
                OnLevelUp?.Invoke(Level);
            }

            // UI 갱신을 위한 경험치 변경 알림
            OnExpChanged?.Invoke(CurrentExp, MaxExp);
        }

        #endregion

        #region 유틸리티 및 계산

        /// <summary>
        /// [설명]: 현재 레벨에서의 경험치 진행률(0.0 ~ 1.0)을 계산하여 반환합니다.
        /// </summary>
        /// <returns>경험치 퍼센트 비율</returns>
        public float GetProgress()
        {
            if (MaxExp <= 0)
            {
                return 0f;
            }
            return Mathf.Clamp01(CurrentExp / MaxExp);
        }

        /// <summary>
        /// [설명]: 레벨에 따른 목표 경험치 요구량을 공식에 따라 산출합니다.
        /// </summary>
        /// <param name="level">계산할 대상 레벨</param>
        /// <returns>해당 레벨의 최대 경험치량</returns>
        private float CalculateMaxExp(int level)
        {
            // 성장 곡선 공식 설계
            float requiredExp = (level + 1) * 10f;

            // 최소값 보장
            return Mathf.Max(10f, requiredExp);
        }

        #endregion
    }
}