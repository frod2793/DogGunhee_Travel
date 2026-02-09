using UnityEngine;

namespace InGame.Weapon.Base
{
    /// <summary>
    /// 무기의 런타임 스탯(현재 레벨, 계산된 데미지 등)을 관리하는 POCO 클래스입니다.
    /// </summary>
    public class WeaponRuntimeStats
    {
        #region 내부 상태 및 변수

        private readonly R3.ReactiveProperty<float> m_attackPowerRP = new(0f);
        private readonly R3.ReactiveProperty<int> m_currentLevelRP = new(1);

        #endregion

        #region 공개 프로퍼티 (기본 정보)

        /// <summary>
        /// 무기의 정적 데이터 원본
        /// </summary>
        public WeaponDataSO Data { get; private set; }

        /// <summary>
        /// 현재 무기 레벨
        /// </summary>
        public int CurrentLevel { get; private set; } = 1;

        /// <summary>
        /// 최종 계산된 공격력
        /// </summary>
        public float AttackPower { get; set; }

        /// <summary>
        /// 최종 계산된 재사용 대기시간
        /// </summary>
        public float CoolTime { get; set; }

        /// <summary>
        /// 최종 계산된 공격 속도 배율
        /// </summary>
        public float AttackSpeed { get; set; }

        /// <summary>
        /// 최종 계산된 공격 사거리
        /// </summary>
        public float AttackRange { get; set; }

        /// <summary>
        /// 최종 계산된 효과 유지 시간
        /// </summary>
        public float Duration { get; set; }

        /// <summary>
        /// 최종 계산된 투사체 개수
        /// </summary>
        public int ProjectileCount { get; set; }

        /// <summary>
        /// 적 타격 시 기절 유지 시간
        /// </summary>
        public float MobStunTime { get; set; }

        /// <summary>
        /// 진화 여부
        /// </summary>
        public bool IsEvolved { get; set; }

        #endregion

        #region 하위 호환성 프로퍼티 (Legacy 별칭)

        public float CurrentAttackPower => AttackPower;
        public float CurrentCoolTime => CoolTime;
        public float CurrentAttackSpeed => AttackSpeed;
        public float CurrentAttackRange => AttackRange;
        public int CurrentProjectileCount => ProjectileCount;
        public float CurrentDuration => Duration;

        #endregion

        #region 반응형 프로퍼티 (Observer Pattern)

        /// <summary>
        /// 공격력 변화를 구독할 수 있는 ReactiveProperty
        /// </summary>
        public R3.ReadOnlyReactiveProperty<float> AttackPowerRP => m_attackPowerRP;

        /// <summary>
        /// 레벨 변화를 구독할 수 있는 ReactiveProperty
        /// </summary>
        public R3.ReadOnlyReactiveProperty<int> CurrentLevelRP => m_currentLevelRP;

        #endregion

        #region 초기화 및 레벨 관리

        /// <summary>
        /// 무기 데이터를 기반으로 런타임 스탯을 생성합니다.
        /// </summary>
        /// <param name="data">기초 무기 데이터</param>
        public WeaponRuntimeStats(WeaponDataSO data)
        {
            Data = data;
            ResetStats();
        }

        /// <summary>
        /// 스탯을 레벨 1의 기본값으로 초기화합니다.
        /// </summary>
        public void ResetStats()
        {
            if (Data == null)
            {
                return;
            }

            CurrentLevel = 1;
            AttackPower = Data.BaseAttackPower;
            CoolTime = Data.BaseCoolTime;
            AttackSpeed = Data.BaseAttackSpeed;
            AttackRange = Data.BaseAttackRange;
            Duration = Data.BaseDuration;
            ProjectileCount = Data.BaseProjectileCount;
            MobStunTime = 0.5f;
            IsEvolved = false;

            m_attackPowerRP.Value = AttackPower;
            m_currentLevelRP.Value = CurrentLevel;
        }

        /// <summary>
        /// 무기 레벨을 갱신하고 상태를 업데이트합니다.
        /// </summary>
        /// <param name="newLevel">갱신할 레벨</param>
        public void LevelUp(int newLevel)
        {
            CurrentLevel = newLevel;

            // 6레벨 이상일 경우 진화 상태로 간주
            if (CurrentLevel >= 6)
            {
                IsEvolved = true;
            }

            m_currentLevelRP.Value = CurrentLevel;
            m_attackPowerRP.Value = AttackPower;

            // TODO: WeaponDataSO의 Upgrades 리스트를 참조하여 스탯 재계산 로직 추가 필요
        }

        #endregion
    }
}
